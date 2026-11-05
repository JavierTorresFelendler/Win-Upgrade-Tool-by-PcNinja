using System.Diagnostics;
using System.Runtime.InteropServices;
using PcNinja.Shared;

// Deterministic checks for shared/PcNinjaSingleInstance.cs using real per-session
// kernel objects. Owner and requester run in one process: requesters disable
// nested-owner detection, and window activation is simulated through
// PcNinjaSingleInstanceOptions.ActivateWindow. Real window focus, UAC and
// cross-integrity launches still need the Windows VM checklist.
internal static class SingleInstanceChecks
{
    private static readonly List<string> Diagnostics = new();

    internal static void Run()
    {
        PcNinjaSingleInstance.DiagnosticSink = line => { lock (Diagnostics) Diagnostics.Add(line); };
        try
        {
            OwnerProbeAndRelease();
            StartupRaceIsRetriedNotLost();
            OwnerWithoutWindowReportsNotResponding();
            ClosingOwnerHandsOverToRequester();
            RapidConcurrentLaunchesAreAllAcknowledged();
            InaccessibleObjectFailsOpen();
            ConflictingObjectTypeFailsOpen();
            LegacyOwnerWithoutAcknowledgementIsSignalled();
            DefaultSecurityFallbackStillWorks();
            NestedEntryDoesNotReleaseOwner();
        }
        finally
        {
            PcNinjaSingleInstance.DiagnosticSink = null;
        }

        Console.WriteLine("PASS: Single-instance guard (startup race, closing hand-over, not-responding, " +
            "rapid launches, inaccessible/conflicting objects fail open, legacy owner, default security, nesting).");
    }

    private static string NewId() => "SmokeSI" + Guid.NewGuid().ToString("N")[..12];

    private static PcNinjaSingleInstanceOptions Owner(Func<bool> activate, int retryMs = 2000) => new()
    {
        ActivateWindow = activate,
        OwnerActivationRetryMilliseconds = retryMs,
        PollIntervalMilliseconds = 20
    };

    private static PcNinjaSingleInstanceOptions Requester(int timeoutMs = 4000) => new()
    {
        AllowNestedOwner = false, // Same process as the owner in these fixtures.
        AcknowledgementTimeoutMilliseconds = timeoutMs,
        ResignalIntervalMilliseconds = 200,
        PollIntervalMilliseconds = 20
    };

    private static void OwnerProbeAndRelease()
    {
        var id = NewId();
        Expect(!PcNinjaSingleInstance.TryActivateExisting(id, Requester(), out var probe) &&
               probe == PcNinjaSingleInstanceOutcome.NoInstance, $"Probe without an owner returned {probe}.");

        var owner = PcNinjaSingleInstance.Acquire(id, Owner(() => true), out var outcome);
        Expect(owner is not null && outcome == PcNinjaSingleInstanceOutcome.Owner && owner.IsGuarded, $"First Acquire returned {outcome}.");

        var second = PcNinjaSingleInstance.Acquire(id, Requester(), out var secondOutcome);
        Expect(second is null && secondOutcome == PcNinjaSingleInstanceOutcome.ActivatedExisting,
            $"Second Acquire returned {secondOutcome}.");
        Expect(PcNinjaSingleInstance.TryActivateExisting(id, Requester(), out probe) &&
               probe == PcNinjaSingleInstanceOutcome.ActivatedExisting, $"Probe with an owner returned {probe}.");
        Expect(owner!.ActivationCount == 2, $"Owner served {owner.ActivationCount} activations, expected 2.");

        owner.Dispose();
        Expect(Environment.GetEnvironmentVariable(PcNinjaSingleInstance.HeldEnvironmentName) is null,
            "Disposing the owner did not clear the nested-owner marker.");
        using var next = PcNinjaSingleInstance.Acquire(id, Owner(() => true), out outcome);
        Expect(next is not null && outcome == PcNinjaSingleInstanceOutcome.Owner, $"Acquire after release returned {outcome}.");
    }

    // Review finding 1: the request arrives before the owner's window exists.
    private static void StartupRaceIsRetriedNotLost()
    {
        var id = NewId();
        var calls = 0;
        // No window for the first 10 attempts (~200 ms), then the GUI is ready.
        using var owner = PcNinjaSingleInstance.Acquire(id, Owner(() => Interlocked.Increment(ref calls) > 10), out _)!;
        var second = PcNinjaSingleInstance.Acquire(id, Requester(), out var outcome);
        Expect(second is null && outcome == PcNinjaSingleInstanceOutcome.ActivatedExisting,
            $"Startup-race requester returned {outcome}.");
        Expect(Volatile.Read(ref calls) >= 11 && owner.ActivationCount == 1,
            $"Owner did not retry until its window existed (calls {calls}, served {owner.ActivationCount}).");
    }

