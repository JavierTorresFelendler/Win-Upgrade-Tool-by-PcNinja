using System.Reflection;
using System.Text.Json;
using PcNinja.Win11Upgrade.Core;

var assembly = Assembly.Load("Windows-Upgrade-Assistant-By-PcNinja");
var runner = assembly.GetType("PcNinja.Win11UpgradeAssistant.CliRunner", true)!;
var options = assembly.GetType("PcNinja.Win11UpgradeAssistant.CliOptions", true)!;
object Parse(params string[] args) => options.GetMethod("Parse")!.Invoke(null, [args])!;
var report = runner.GetMethod("ReportSetupResult", BindingFlags.NonPublic | BindingFlags.Static)!;
var image = new InstallationImage(6, "Fixture Pro", "Professional", "x64", "10.0.26300", "Client", "en-US");
var source = new MountedSource("X:\\", "unused-setup.exe", "X:\\sources\\install.wim", [image]);

// Exercise the actual result presenter with fixtures, never engine/Setup launch.
foreach (var quiet in new[] { false, true })
foreach (var noRestart in new[] { false, true })
foreach (var setupCode in new[] { 0, 0x40010004, unchecked((int)0xC190020E), unchecked((int)0xC0000005) })
{
    var arguments = new List<string> { "upgrade", "--mounted", "X:\\", "--yes", "--json" };
    if (quiet) arguments.Add("--quiet");
    if (noRestart) arguments.Add("--no-restart");
    var (exitCode, json, _) = Capture(() => (int)report.Invoke(null,
        [new UpgradeResult(setupCode, "unused-setup.exe"), Parse(arguments.ToArray()), source, image, null])!);
    using var document = JsonDocument.Parse(json);
    var root = document.RootElement;
    var expectedExit = setupCode == 0 ? 0 : setupCode == 0x40010004 ? 8 : 7;
    var expectedReady = setupCode == 0 && noRestart;
    if (exitCode != expectedExit || root.GetProperty("exitCode").GetInt32() != expectedExit ||
        root.GetProperty("schemaVersion").GetInt32() != 1 ||
        root.GetProperty("readyForRestart").GetBoolean() != expectedReady ||
        root.GetProperty("success").GetBoolean() != (setupCode == 0) ||
        root.GetProperty("setupExitCode").GetInt32() != setupCode ||
        root.GetProperty("setupExitCodeHex").GetString() != $"0x{unchecked((uint)setupCode):X8}")
        throw new Exception("CLI result lost native Setup evidence or incorrectly authorized restart.");
    var expectedResult = expectedExit == 8 ? "SETUP_OUTCOME_UNCONFIRMED" : expectedExit == 7 ? "SETUP_FAILED" :
        noRestart ? "UPGRADE_PREPARED_RESTART_REQUIRED" : "UPGRADE_PREPARED_AUTOMATIC_RESTART";
    if (root.GetProperty("resultCode").GetString() != expectedResult)
        throw new Exception("CLI result identifier was ambiguous.");
    if (setupCode == 0 && (root.GetProperty("quiet").GetBoolean() != quiet ||
        root.GetProperty("automaticRestart").GetBoolean() != !noRestart ||
        root.GetProperty("manualRestartRequired").GetBoolean() != noRestart))
        throw new Exception("CLI result lost independent quiet/restart intent.");
}

// Every prelaunch/error group keeps its established exit code and cannot reboot.
var fail = runner.GetMethod("Fail", BindingFlags.NonPublic | BindingFlags.Static)!;
foreach (var failureCode in Enumerable.Range(1, 8))
{
    var failed = Capture(() => (int)fail.Invoke(null, [failureCode, "fixture failure", true, null])!);
    using var document = JsonDocument.Parse(failed.Output);
    if (failed.Code != failureCode || document.RootElement.GetProperty("readyForRestart").GetBoolean() ||
        document.RootElement.GetProperty("success").GetBoolean() ||
        document.RootElement.GetProperty("setupExitCode").ValueKind != JsonValueKind.Null)
        throw new Exception("An error group lost its exit code or invented Setup/restart evidence.");
}

foreach (var command in new[] { "status", "validate" })
{
    try { Parse(command, "--quiet"); throw new Exception("Quiet accepted for a non-upgrade command."); }
    catch (TargetInvocationException ex) when (ex.InnerException?.GetType().Name == "CliArgumentException") { }
}
var run = runner.GetMethod("RunAsync")!;
var cancelled = Capture(() => ((Task<int>)run.Invoke(null,
    [new[] { "upgrade", "--quiet", "--mounted", "X:\\", "--json" }])!).GetAwaiter().GetResult());
using (var document = JsonDocument.Parse(cancelled.Output))
    if (cancelled.Code != 6 || document.RootElement.GetProperty("readyForRestart").GetBoolean() ||
        document.RootElement.GetProperty("resultCode").GetString() != "CANCELLED")
        throw new Exception("Quiet without explicit consent did not stop before transfer/Setup.");

// A caller must not mistake a successful status/validation/dry run for preparation.
var envelope = assembly.GetType("PcNinja.Win11UpgradeAssistant.CliAutomationResult", true)!;
foreach (var code in new[] { "STATUS_REPORTED", "VALIDATION_PASSED", "DRY_RUN_COMPLETED", "SETUP_FAILED" })
{
    var payload = (Dictionary<string, object?>)envelope.GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static)!
        .Invoke(null, [new { valid = true }, code, 0, "fixture", true])!;
    if (!Equals(payload["readyForRestart"], false)) throw new Exception("Non-upgrade result authorized restart.");
}
Parse("upgrade", "--mounted", "X:\\", "--quiet", "--no-restart", "--dry-run", "--json");
Console.WriteLine("PASS: Headless CLI result codes/JSON/native HRESULT, quiet/restart matrix, fail-closed restart authorization and explicit consent. No media access, GUI or Setup.");

static (int Code, string Output, string Error) Capture(Func<int> action)
{
    var output = Console.Out;
    var error = Console.Error;
    using var capture = new StringWriter();
    using var errors = new StringWriter();
    try
    {
        Console.SetOut(capture);
        Console.SetError(errors);
        var code = action();
        return (code, capture.ToString(), errors.ToString());
    }
    finally { Console.SetOut(output); Console.SetError(error); }
}
