using System.ComponentModel;
using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32.SafeHandles;

namespace PcNinja.Win11Upgrade.Core;

public sealed record AppExecutableInfo(string Version, string FileVersion, string Sha256, long SizeBytes, string? SignerThumbprint);

/// <summary>
/// Product-pinned local update staging. Keep this object alive through replacement:
/// its non-delete-sharing directory handles prevent ancestor rename/junction swaps.
/// Existing untrusted folders are rejected, never taken over or silently hardened.
/// </summary>
public sealed class AppUpdateWorkspace : IDisposable
{
    internal const string PublicDirectorySddl = "O:BAG:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;0x1200a9;;;BU)";
    internal const string PrivateDirectorySddl = "O:BAG:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)";
    private const string PublicFileSddl = "O:BAG:BAD:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;0x1200a9;;;BU)";
    private const string PrivateFileSddl = "O:BAG:BAD:P(A;;FA;;;SY)(A;;FA;;;BA)";
    private const int MaximumRequestBytes = 16 * 1024;
    private const long SpaceMarginBytes = 64L * 1024 * 1024;
    private const uint ReadControl = 0x00020000, ReadAttributes = 0x80;
    private const uint GenericRead = 0x80000000, GenericWrite = 0x40000000, DeleteAccess = 0x00010000;
    private const uint OpenReparsePoint = 0x00200000, BackupSemantics = 0x02000000;
    private const uint OwnerAndDacl = 5;
    private static readonly JsonSerializerOptions RequestJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 16,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
    };
    private readonly DirectoryAnchors _anchors;
    private bool _cleanupAllowed;
    private bool _disposed;

    public const string StateDirectoryName = ".pcninja-updates";
    public const string ResultFileName = "Last-AppUpdate.json";

    /// <summary>
    /// Last app-update result, kept with the diagnostic log in ProgramData so the
    /// portable folder ends with only the new and the archived EXE.
    /// </summary>
    public static string ResultPath => Path.Combine(AppLog.Folder, ResultFileName);

    /// <summary>Pre-release builds kept the result inside the portable staging folder.</summary>
    public static string LegacyResultPath(string sourceDirectory) => Path.Combine(sourceDirectory, StateDirectoryName, ResultFileName);
    public string SourceDirectory { get; }
    public string StateDirectory => Path.Combine(SourceDirectory, StateDirectoryName);
    public string DirectoryPath { get; }
    public string CandidatePath => Path.Combine(DirectoryPath, "candidate.exe");
    public string HelperPath => Path.Combine(DirectoryPath, "UpdateHelper.exe");
    public string RequestPath => Path.Combine(DirectoryPath, "request.json");
    public string PreviousPath => Path.Combine(DirectoryPath, "previous.exe");
    public string DisplacedPath => Path.Combine(DirectoryPath, "displaced.exe");
    public string FailedPath => Path.Combine(DirectoryPath, "failed.exe");
    public string TransactionPath => Path.Combine(DirectoryPath, "transaction.json");

    private AppUpdateWorkspace(string sourceDirectory, string operationId, DirectoryAnchors anchors, bool cleanupAllowed)
    {
        SourceDirectory = sourceDirectory;
        DirectoryPath = Path.Combine(StateDirectory, operationId);
        _anchors = anchors;
        _cleanupAllowed = cleanupAllowed;
    }

    public static AppUpdateWorkspace Create(string sourceDirectory, string operationId, long candidateBytes, long helperBytes) =>
        OpenCore(operationId, candidateBytes, helperBytes, create: true, sourceDirectory);

    public static AppUpdateWorkspace Open(string sourceDirectory, string operationId) =>
        OpenCore(operationId, 0, 0, create: false, sourceDirectory);

    // Test-only API: one explicitly named random child of the current temp folder.
    // Never exposed by application arguments or by the production Create/Open path.
    internal static AppUpdateWorkspace CreateFixture(string parent, string operationId, long candidateBytes, long helperBytes) =>
        OpenCore(operationId, candidateBytes, helperBytes, true, ValidateFixtureParent(parent));
    internal static AppUpdateWorkspace OpenFixture(string parent, string operationId) =>
        OpenCore(operationId, 0, 0, false, ValidateFixtureParent(parent));

    private static AppUpdateWorkspace OpenCore(string operationId, long candidateBytes, long helperBytes, bool create, string sourceDirectory)
    {
        operationId = NormalizeOperationId(operationId);
        var required = create ? RequiredFreeBytes(candidateBytes, helperBytes) : 0;
        var parent = NormalizeLocalPath(sourceDirectory);
        var anchors = AnchorLocalChain(parent);
        try
        {
            if (create && new DriveInfo(Path.GetPathRoot(parent)!).AvailableFreeSpace < required)
                throw new IOException("There is not enough free space for the application candidate, helper and update safety margin. Windows upgrade workspace is checked separately.");
            var staging = Path.Combine(parent, StateDirectoryName);
            anchors.Add(OpenControlledDirectory(staging, true, create));
            if (create) File.SetAttributes(staging, File.GetAttributes(staging) | FileAttributes.Hidden);
            var operation = Path.Combine(staging, operationId);
            anchors.Add(OpenControlledDirectory(operation, true, create, requireNew: create));
            return new AppUpdateWorkspace(parent, operationId, anchors, cleanupAllowed: create);
        }
        catch { anchors.Dispose(); throw; }
    }

    public void WriteRequest<T>(T request)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(request);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(request, RequestJson);
        if (bytes.Length is <= 0 or > MaximumRequestBytes)
            throw new InvalidDataException("The application update request exceeds its size limit.");
        using var security = new NativeSecurity(PrivateFileSddl);
        var attributes = security.Attributes;
        using var handle = CreateFileWithSecurity(RequestPath, GenericWrite | ReadControl | ReadAttributes, 0,
            ref attributes, 1, OpenReparsePoint, IntPtr.Zero); // CREATE_NEW, never overwrite a handoff.
        if (handle.IsInvalid) throw NativeFailure("Could not create the private update request.");
        VerifyRegularFile(handle, RequestPath);
        ValidateControlledDescriptor(ReadDescriptor(handle), privateScope: true, requireProtected: true);
        using var stream = new FileStream(handle, FileAccess.Write);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }

    public T ReadRequest<T>()
    {
        EnsureAlive();
        using var handle = OpenRegularFile(RequestPath, GenericRead | ReadControl | ReadAttributes, 1);
        ValidateControlledDescriptor(ReadDescriptor(handle), privateScope: true, requireProtected: false);
        using var stream = new FileStream(handle, FileAccess.Read);
        if (stream.Length is <= 0 or > MaximumRequestBytes)
            throw new InvalidDataException("The private update request has an invalid size.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return ParseRequest<T>(bytes);
    }

    public void WriteTransactionState<T>(T state)
    {
        EnsureAlive();
        ArgumentNullException.ThrowIfNull(state);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(state, RequestJson);
        if (bytes.Length is <= 0 or > MaximumRequestBytes)
            throw new InvalidDataException("The private update transaction state exceeds its size limit.");
        var temporary = Path.Combine(DirectoryPath, "transaction-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var security = new NativeSecurity(PrivateFileSddl))
            {
                var attributes = security.Attributes;
                using var handle = CreateFileWithSecurity(temporary, GenericWrite | ReadControl | ReadAttributes, 0,
                    ref attributes, 1, OpenReparsePoint, IntPtr.Zero);
                if (handle.IsInvalid) throw NativeFailure("Could not create the private transaction replacement.");
                VerifyRegularFile(handle, temporary);
                ValidateControlledDescriptor(ReadDescriptor(handle), privateScope: true, requireProtected: true);
                using var stream = new FileStream(handle, FileAccess.Write);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            if (ExistsExactly(TransactionPath))
            {
                using (var current = OpenRegularFile(TransactionPath, GenericRead | ReadControl | ReadAttributes, 1))
                    ValidateControlledDescriptor(ReadDescriptor(current), privateScope: true, requireProtected: true);
                File.Replace(temporary, TransactionPath, null, ignoreMetadataErrors: false);
            }
            else File.Move(temporary, TransactionPath, overwrite: false);
        }
        finally
        {
            if (ExistsExactly(temporary))
                DeleteExactFiles([temporary], privateScope: true);
        }
    }

    internal T ReadTransactionState<T>()
    {
        EnsureAlive();
        using var handle = OpenRegularFile(TransactionPath, GenericRead | ReadControl | ReadAttributes, 1);
        ValidateControlledDescriptor(ReadDescriptor(handle), privateScope: true, requireProtected: false);
        using var stream = new FileStream(handle, FileAccess.Read);
        if (stream.Length is <= 0 or > MaximumRequestBytes)
            throw new InvalidDataException("The private update transaction state has an invalid size.");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        return ParseRequest<T>(bytes);
    }

    internal static T ParseRequest<T>(byte[] bytes)
    {
        if (bytes.Length is <= 0 or > MaximumRequestBytes)
            throw new InvalidDataException("The application update request exceeds its size limit.");
        using var document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 });
        RejectDuplicateProperties(document.RootElement);
        return document.RootElement.Deserialize<T>(RequestJson)
            ?? throw new InvalidDataException("The application update request is empty.");
    }

    /// <summary>Call before starting the helper. An opened helper workspace can never clean staging.</summary>
    public void MarkHandoffStarted() { EnsureAlive(); _cleanupAllowed = false; }

    /// <summary>Only before handoff, with no backup and no mapped/running helper; no recursive deletion.</summary>
    public void CleanupUncommitted()
    {
        EnsureAlive();
        if (!_cleanupAllowed)
            throw new InvalidOperationException("Update recovery or helper handoff is present; staging is retained.");
        DeleteExactFiles([HelperPath, CandidatePath, RequestPath, PreviousPath, DisplacedPath, FailedPath, TransactionPath], privateScope: true);
    }

    /// <summary>
    /// Removes finished update staging beside the application: every exact operation
    /// folder whose helper is no longer running, the pre-release result file and then
    /// the hidden staging folder itself. Call only while holding the application-update
    /// operation gate (no download or helper handoff can be active). Unknown entries are
    /// never deleted; anything locked or unexpected is retained and reported.
    /// </summary>
    public static AppUpdateStagingCleanup RemoveFinishedStaging(string sourceDirectory) =>
        RemoveFinishedStagingCore(NormalizeLocalPath(sourceDirectory));

    internal static AppUpdateStagingCleanup RemoveFinishedStagingFixture(string parent) =>
        RemoveFinishedStagingCore(ValidateFixtureParent(parent));

    private static AppUpdateStagingCleanup RemoveFinishedStagingCore(string parent)
    {
        var staging = Path.Combine(parent, StateDirectoryName);
        if (!Directory.Exists(staging)) return new(0, [], StagingRemoved: false, StagingPresent: false);

        // Validate the protected root first: no reparse points, Administrators/SYSTEM ACL.
        using (var anchors = AnchorLocalChain(parent)) anchors.Add(OpenControlledDirectory(staging, true, create: false));

        var removed = 0;
        var retained = new List<string>();
        foreach (var entry in Directory.EnumerateFileSystemEntries(staging).ToArray())
        {
            var name = Path.GetFileName(entry);
            try
            {
                if (string.Equals(name, ResultFileName, StringComparison.OrdinalIgnoreCase) && !Directory.Exists(entry))
                    DeleteExactFiles([entry], privateScope: false);
                else if (IsOperationId(name) && Directory.Exists(entry))
                {
                    using (var workspace = OpenCore(name, 0, 0, create: false, parent))
                    {
                        FinalizeTransaction(workspace, name);
                        var files = new List<string> { workspace.HelperPath, workspace.CandidatePath, workspace.RequestPath,
                            workspace.PreviousPath, workspace.DisplacedPath, workspace.FailedPath,
                            workspace.TransactionPath, Path.Combine(workspace.DirectoryPath, "result.tmp") };
                        foreach (var temporary in Directory.EnumerateFiles(workspace.DirectoryPath, "transaction-*.tmp"))
                        {
                            var fileName = Path.GetFileName(temporary);
                            var idPart = fileName["transaction-".Length..^".tmp".Length];
                            if (Guid.TryParseExact(idPart, "N", out _)) files.Add(temporary);
                        }
                        DeleteExactFiles(files.ToArray(), privateScope: false);
                    }
                    Directory.Delete(entry, recursive: false); // Fails, and keeps it, if anything unknown remains.
                    removed++;
                }
                else retained.Add($"{name}: not created by the updater");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or
                InvalidDataException or ArgumentException or System.ComponentModel.Win32Exception)
            {
                retained.Add($"{name}: {ex.Message}");
            }
        }

        if (retained.Count > 0) return new(removed, retained, StagingRemoved: false, StagingPresent: true);
        Directory.Delete(staging, recursive: false);
        return new(removed, retained, StagingRemoved: true, StagingPresent: false);
    }

    public static int MarkAwaitingGuiTransactionsReady(string sourceDirectory, string currentExecutablePath)
    {
        var parent = NormalizeLocalPath(sourceDirectory);
        var expectedPath = NormalizeLocalPath(Path.Combine(parent, AppUpdateIdentity.AssetName));
        if (!string.Equals(NormalizeLocalPath(currentExecutablePath), expectedPath, StringComparison.OrdinalIgnoreCase))
            return 0;
        var installed = InspectExecutable(expectedPath);
        using var installedGuard = HoldOriginal(expectedPath, installed.Sha256);
        var staging = Path.Combine(parent, StateDirectoryName);
        if (!Directory.Exists(staging)) return 0;
        using var anchors = AnchorLocalChain(parent);
        anchors.Add(OpenControlledDirectory(staging, true, create: false));
        var marked = 0;
        foreach (var entry in Directory.EnumerateDirectories(staging).ToArray())
        {
            var id = Path.GetFileName(entry);
            if (!IsOperationId(id)) continue;
            try
            {
                using var workspace = OpenCore(id, 0, 0, create: false, parent);
                var state = workspace.ReadTransactionState<AppUpdateTransactionState>();
                if (state.Status != "awaiting-gui") continue;
                if (state.SchemaVersion != 1 || state.OperationId != id ||
                    state.ParentHash is not { Length: 64 } || !state.ParentHash.All(Uri.IsHexDigit) ||
                    state.ReleaseHash is not { Length: 64 } || !state.ReleaseHash.All(Uri.IsHexDigit) ||
                    !TryParseWindowsFileVersion(state.ParentFileVersion, out var parentVersion) ||
                    Path.GetFileName(state.OriginalName) != state.OriginalName ||
                    !Path.GetExtension(state.OriginalName).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The pending GUI transaction identity is invalid.");
                if (!installed.Sha256.Equals(state.ReleaseHash, StringComparison.OrdinalIgnoreCase)) continue;
                VerifyHashAndFileVersion(expectedPath, state.ReleaseHash, null, "running successor");
                VerifyHashAndFileVersion(workspace.PreviousPath, state.ParentHash, parentVersion,
                    "pending predecessor snapshot", privateScope: true);
                workspace.WriteTransactionState(state with { Status = "successor-ready" });
                marked++;
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or
                System.ComponentModel.Win32Exception or System.Security.SecurityException or ArgumentException)
            {
                AppLog.Info($"Pending app-update transaction {id} remains protected: {ex.Message}");
            }
        }
        return marked;
    }

    private static void FinalizeTransaction(AppUpdateWorkspace workspace, string operationId)
    {
        var state = workspace.ReadTransactionState<AppUpdateTransactionState>();
        if (state.SchemaVersion != 1 || state.OperationId != operationId ||
            state.ParentHash is not { Length: 64 } || !state.ParentHash.All(Uri.IsHexDigit) ||
            state.ReleaseHash is not { Length: 64 } || !state.ReleaseHash.All(Uri.IsHexDigit) ||
            !TryParseWindowsFileVersion(state.ParentFileVersion, out var parentVersion) ||
            Path.GetFileName(state.OriginalName) != state.OriginalName ||
            !Path.GetExtension(state.OriginalName).Equals(".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The update transaction identity is incomplete or invalid; recovery files were retained.");

        var target = Path.Combine(workspace.SourceDirectory, AppUpdateIdentity.AssetName);
        if (state.Status == "rolled-back")
        {
            VerifyHashAndFileVersion(target, state.ParentHash, parentVersion, "restored predecessor");
            VerifyRecoveryIfPresent(workspace.FailedPath, state.ReleaseHash, null, "failed successor");
            VerifyRecoveryIfPresent(workspace.DisplacedPath, state.ParentHash, parentVersion, "displaced predecessor");
        }
        else if (state.Status == "rolled-back-separate")
        {
            VerifyHashAndFileVersion(Path.Combine(workspace.SourceDirectory, state.OriginalName),
                state.ParentHash, parentVersion, "separate predecessor");
            VerifyRecoveryIfPresent(target, state.ReleaseHash, null, "reused successor");
        }
        else if (state.Status is "successor-ready" or "ready-needs-archive" or "ready")
        {
            VerifyHashAndFileVersion(target, state.ReleaseHash, null, "installed successor");
            var archive = FindVerifiedArchive(workspace.SourceDirectory, state, parentVersion);
            if (archive is null && ExistsExactly(workspace.PreviousPath))
            {
                VerifyHashAndFileVersion(workspace.PreviousPath, state.ParentHash, parentVersion,
                    "predecessor recovery snapshot", privateScope: true);
                var result = ArchiveOriginalExact(workspace.SourceDirectory, workspace.PreviousPath, state.ParentHash,
                    parentVersion, state.OriginalName, privateSource: true, verifyExecutable: true);
                archive = result.Path;
            }
            if (archive is null)
                throw new IOException("The verified predecessor archive is unavailable; recovery files were retained.");
            RetireSeparateOriginal(workspace.SourceDirectory, state, target);
            state = state with { Status = "ready", ArchivePath = archive };
            workspace.WriteTransactionState(state);
            VerifyRecoveryIfPresent(workspace.DisplacedPath, state.ParentHash, parentVersion, "displaced predecessor");
        }
        else
            throw new InvalidDataException("The transaction has no cleanup-safe terminal state; recovery files were retained.");

        VerifyRecoveryIfPresent(workspace.PreviousPath, state.ParentHash, parentVersion, "predecessor snapshot");
        VerifyRecoveryIfPresent(workspace.DisplacedPath, state.ParentHash, parentVersion, "displaced predecessor");
        VerifyRecoveryIfPresent(workspace.FailedPath, state.Status == "rolled-back" ? state.ReleaseHash : state.ReleaseHash,
            null, "failed or superseded successor");
        foreach (var archive in LegacyArchivePaths(workspace.SourceDirectory, state.OriginalName))
        {
            if (!ExistsExactly(archive)) continue;
            using var legacy = OpenRegularFile(archive, GenericRead | ReadControl | ReadAttributes, 1);
            if (!HashHandle(legacy).Equals(state.ParentHash, StringComparison.OrdinalIgnoreCase))
                continue; // An unrelated legacy archive is never removed.
            if (state.Status == "ready" && FindVerifiedArchive(workspace.SourceDirectory, state, parentVersion) is not null)
                DeleteByHandle(legacy);
        }
    }

    private static string? FindVerifiedArchive(string parent, AppUpdateTransactionState state, string parentVersion)
    {
        var names = new[] { ArchiveName(parentVersion), ArchiveName(parentVersion, state.ParentHash) };
        foreach (var name in names.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var path = Path.Combine(parent, name);
            if (!ExistsExactly(path)) continue;
            VerifyHashAndFileVersion(path, state.ParentHash, parentVersion, "predecessor archive");
            if (state.ArchivePath is not null && !string.Equals(Path.GetFullPath(state.ArchivePath),
                Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The recorded predecessor archive path changed; recovery files were retained.");
            return path;
        }
        return null;
    }

    private static void VerifyRecoveryIfPresent(string path, string expectedHash, string? expectedVersion, string description)
    {
        if (ExistsExactly(path))
            VerifyHashAndFileVersion(path, expectedHash, expectedVersion, description, privateScope: true);
    }

    private static void VerifyHashAndFileVersion(string path, string expectedHash, string? expectedVersion,
        string description, bool privateScope = false)
    {
        using (var file = OpenRegularFile(path, GenericRead | ReadControl | ReadAttributes, 1))
            ValidateControlledDescriptor(ReadDescriptor(file), privateScope, requireProtected: true);
        var actual = InspectExecutable(path);
        if (!actual.Sha256.Equals(expectedHash, StringComparison.OrdinalIgnoreCase) ||
            expectedVersion is not null && actual.FileVersion != expectedVersion)
            throw new InvalidDataException($"The {description} failed exact hash/FileVersion verification.");
    }

    private static void RetireSeparateOriginal(string parent, AppUpdateTransactionState state, string fixedTarget)
    {
        if (state.OriginalName.Equals(Path.GetFileName(fixedTarget), StringComparison.OrdinalIgnoreCase)) return;
        var original = Path.Combine(parent, state.OriginalName);
        if (!ExistsExactly(original)) return;
        var actual = InspectExecutable(original);
        if (!actual.Sha256.Equals(state.ParentHash, StringComparison.OrdinalIgnoreCase) ||
            actual.FileVersion != state.ParentFileVersion)
            throw new InvalidDataException("A separate predecessor changed; it was retained beside its verified archive.");
        using var file = OpenRegularFile(original, GenericRead | DeleteAccess | ReadControl | ReadAttributes, 1);
        if (!HashHandle(file).Equals(state.ParentHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A separate predecessor changed while being retired.");
        DeleteByHandle(file);
    }

    private static bool IsOperationId(string name)
    {
        try { return NormalizeOperationId(name) == name; } // Exact lowercase "N" GUID, as created.
        catch (ArgumentException) { return false; }
    }

    private static void DeleteExactFiles(string[] paths, bool privateScope)
    {
        var files = new List<SafeFileHandle>();
        try
        {
            // Acquire every file exclusively before marking any for deletion. A running
            // image cannot grant GENERIC_WRITE; do not partially clean an active helper.
            foreach (var path in paths)
            {
                if (!ExistsExactly(path)) continue;
                var handle = OpenRegularFile(path, GenericRead | GenericWrite | DeleteAccess | ReadControl | ReadAttributes, 0);
                files.Add(handle);
                ValidateControlledDescriptor(ReadDescriptor(handle), privateScope, requireProtected: false);
            }
            foreach (var file in files)
            {
                var disposition = new FileDisposition { DeleteFile = true };
                if (!SetFileInformationByHandle(file, 4, ref disposition, (uint)Marshal.SizeOf<FileDisposition>()))
                    throw NativeFailure("Could not remove an exact uncommitted staging file; remaining files were retained.");
            }
        }
        finally { foreach (var file in files) file.Dispose(); }
    }

    /// <summary>Final versioned files are readable; staging and request remain private.</summary>
    private static void SetFileAcl(string path, bool publicRead, AppUpdateManifest release, string? signer)
    {
        using var file = OpenRegularFile(path, ReadControl | ReadAttributes | 0x00040000 | 0x00080000, 1);
        VerifyExecutableInfo(InspectExecutable(path), release, signer);
        using var security = new NativeSecurity(publicRead ? PublicFileSddl : PrivateFileSddl);
        if (!GetSecurityDescriptorOwner(security.Pointer, out var owner, out _) ||
            !GetSecurityDescriptorDacl(security.Pointer, out var present, out var dacl, out _) || !present)
            throw NativeFailure("Could not prepare the application file ACL.");
        var result = SetSecurityInfo(file, 1, OwnerAndDacl | 0x80000000, owner, IntPtr.Zero, dacl, IntPtr.Zero);
        if (result != 0) throw NativeFailure("Could not secure the installed application file.", (int)result);
        ValidateControlledDescriptor(ReadDescriptor(file), privateScope: !publicRead, requireProtected: true);
    }

    private static void SetArchivedFileAcl(string path, string expectedHash)
    {
        using var file = OpenRegularFile(path, ReadControl | ReadAttributes | 0x00040000 | 0x00080000, 1);
        if (!HashHandle(file).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A predecessor archive changed before its permissions were set.");
        using var security = new NativeSecurity(PublicFileSddl);
        if (!GetSecurityDescriptorOwner(security.Pointer, out var owner, out _) ||
            !GetSecurityDescriptorDacl(security.Pointer, out var present, out var dacl, out _) || !present)
            throw NativeFailure("Could not prepare predecessor archive permissions.");
        var result = SetSecurityInfo(file, 1, OwnerAndDacl | 0x80000000, owner, IntPtr.Zero, dacl, IntPtr.Zero);
        if (result != 0) throw NativeFailure("Could not publish the verified predecessor archive.", (int)result);
        ValidateControlledDescriptor(ReadDescriptor(file), privateScope: false, requireProtected: true);
    }

    private static void SetRecoveryFileAcl(string path, string expectedHash)
    {
        using var file = OpenRegularFile(path, ReadControl | ReadAttributes | 0x00040000 | 0x00080000, 1);
        if (!HashHandle(file).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("A recovery file changed before its permissions were set.");
        using var security = new NativeSecurity(PrivateFileSddl);
        if (!GetSecurityDescriptorOwner(security.Pointer, out var owner, out _) ||
            !GetSecurityDescriptorDacl(security.Pointer, out var present, out var dacl, out _) || !present)
            throw NativeFailure("Could not prepare recovery-file permissions.");
        var result = SetSecurityInfo(file, 1, OwnerAndDacl | 0x80000000, owner, IntPtr.Zero, dacl, IntPtr.Zero);
        if (result != 0) throw NativeFailure("Could not secure the application recovery file.", (int)result);
        ValidateControlledDescriptor(ReadDescriptor(file), privateScope: true, requireProtected: true);
    }

    public string TargetPath(AppUpdateManifest release)
    {
        EnsureAlive();
        AppUpdateClient.ValidateManifest(release);
        return Path.Combine(SourceDirectory, AppUpdateIdentity.AssetName);
    }

    public void CopyExistingCandidate(AppUpdateManifest release, string? signer)
    {
        var target = TargetPath(release);
        using var guard = HoldVerifiedCandidate(target, release, signer);
        File.Copy(target, CandidatePath, overwrite: false);
        SetFileAcl(CandidatePath, publicRead: false, release, signer); // CopyFile may preserve a user-writable source ACL.
    }

    public void PrepareHelper(AppUpdateManifest release, string? signer)
    {
        EnsureAlive();
        SetFileAcl(CandidatePath, publicRead: false, release, signer);
        VerifyCandidate(CandidatePath, release, signer);
        File.Copy(CandidatePath, HelperPath, overwrite: false);
        SetFileAcl(HelperPath, publicRead: false, release, signer);
        VerifyCandidate(HelperPath, release, signer);
    }

    /// <summary>Never overwrite. Keep exact bytes and ancestors locked through CreateProcess.</summary>
    public IDisposable PublishAndHold(AppUpdateManifest release, string? signer)
    {
        EnsureAlive();
        var target = TargetPath(release);
        if (!ExistsExactly(target))
        {
            VerifyCandidate(CandidatePath, release, signer);
            SetFileAcl(CandidatePath, publicRead: true, release, signer);
            try { File.Move(CandidatePath, target, overwrite: false); }
            catch (IOException) when (ExistsExactly(target)) { /* Validate the exact raced/existing file; never overwrite it. */ }
        }
        SetFileAcl(target, publicRead: true, release, signer);
        return HoldVerifiedCandidate(target, release, signer);
    }

    public void SnapshotOriginal(string path, string expectedHash)
    {
        EnsureAlive();
        if (ExistsExactly(PreviousPath))
        {
            using var existing = OpenRegularFile(PreviousPath, GenericRead | ReadControl | ReadAttributes, 1);
            ValidateControlledDescriptor(ReadDescriptor(existing), privateScope: true, requireProtected: true);
            if (!HashHandle(existing).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A different predecessor recovery snapshot already exists.");
            return;
        }
        using var parents = AnchorLocalChain(Path.GetDirectoryName(NormalizeLocalPath(path))!);
        using var source = OpenRegularFile(path, GenericRead | ReadControl | ReadAttributes, 1);
        using var input = new FileStream(source, FileAccess.Read, 64 * 1024, isAsync: false);
        var actualHash = Convert.ToHexString(SHA256.HashData(input));
        if (!actualHash.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The predecessor changed before its recovery snapshot was created.");
        input.Position = 0;
        using var security = new NativeSecurity(PrivateFileSddl);
        var attributes = security.Attributes;
        using var snapshot = CreateFileWithSecurity(PreviousPath, GenericWrite | ReadControl | ReadAttributes, 0,
            ref attributes, 1, OpenReparsePoint, IntPtr.Zero);
        if (snapshot.IsInvalid) throw NativeFailure("Could not create the private predecessor recovery snapshot.");
        VerifyRegularFile(snapshot, PreviousPath);
        ValidateControlledDescriptor(ReadDescriptor(snapshot), privateScope: true, requireProtected: true);
        using (var output = new FileStream(snapshot, FileAccess.Write, 64 * 1024, isAsync: false))
        {
            input.CopyTo(output);
            output.Flush(flushToDisk: true);
        }
        if (!HashHandle(snapshot).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The predecessor recovery snapshot failed its SHA-256 verification.");
    }

    public IDisposable ReplaceCurrentAndHold(AppUpdateManifest release, string? signer, string expectedParentHash)
    {
        EnsureAlive();
        var target = TargetPath(release);
        if (!string.Equals(Path.GetFullPath(target), Path.GetFullPath(Path.Combine(SourceDirectory, AppUpdateIdentity.AssetName)), StringComparison.OrdinalIgnoreCase) ||
            !ExistsExactly(target) || ExistsExactly(DisplacedPath) || ExistsExactly(FailedPath))
            throw new IOException("Same-name replacement has an occupied or unexpected recovery path.");
        VerifyCandidate(CandidatePath, release, signer);
        var current = InspectExecutable(target);
        if (!current.Sha256.Equals(expectedParentHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The fixed-name current EXE changed before replacement.");
        try { File.Replace(CandidatePath, target, DisplacedPath, ignoreMetadataErrors: false); }
        catch (IOException) when (!ExistsExactly(target))
        { throw new IOException("Atomic replacement did not leave the current EXE at its fixed path."); }
        var displaced = InspectExecutable(DisplacedPath);
        if (!displaced.Sha256.Equals(expectedParentHash, StringComparison.OrdinalIgnoreCase))
        {
            // Preserve a raced-in target byte-for-byte before refusing to continue.
            if (ExistsExactly(FailedPath))
                throw new IOException("A raced target displaced the original; both recovery copies were retained.");
            File.Replace(DisplacedPath, target, FailedPath, ignoreMetadataErrors: false);
            throw new InvalidDataException("The fixed-name target changed during atomic replacement; its exact prior bytes were restored.");
        }
        SetRecoveryFileAcl(DisplacedPath, expectedParentHash);
        SetFileAcl(target, publicRead: true, release, signer);
        return HoldVerifiedCandidate(target, release, signer);
    }

    public void RestorePredecessor(AppUpdateManifest release, string? signer, string expectedParentHash)
    {
        EnsureAlive();
        var target = TargetPath(release);
        if (!ExistsExactly(PreviousPath) || ExistsExactly(FailedPath))
            throw new IOException("The predecessor snapshot is missing or the failed-image recovery path is occupied.");
        var predecessor = InspectExecutable(PreviousPath);
        if (!predecessor.Sha256.Equals(expectedParentHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The predecessor recovery snapshot no longer matches the original hash.");
        VerifyCandidate(target, release, signer);
        File.Replace(PreviousPath, target, FailedPath, ignoreMetadataErrors: false);
        var restored = InspectExecutable(target);
        if (!restored.Sha256.Equals(expectedParentHash, StringComparison.OrdinalIgnoreCase) ||
            restored.FileVersion != predecessor.FileVersion)
            throw new InvalidDataException("The fixed-name predecessor restore could not be verified.");
        SetArchivedFileAcl(target, expectedParentHash);
        SetFileAcl(FailedPath, publicRead: false, release, signer);
    }

    public void RemovePublishedCandidate(AppUpdateManifest release, string? signer)
    {
        EnsureAlive();
        var target = TargetPath(release);
        VerifyExecutableInfo(InspectExecutable(target), release, signer);
        using var candidate = OpenRegularFile(target, GenericRead | GenericWrite | DeleteAccess | ReadControl | ReadAttributes, 0);
        if (!HashHandle(candidate).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The unconfirmed successor changed before removal.");
        DeleteByHandle(candidate);
    }

    public static IDisposable HoldVerifiedCandidate(string path, AppUpdateManifest release, string? signer) =>
        HoldExecutable(path, info => VerifyExecutableInfo(info, release, signer));

    public static IDisposable HoldOriginal(string path, string expectedHash) => HoldExecutable(path, info =>
    {
        if (!info.Sha256.Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The original executable changed; the new tool was not launched.");
    });

    /// <summary>Archive only the exact verified predecessor; never overwrite OLD.</summary>
    public AppUpdateArchive ArchiveOriginal(string path, string expectedHash, string fileVersion,
        string legacyOriginalName, bool privateSource)
    {
        EnsureAlive();
        var full = NormalizeLocalPath(path);
        var expectedSource = privateSource ? PreviousPath : Path.Combine(SourceDirectory, legacyOriginalName);
        if (!string.Equals(full, Path.GetFullPath(expectedSource), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Only the transaction-bound predecessor source can be archived.");
        return ArchiveOriginalExact(SourceDirectory, path, expectedHash, fileVersion, legacyOriginalName,
            privateSource, verifyExecutable: true);
    }

    internal static AppUpdateArchive ArchiveFixtureOriginal(string parent, string path, string expectedHash, string fileVersion) =>
        ArchiveOriginalExact(ValidateFixtureParent(parent), path, expectedHash, fileVersion,
            Path.GetFileName(path), privateSource: false, verifyExecutable: false);

    internal static string ArchiveName(string fileVersion, string? hash = null)
    {
        if (!TryParseWindowsFileVersion(fileVersion, out fileVersion) ||
            hash is not null && (hash.Length != 64 || !hash.All(Uri.IsHexDigit)))
            throw new InvalidDataException("A verified four-part Windows FileVersion and optional SHA-256 are required for an archive name.");
        return Path.GetFileNameWithoutExtension(AppUpdateIdentity.AssetName) + "-v" + fileVersion +
            (hash is null ? "" : "-" + hash.ToLowerInvariant()) + ".exe";
    }

    private static AppUpdateArchive ArchiveOriginalExact(string parent, string path, string expectedHash,
        string fileVersion, string legacyOriginalName, bool privateSource, bool verifyExecutable)
    {
        var full = NormalizeLocalPath(path);
        if (!Path.GetExtension(full).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            !privateSource && !string.Equals(Path.GetDirectoryName(full), parent, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetPathRoot(full), Path.GetPathRoot(parent), StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(legacyOriginalName) != legacyOriginalName ||
            !Path.GetExtension(legacyOriginalName).Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            expectedHash is not { Length: 64 } || !expectedHash.All(char.IsAsciiHexDigit))
            throw new InvalidDataException("Only the exact ordinary predecessor EXE can be archived.");
        var archiveVersion = ArchiveName(fileVersion);
        if (verifyExecutable)
        {
            var actual = InspectExecutable(full);
            if (!actual.Sha256.Equals(expectedHash, StringComparison.OrdinalIgnoreCase) ||
                actual.FileVersion != fileVersion)
                throw new InvalidDataException("The predecessor's verified bytes or Windows FileVersion changed before archival.");
        }
        using var anchors = AnchorLocalChain(parent);
        var archive = Path.Combine(parent, archiveVersion);
        string status;
        using (var source = OpenRegularFile(full, GenericRead | DeleteAccess | ReadControl | ReadAttributes, 1))
        {
            if (privateSource)
                ValidateControlledDescriptor(ReadDescriptor(source), privateScope: true, requireProtected: true);
            if (!HashHandle(source).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The predecessor changed; its filename was retained.");
            status = TryArchive(source, archive, expectedHash);
            if (status == "occupied")
            {
                archive = Path.Combine(parent, ArchiveName(fileVersion, expectedHash));
                status = TryArchive(source, archive, expectedHash);
            }
        }
        if (status == "occupied")
            throw new IOException("Both deterministic predecessor archive names are occupied by different bytes; recovery files were retained.");
        if (verifyExecutable)
        {
            var actualArchive = InspectExecutable(archive);
            if (!actualArchive.Sha256.Equals(expectedHash, StringComparison.OrdinalIgnoreCase) ||
                actualArchive.FileVersion != fileVersion)
                throw new InvalidDataException("The predecessor archive failed executable identity verification.");
            SetArchivedFileAcl(archive, expectedHash);
        }
        else using (var verifiedArchive = OpenRegularFile(archive, GenericRead | ReadControl | ReadAttributes, 1))
            if (!HashHandle(verifiedArchive).Equals(expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The predecessor archive could not be re-verified.");
        RetireLegacyDuplicates(parent, legacyOriginalName, expectedHash, archive);
        return new(status, archive);
    }

    private static string TryArchive(SafeFileHandle source, string destination, string expectedHash)
    {
        if (ExistsExactly(destination))
        {
            using var existing = OpenRegularFile(destination, GenericRead | ReadControl | ReadAttributes, 1);
            if (!HashHandle(existing).Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) return "occupied";
            if (MatchesHandlePath(source, destination)) return "reused";
            DeleteByHandle(source);
            return "reused";
        }
        var rootOffset = IntPtr.Size == 8 ? 8 : 4;
        var lengthOffset = rootOffset + IntPtr.Size;
        var nameOffset = lengthOffset + 4;
        var name = Encoding.Unicode.GetBytes(destination + '\0');
        var buffer = Marshal.AllocHGlobal(nameOffset + name.Length);
        try
        {
            Marshal.Copy(new byte[nameOffset], 0, buffer, nameOffset);
            Marshal.WriteInt32(buffer, lengthOffset, name.Length - 2);
            Marshal.Copy(name, 0, IntPtr.Add(buffer, nameOffset), name.Length);
            if (RenameFileByHandle(source, 3, buffer, (uint)(nameOffset + name.Length))) return "archived";
            var error = Marshal.GetLastWin32Error();
            if (error is not (80 or 183)) throw NativeFailure("The predecessor could not be archived without overwriting another file.", error);
        }
        finally { Marshal.FreeHGlobal(buffer); }
        using var raced = OpenRegularFile(destination, GenericRead | ReadControl | ReadAttributes, 1);
        if (!HashHandle(raced).Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) return "occupied";
        if (!MatchesHandlePath(source, destination)) DeleteByHandle(source);
        return "reused";
    }

    private static void RetireLegacyDuplicates(string parent, string originalName, string expectedHash, string survivor)
    {
        foreach (var legacy in LegacyArchivePaths(parent, originalName))
        {
            if (!ExistsExactly(legacy) || string.Equals(legacy, survivor, StringComparison.OrdinalIgnoreCase)) continue;
            using var archive = OpenRegularFile(survivor, GenericRead | ReadControl | ReadAttributes, 1);
            if (!HashHandle(archive).Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) continue;
            using var duplicate = OpenRegularFile(legacy, GenericRead | DeleteAccess | ReadControl | ReadAttributes, 1);
            if (HashHandle(duplicate).Equals(expectedHash, StringComparison.OrdinalIgnoreCase)) DeleteByHandle(duplicate);
        }
    }

    internal static IEnumerable<string> LegacyArchivePaths(string parent, string originalName)
    {
        yield return Path.Combine(parent, "OLD-" + originalName);
        for (var slot = 1; slot < 1000; slot++)
            yield return Path.Combine(parent, $"OLD.V{slot}-" + originalName);
    }

    private static string HashHandle(SafeFileHandle handle)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[64 * 1024];
        long offset = 0;
        int read;
        while ((read = RandomAccess.Read(handle, buffer, offset)) != 0)
        {
            hash.AppendData(buffer.AsSpan(0, read));
            offset += read;
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static void DeleteByHandle(SafeFileHandle handle)
    {
        var disposition = new FileDisposition { DeleteFile = true };
        if (!SetFileInformationByHandle(handle, 4, ref disposition, (uint)Marshal.SizeOf<FileDisposition>()))
            throw NativeFailure("A verified duplicate predecessor could not be safely retired.");
    }

    private static IDisposable HoldExecutable(string path, Action<AppExecutableInfo> verify)
    {
        var full = NormalizeLocalPath(path);
        var anchors = AnchorLocalChain(Path.GetDirectoryName(full)!);
        SafeFileHandle? file = null;
        try
        {
            file = OpenRegularFile(full, GenericRead | ReadControl | ReadAttributes, 1);
            verify(InspectExecutable(full));
            return new ExecutableGuard(anchors, file);
        }
        catch { file?.Dispose(); anchors.Dispose(); throw; }
    }

    public void WriteResult<T>(T result)
    {
        EnsureAlive();
        // Outside the portable folder (see ResultPath). Random CreateNew temp name; the
        // final move replaces a planted link itself and never writes through it.
        Directory.CreateDirectory(AppLog.Folder);
        var temporary = Path.Combine(AppLog.Folder, $"{ResultFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(file, result, RequestJson);
            File.Move(temporary, ResultPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    private sealed class ExecutableGuard(DirectoryAnchors parents, SafeFileHandle file) : IDisposable
    { public void Dispose() { file.Dispose(); parents.Dispose(); } }

    public static AppExecutableInfo InspectExecutable(string path)
    {
        var fullPath = NormalizeLocalPath(path);
        // Every parent is anchored before any pathname-based metadata/crypto API.
        // Combined with a no-write/no-delete file handle, these APIs see one file.
        using var parents = AnchorLocalChain(Path.GetDirectoryName(fullPath)!);
        using var handle = OpenRegularFile(fullPath, GenericRead | ReadControl | ReadAttributes, 1);
        using var stream = new FileStream(handle, FileAccess.Read, 64 * 1024, isAsync: false);
        if (stream.Length is <= 0 or > AppUpdateIdentity.MaximumBytes)
            throw new InvalidDataException("The application executable has an invalid byte count.");
        bool signaturePresent;
        using (var pe = new PEReader(stream, PEStreamOptions.LeaveOpen))
        {
            var header = pe.PEHeaders.PEHeader;
            if (header is null || pe.PEHeaders.CoffHeader.Machine != Machine.Amd64 ||
                header.Magic != PEMagic.PE32Plus || header.Subsystem != Subsystem.WindowsGui ||
                (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.ExecutableImage) == 0 ||
                (pe.PEHeaders.CoffHeader.Characteristics & Characteristics.Dll) != 0)
                throw new InvalidDataException("The update must be an AMD64 Windows GUI executable, not a DLL or other platform.");
            var certificate = header.CertificateTableDirectory;
            signaturePresent = certificate.Size != 0 || certificate.RelativeVirtualAddress != 0;
            if (signaturePresent && (certificate.Size <= 0 || certificate.RelativeVirtualAddress <= 0 ||
                (long)certificate.RelativeVirtualAddress + certificate.Size > stream.Length))
                throw new InvalidDataException("The application executable has an invalid signature table.");
        }
        var metadata = FileVersionInfo.GetVersionInfo(fullPath);
        if (metadata.ProductName != AppUpdateIdentity.Product || metadata.CompanyName != "PcNinja" ||
            !ReleaseVersion.TryParse(metadata.ProductVersion, out var version) ||
            !TryParseWindowsFileVersion(metadata.FileVersion, out var fileVersion))
            throw new InvalidDataException("The executable does not have the fixed PcNinja product/company and a valid release version.");
        stream.Position = 0;
        var hash = Convert.ToHexString(SHA256.HashData(stream));
        stream.Position = 0;
        var signer = VerifyAuthenticode(fullPath, handle, signaturePresent);
        // Validate the complete informational version; only build metadata is ignored.
        return new(version!.Value.Split('+')[0], fileVersion, hash, stream.Length, signer);
    }

    public static bool TryParseWindowsFileVersion(string? value, out string fileVersion)
    {
        fileVersion = "";
        if (string.IsNullOrEmpty(value) || !Version.TryParse(value, out var parsed) || parsed.Revision < 0 ||
            parsed.ToString(4) != value || parsed.Major > 65534 || parsed.Minor > 65534 ||
            parsed.Build > 65534 || parsed.Revision > 65534) return false;
        fileVersion = parsed.ToString(4);
        return true;
    }

    public static AppExecutableInfo VerifyCandidate(string path, AppUpdateManifest release, string? requiredSigner)
    {
        AppUpdateClient.ValidateManifest(release);
        var actual = InspectExecutable(path);
        VerifyExecutableInfo(actual, release, requiredSigner);
        return actual;
    }

    internal static void VerifyExecutableInfo(AppExecutableInfo actual, AppUpdateManifest release, string? requiredSigner)
    {
        AppUpdateClient.ValidateManifest(release);
        if (!ReleaseVersion.TryParse(actual.Version, out var actualVersion) ||
            !string.Equals(actualVersion!.Value.Split('+')[0], release.Version.Split('+')[0], StringComparison.Ordinal) ||
            actual.SizeBytes != release.SizeBytes || !actual.Sha256.Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The candidate executable does not match the exact release version, size and SHA-256.");
        if (requiredSigner is not null && (requiredSigner.Length != 40 || !requiredSigner.All(Uri.IsHexDigit) ||
            actual.SignerThumbprint is null || !requiredSigner.Equals(actual.SignerThumbprint, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("The update does not preserve the installed application's trusted signing certificate.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _anchors.Dispose();
    }

    internal static string NormalizeOperationId(string operationId) => operationId is { Length: 32 } &&
        operationId.All(char.IsAsciiHexDigit) && Guid.TryParseExact(operationId, "N", out var id) && id != Guid.Empty
            ? id.ToString("N") : throw new ArgumentException("An update operation must be an exact 32-hex GUID.", nameof(operationId));

    internal static long RequiredFreeBytes(long candidateBytes, long helperBytes)
    {
        if (candidateBytes is <= 0 or > AppUpdateIdentity.MaximumBytes || helperBytes is <= 0 or > AppUpdateIdentity.MaximumBytes)
            throw new ArgumentOutOfRangeException(nameof(candidateBytes), "Application candidate and helper byte counts are outside their limits.");
        return checked(candidateBytes + helperBytes + SpaceMarginBytes);
    }

    internal static void ValidateControlledDescriptor(RawSecurityDescriptor descriptor, bool privateScope, bool requireProtected)
    {
        if (!IsAdministratorOrSystem(descriptor.Owner) || descriptor.DiscretionaryAcl is null ||
            (descriptor.ControlFlags & ControlFlags.DiscretionaryAclPresent) == 0 ||
            requireProtected && (descriptor.ControlFlags & ControlFlags.DiscretionaryAclProtected) == 0)
            throw new UnauthorizedAccessException("An existing update folder/file is not owned and protected by Administrators/SYSTEM. It was not changed.");
        var administrators = false;
        var system = false;
        foreach (GenericAce entry in descriptor.DiscretionaryAcl)
        {
            if (entry is not CommonAce ace || ace.AceQualifier != AceQualifier.AccessAllowed || ace.IsCallback ||
                (ace.AceFlags & AceFlags.InheritOnly) != 0)
                throw new UnauthorizedAccessException("The update ACL is not an explicit controlled allowlist.");
            if (IsAdministratorOrSystem(ace.SecurityIdentifier))
            {
                if ((uint)ace.AccessMask != 0x001f01ff) throw new UnauthorizedAccessException("The update ACL lacks its exact administrator/system full-control rule.");
                administrators |= ace.SecurityIdentifier.Value == "S-1-5-32-544";
                system |= ace.SecurityIdentifier.Value == "S-1-5-18";
            }
            else if (privateScope || ace.SecurityIdentifier.Value != "S-1-5-32-545" ||
                ((uint)ace.AccessMask & ~0x001200a9U) != 0)
                throw new UnauthorizedAccessException("Non-administrator access to update staging is not allowed.");
        }
        if (!administrators || !system) throw new UnauthorizedAccessException("The update ACL is missing Administrators or SYSTEM.");
    }

    private static bool IsAdministratorOrSystem(SecurityIdentifier? sid) => sid?.Value is "S-1-5-32-544" or "S-1-5-18";

    private static SafeFileHandle OpenControlledDirectory(string path, bool privateScope, bool create, bool requireNew = false)
    {
        if (create)
        {
            using var security = new NativeSecurity(privateScope ? PrivateDirectorySddl : PublicDirectorySddl);
            var attributes = security.Attributes;
            if (!CreateDirectoryW(path, ref attributes))
            {
                var error = Marshal.GetLastWin32Error();
                if (error != 183 || requireNew) throw NativeFailure("The protected update folder could not be created atomically.", error);
            }
        }
        var handle = OpenDirectory(path);
        try { ValidateControlledDescriptor(ReadDescriptor(handle), privateScope, requireProtected: true); return handle; }
        catch { handle.Dispose(); throw; }
    }

    private static DirectoryAnchors AnchorLocalChain(string path)
    {
        path = NormalizeLocalPath(path);
        var root = Path.GetPathRoot(path)!;
        var drive = new DriveInfo(root);
        if (drive.DriveType != DriveType.Fixed || !drive.IsReady || !drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Download and run needs a writable ordinary fixed local NTFS folder beside this tool. Copy the tool off network/ISO/removable/reparse media first; no alternate folder is chosen.");
        var anchors = new DirectoryAnchors();
        try
        {
            anchors.Add(OpenDirectory(root));
            var current = root;
            foreach (var segment in Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar))
            {
                if (segment == ".") continue;
                current = Path.Combine(current, segment);
                anchors.Add(OpenDirectory(current));
            }
            return anchors;
        }
        catch { anchors.Dispose(); throw; }
    }

    private static SafeFileHandle OpenDirectory(string path)
    {
        var handle = CreateFileW(path, ReadControl | ReadAttributes, 3, IntPtr.Zero, 3,
            BackupSemantics | OpenReparsePoint, IntPtr.Zero); // share read/write, never delete.
        if (handle.IsInvalid) { handle.Dispose(); throw NativeFailure("An application-update parent directory could not be anchored."); }
        try
        {
            if (!GetFileInformationByHandle(handle, out var info) ||
                (info.Attributes & (uint)FileAttributes.Directory) == 0 ||
                (info.Attributes & (uint)FileAttributes.ReparsePoint) != 0 || !MatchesHandlePath(handle, path))
                throw new IOException("Application-update paths must not contain directory reparse points or changed ancestors.");
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    private static SafeFileHandle OpenRegularFile(string path, uint access, uint share)
    {
        var handle = CreateFileW(path, access, share, IntPtr.Zero, 3, OpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid) { handle.Dispose(); throw NativeFailure("The exact update file could not be locked. Running, read-only or unavailable files are not replaced."); }
        try { VerifyRegularFile(handle, path); return handle; }
        catch { handle.Dispose(); throw; }
    }

    private static void VerifyRegularFile(SafeFileHandle handle, string path)
    {
        if (!GetFileInformationByHandle(handle, out var info) || info.Links != 1 ||
            (info.Attributes & (uint)(FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0 || !MatchesHandlePath(handle, path))
            throw new IOException("Update files must be ordinary single-link files at their exact anchored paths.");
    }

    private static bool MatchesHandlePath(SafeFileHandle handle, string path)
    {
        var buffer = new StringBuilder(32768);
        var length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Capacity, 0);
        return length > 0 && length < buffer.Capacity && string.Equals(Path.TrimEndingDirectorySeparator(buffer.ToString()),
            Path.TrimEndingDirectorySeparator(@"\\?\" + Path.GetFullPath(path)), StringComparison.OrdinalIgnoreCase);
    }

    private static RawSecurityDescriptor ReadDescriptor(SafeFileHandle handle)
    {
        var result = GetSecurityInfo(handle, 1, OwnerAndDacl, out _, out _, out _, out _, out var descriptor);
        if (result != 0) throw NativeFailure("The anchored update object's ACL could not be inspected.", (int)result);
        try
        {
            var length = GetSecurityDescriptorLength(descriptor);
            if (length is 0 or > 65536) throw new IOException("The update object has an unsupported security descriptor.");
            var bytes = new byte[(int)length];
            Marshal.Copy(descriptor, bytes, 0, bytes.Length);
            return new RawSecurityDescriptor(bytes, 0);
        }
        finally { LocalFree(descriptor); }
    }

    private static string NormalizeLocalPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path != path.Trim() || path.Length < 3 ||
            !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' ||
            path.AsSpan(2).Contains(':') || path.IndexOfAny(['\0', '*', '?']) >= 0)
            throw new IOException("Application-update filesystem operations require ordinary absolute local drive paths, not network/device/relative/stream paths.");
        return Path.GetFullPath(path);
    }

    private static string ValidateFixtureParent(string path)
    {
        var full = NormalizeLocalPath(path);
        const string prefix = "PcNinja-AppUpdate-Fixture-";
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(full));
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || !Guid.TryParseExact(name[prefix.Length..], "N", out _) ||
            !string.Equals(Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(full)!),
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("An updater fixture must be one uniquely named child of the current temporary directory.");
        return full;
    }

    private static bool ExistsExactly(string path)
    {
        try { File.GetAttributes(path); return true; }
        catch (FileNotFoundException) { return false; }
        catch (DirectoryNotFoundException) { return false; }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("The private update request contains duplicate properties.");
                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) RejectDuplicateProperties(item);
    }

    private void EnsureAlive() => ObjectDisposedException.ThrowIf(_disposed, this);
    private static IOException NativeFailure(string message, int? code = null) => new(message,
        new Win32Exception(code ?? Marshal.GetLastWin32Error()));

    private static string? VerifyAuthenticode(string path, SafeFileHandle handle, bool signaturePresent)
    {
        var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path, Handle = handle.DangerousGetHandle() };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        try
        {
            Marshal.StructureToPtr(file, pointer, false);
            var trust = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), Ui = 2, Union = 1,
                File = pointer, StateAction = 1, ProviderFlags = 0x1080 }; // cached-only revocation, no network/UI.
            try
            {
                var status = WinVerifyTrust(new IntPtr(-1), ref action, ref trust);
                if (status == unchecked((int)0x800b0100) && !signaturePresent) return null; // Explicit unsigned policy.
                if (status != 0) throw new InvalidDataException($"The executable has an invalid/untrusted Authenticode signature (0x{unchecked((uint)status):X8}).");
                using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
                return certificate.Thumbprint.ToUpperInvariant();
            }
            finally { trust.StateAction = 2; WinVerifyTrust(new IntPtr(-1), ref action, ref trust); }
        }
        finally { Marshal.DestroyStructure<TrustFile>(pointer); Marshal.FreeHGlobal(pointer); }
    }

    private sealed class DirectoryAnchors : IDisposable
    {
        private readonly List<SafeFileHandle> _handles = [];
        internal SafeFileHandle Last => _handles[^1];
        internal void Add(SafeFileHandle handle) => _handles.Add(handle);
        public void Dispose() { for (var i = _handles.Count - 1; i >= 0; i--) _handles[i].Dispose(); _handles.Clear(); }
    }

    private sealed class NativeSecurity : IDisposable
    {
        internal IntPtr Pointer { get; private set; }
        internal SecurityAttributes Attributes => new() { Length = Marshal.SizeOf<SecurityAttributes>(), Descriptor = Pointer };
        internal NativeSecurity(string sddl)
        {
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl, 1, out var pointer, out _))
                throw NativeFailure("Could not prepare the protected application-update ACL.");
            Pointer = pointer;
        }
        public void Dispose() { if (Pointer != IntPtr.Zero) { LocalFree(Pointer); Pointer = IntPtr.Zero; } }
    }

    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int Length; public IntPtr Descriptor; [MarshalAs(UnmanagedType.Bool)] public bool Inherit; }
    [StructLayout(LayoutKind.Sequential)] private struct FileDisposition { [MarshalAs(UnmanagedType.U1)] public bool DeleteFile; }
    [StructLayout(LayoutKind.Sequential)] private struct FileInformation
    {
        public uint Attributes; public FILETIME Created; public FILETIME Accessed; public FILETIME Written;
        public uint VolumeSerial; public uint SizeHigh; public uint SizeLow; public uint Links; public uint IndexHigh; public uint IndexLow;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct TrustFile
    {
        public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle; public IntPtr KnownSubject;
    }
    [StructLayout(LayoutKind.Sequential)] private struct TrustData
    {
        public uint Size; public IntPtr Callback; public IntPtr Sip; public uint Ui; public uint Revocation;
        public uint Union; public IntPtr File; public uint StateAction; public IntPtr State;
        public IntPtr Url; public uint ProviderFlags; public uint UiContext;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern SafeFileHandle CreateFileWithSecurity(string path, uint access, uint share, ref SecurityAttributes security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateDirectoryW(string path, ref SecurityAttributes security);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint length, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool SetFileInformationByHandle(SafeFileHandle file, int informationClass, ref FileDisposition information, uint size);
    [DllImport("kernel32.dll", EntryPoint = "SetFileInformationByHandle", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool RenameFileByHandle(SafeFileHandle file, int informationClass, IntPtr information, uint size);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string sddl, uint revision, out IntPtr descriptor, out uint size);
    [DllImport("advapi32.dll")] private static extern uint GetSecurityInfo(SafeFileHandle file, int type, uint information, out IntPtr owner, out IntPtr group, out IntPtr dacl, out IntPtr sacl, out IntPtr descriptor);
    [DllImport("advapi32.dll")] private static extern uint GetSecurityDescriptorLength(IntPtr descriptor);
    [DllImport("advapi32.dll")] private static extern uint SetSecurityInfo(SafeFileHandle file, int type, uint information, IntPtr owner, IntPtr group, IntPtr dacl, IntPtr sacl);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSecurityDescriptorOwner(IntPtr descriptor, out IntPtr owner, [MarshalAs(UnmanagedType.Bool)] out bool ownerDefaulted);
    [DllImport("advapi32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSecurityDescriptorDacl(IntPtr descriptor, [MarshalAs(UnmanagedType.Bool)] out bool present, out IntPtr dacl, [MarshalAs(UnmanagedType.Bool)] out bool defaulted);
    [DllImport("wintrust.dll", ExactSpelling = true)] private static extern int WinVerifyTrust(IntPtr window, ref Guid action, ref TrustData data);
}