    private static void OwnerWithoutWindowReportsNotResponding()
    {
        var id = NewId();
        using var owner = PcNinjaSingleInstance.Acquire(id, Owner(() => false, retryMs: 300), out _)!;
        var watch = Stopwatch.StartNew();
        var second = PcNinjaSingleInstance.Acquire(id, Requester(timeoutMs: 800), out var outcome);
        Expect(second is null && outcome == PcNinjaSingleInstanceOutcome.ExistingNotResponding,
            $"Unacknowledged request returned {outcome}.");
        Expect(watch.ElapsedMilliseconds is >= 700 and < 5000, $"Not-responding wait was {watch.ElapsedMilliseconds} ms.");
        Expect(owner.ActivationCount == 0, "An unserved request was counted as activated.");
        Expect(!PcNinjaSingleInstance.TryActivateExisting(id, Requester(timeoutMs: 500), out var probe) &&
               probe == PcNinjaSingleInstanceOutcome.ExistingNotResponding, $"Probe of a hung owner returned {probe}.");
        ExpectDiagnostic("did not acknowledge");
    }

    // The owner's window is already gone (closing) when the request arrives.
    private static void ClosingOwnerHandsOverToRequester()
    {
        var id = NewId();
        var owner = PcNinjaSingleInstance.Acquire(id, Owner(() => false, retryMs: 5000), out _)!;
        var requester = Task.Run(() =>
        {
            var holder = PcNinjaSingleInstance.Acquire(id, Requester(timeoutMs: 6000), out var outcome);
            return (holder, outcome);
        });
        Thread.Sleep(300);
        Expect(!requester.IsCompleted, "Requester finished before the owner exited.");
        owner.Dispose();
        Expect(requester.Wait(5000), "Requester did not take over after the owner exited.");
        var (taken, takenOutcome) = requester.Result;
        Expect(taken is not null && takenOutcome == PcNinjaSingleInstanceOutcome.Owner && taken.IsGuarded,
            $"Hand-over returned {takenOutcome}.");

        // The new owner must itself be reachable.
        using (taken)
        {
            var probeOwner = PcNinjaSingleInstance.Acquire(id, Requester(timeoutMs: 1000), out var probe);
            Expect(probeOwner is null && probe != PcNinjaSingleInstanceOutcome.Owner, $"New owner was not detected ({probe}).");
        }

        ExpectDiagnostic("takes over");
    }

