using PcNinja.Win11UpgradeAssistant;

internal static class AppUpdateReadinessChecks
{
    internal static async Task RunAsync()
    {
        foreach (var invalid in new[] { "", "arbitrary", "PcNinja.AppReady.../file", AppUpdateGuiReadiness.Prefix + new string('z', 32) })
            if (AppUpdateGuiReadiness.IsValidName(invalid)) throw new Exception("Unsafe readiness channel name accepted.");
        foreach (var matchPid in Enumerable.Repeat(true, 10).Append(false))
        {
            var name = AppUpdateGuiReadiness.Prefix + Guid.NewGuid().ToString("N");
            using var server = AppUpdateGuiReadiness.CreateServer(name);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var wait = AppUpdateGuiReadiness.WaitAsync(server, Environment.ProcessId + (matchPid ? 0 : 10000), timeout.Token);
            var signal = AppUpdateGuiReadiness.SignalAsync(name);
            if (await wait != matchPid) throw new Exception("Readiness did not bind the ACK to the exact launched process.");
            if (!matchPid) server.Dispose();
            await signal;
        }
        using (var absent = AppUpdateGuiReadiness.CreateServer(AppUpdateGuiReadiness.Prefix + Guid.NewGuid().ToString("N")))
        using (var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(100)))
            if (await AppUpdateGuiReadiness.WaitAsync(absent, Environment.ProcessId, timeout.Token))
                throw new Exception("Missing GUI acknowledgement was treated as ready.");
        Console.WriteLine("PASS: Same-user random bounded readiness channel, exact native client PID, absent/wrong-client ACK refusal. In-process pipe fixtures only; no GUI/helper/EXE launched.");
    }
}