    private static void RapidConcurrentLaunchesAreAllAcknowledged()
    {
        var id = NewId();
        using var owner = PcNinjaSingleInstance.Acquire(id, Owner(() => { Thread.Sleep(15); return true; }), out _)!;
        var requesters = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            var holder = PcNinjaSingleInstance.Acquire(id, Requester(), out var outcome);
            return holder is null ? outcome : PcNinjaSingleInstanceOutcome.Owner;
        })).ToArray();
        Expect(Task.WaitAll(requesters, 10000), "Concurrent requesters did not finish.");
        var outcomes = requesters.Select(task => task.Result).ToArray();
        Expect(outcomes.All(outcome => outcome == PcNinjaSingleInstanceOutcome.ActivatedExisting),
            "Concurrent launches: " + string.Join(", ", outcomes));
        Expect(owner.ActivationCount >= 1, "Concurrent launches were not served.");
    }

    // Review finding 2: the object exists but cannot be opened (name squatting,
    // other account, hostile DACL). The guard must fail open, not exit silently.
    private static void InaccessibleObjectFailsOpen()
    {
        var id = NewId();
        using var blocker = DenyAllEvent.Create(PcNinjaSingleInstance.GetEventName(id));
        Expect(!PcNinjaSingleInstance.TryActivateExisting(id, Requester(), out var probe) &&
               probe == PcNinjaSingleInstanceOutcome.GuardUnavailable, $"Probe of an inaccessible object returned {probe}.");
        using var holder = PcNinjaSingleInstance.Acquire(id, Requester(), out var outcome);
        Expect(holder is not null && outcome == PcNinjaSingleInstanceOutcome.GuardUnavailable && !holder.IsGuarded,
            $"Inaccessible object returned {outcome}; the app must start (fail open).");
        ExpectDiagnostic("inaccessible");
    }

    private static void ConflictingObjectTypeFailsOpen()
    {
        var id = NewId();
        using var mutex = new Mutex(false, PcNinjaSingleInstance.GetEventName(id));
        Expect(!PcNinjaSingleInstance.TryActivateExisting(id, Requester(), out var probe) &&
               probe == PcNinjaSingleInstanceOutcome.GuardUnavailable, $"Probe of a conflicting object returned {probe}.");
        using var holder = PcNinjaSingleInstance.Acquire(id, Requester(), out var outcome);
        Expect(holder is not null && outcome == PcNinjaSingleInstanceOutcome.GuardUnavailable && !holder.IsGuarded,
            $"Conflicting object type returned {outcome}; the app must start (fail open).");
    }

    // An owner built from the first PR revision has no acknowledgement event.
    private static void LegacyOwnerWithoutAcknowledgementIsSignalled()
    {
        var id = NewId();
        using var legacy = new EventWaitHandle(false, EventResetMode.AutoReset, PcNinjaSingleInstance.GetEventName(id));
        var watch = Stopwatch.StartNew();
        var holder = PcNinjaSingleInstance.Acquire(id, Requester(), out var outcome);
        Expect(holder is null && outcome == PcNinjaSingleInstanceOutcome.ActivatedExisting && watch.ElapsedMilliseconds < 2000,
            $"Legacy owner returned {outcome} after {watch.ElapsedMilliseconds} ms.");
        Expect(legacy.WaitOne(0), "Legacy owner was not signalled.");
    }

    private static void DefaultSecurityFallbackStillWorks()
    {
        var id = NewId();
        var options = Owner(() => true);
        options.ForceDefaultSecurity = true;
        using var owner = PcNinjaSingleInstance.Acquire(id, options, out var outcome)!;
        Expect(outcome == PcNinjaSingleInstanceOutcome.Owner && owner.IsGuarded, $"Default-security owner returned {outcome}.");
        var requester = Requester();
        requester.ForceDefaultSecurity = true;
        Expect(PcNinjaSingleInstance.Acquire(id, requester, out outcome) is null &&
               outcome == PcNinjaSingleInstanceOutcome.ActivatedExisting, $"Default-security requester returned {outcome}.");
        ExpectDiagnostic("default security");
    }

    private static void NestedEntryDoesNotReleaseOwner()
    {
        var id = NewId();
        using var owner = PcNinjaSingleInstance.Acquire(id, Owner(() => true), out _)!;
        using (var nested = PcNinjaSingleInstance.Acquire(id, null, out var outcome))
            Expect(nested is not null && outcome == PcNinjaSingleInstanceOutcome.NestedOwner && !nested.IsGuarded,
                $"Nested entry returned {outcome}.");
        Expect(PcNinjaSingleInstance.Acquire(id, Requester(), out var after) is null &&
               after == PcNinjaSingleInstanceOutcome.ActivatedExisting, $"Owner was released by a nested holder ({after}).");
    }

    private static void ExpectDiagnostic(string fragment)
    {
        lock (Diagnostics)
            Expect(Diagnostics.Any(line => line.Contains(fragment, StringComparison.OrdinalIgnoreCase)),
                $"Missing diagnostic containing '{fragment}'.");
    }

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception("Single-instance: " + message);
    }

    private sealed class DenyAllEvent : IDisposable
    {
        private readonly IntPtr handle;
        private DenyAllEvent(IntPtr handle) => this.handle = handle;

        internal static DenyAllEvent Create(string name)
        {
            // Empty DACL: nobody, including this account, may open the object.
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW("D:", 1, out var descriptor, out _))
                throw new Exception("Deny-all security descriptor could not be built.");
            try
            {
                var attributes = new SecurityAttributes
                {
                    Length = Marshal.SizeOf<SecurityAttributes>(),
                    Descriptor = descriptor
                };
                var created = CreateEventW(ref attributes, false, false, name);
                if (created == IntPtr.Zero) throw new Exception($"Deny-all event failed: {Marshal.GetLastWin32Error()}.");
                return new DenyAllEvent(created);
            }
            finally
            {
                LocalFree(descriptor);
            }
        }

        public void Dispose() => CloseHandle(handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct SecurityAttributes
        {
            public int Length;
            public IntPtr Descriptor;
            public bool Inherit;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateEventW(ref SecurityAttributes attributes, bool manualReset, bool initialState, string name);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptorW(string sddl, uint revision, out IntPtr descriptor, out uint size);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr memory);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
