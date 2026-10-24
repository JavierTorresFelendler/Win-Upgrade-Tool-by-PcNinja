using System.Diagnostics;
using System.Reflection;
using PcNinja.Win11Upgrade.Core;

internal static class DownloadGuidanceChecks
{
    internal static void Run()
    {
        var priorContext = SynchronizationContext.Current;
        Form? form = null;
        try
        {
            var assembly = Assembly.Load("Windows-Upgrade-Assistant-By-PcNinja");
            var type = assembly.GetType("PcNinja.Win11UpgradeAssistant.MainForm", throwOnError: true)!;
            form = (Form)Activator.CreateInstance(type, nonPublic: true)!;
            var context = new FormPumpContext(form);
            SynchronizationContext.SetSynchronizationContext(context);
            T Field<T>(string name) => (T)type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
            void Set(string name, object? value) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(form, value);
            object? Call(string name, params object?[] values) => type.GetMethod(name,
                BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, values);
            Task Begin(string name)
            {
                // DoEvents may install/restore its own thread context. Each new
                // async operation must capture this fixture's live form instead.
                SynchronizationContext.SetSynchronizationContext(context);
                return (Task)Call(name)!;
            }

            var client = new DownloadSystem("Professional", "es-ES", 22631, 1, "x64", "Client");
            var server = new DownloadSystem("ServerStandard", "en-US", 20348, 1, "x64", "Server");
            var browser = new List<string>();
            var copied = new List<string>();
            var confirmations = 0;
            Set("_readDownloadSystem", new Func<DownloadSystem>(() => client));
            Set("_launchBrowser", new Action<string>(browser.Add));
            Set("_copyCommandToClipboard", new Action<string>(copied.Add));
            Set("_confirmUpgrade", new Func<string, string, bool>((_, _) => { confirmations++; return false; }));
            Set("_resolveDownloadRecommendation", new Func<CancellationToken, Task<DownloadRecommendation>>(_ =>
                throw new Exception("The fixture resolver must be explicitly configured.")));
            Set("_prepareServerCommand", new Func<MountedSource, int, CancellationToken, Task<string>>((_, _, _) =>
                throw new Exception("The fixture command preparer must be explicitly configured.")));
            form.Show();
            Application.DoEvents();
            Call("ApplyDownloadSystem", client);

            var automatic = Field<RadioButton>("_autoDownload");
            var existing = Field<RadioButton>("_existingMedia");
            var sources = Field<ComboBox>("_sources");
            var images = Field<ComboBox>("_installationImages");
            var notice = Field<Label>("_downloadNotice");
            var status = Field<Label>("_status");
            var start = Field<Button>("_start");
            var copy = Field<Button>("_copyServerCommand");
            var preview = Field<TextBox>("_serverCommandPreview");
            var match = Field<Button>("_matchingDownload");
            if (automatic.Enabled || automatic.Checked || !existing.Checked || !match.Enabled ||
                !Field<Button>("_browse").Enabled || !Field<Button>("_browseInstallationFolder").Enabled ||
                !Field<Button>("_pcNinjaDownload").Enabled || !Field<Button>("_microsoft").Enabled)
                throw new Exception("Spanish client gating removed manual alternatives or left automatic download enabled.");

            var clientImage = new InstallationImage(6, "Spanish Pro fixture", "Professional", "x64", "10.0.26100.1", "Client", "es-ES");
            var clientSource = new MountedSource(@"X:\client-fixture", @"X:\client-fixture\setup.exe",
                @"X:\client-fixture\sources\install.wim", [clientImage]);
            void Populate(MountedSource source, InstallationImage? selection = null)
            {
                sources.Text = source.Root;
                Set("_preparedKey", source.Root);
                Set("_preparedSource", source);
                images.Items.Clear();
                foreach (var item in source.Images ?? []) images.Items.Add(item);
                if (selection is not null && !images.Items.Contains(selection)) images.Items.Add(selection);
                images.SelectedItem = selection;
                Call("UpdateImageInfo");
                Call("UpdateSourceControls");
            }
            void AssertNoSetup()
            {
                if (Field<bool>("_setupHasStarted") || confirmations != 0)
                    throw new Exception("Download/manual Server guidance entered Setup or upgrade consent.");
            }
            void AssertIdle()
            {
                if (Field<CancellationTokenSource?>("_operation") is not null || !match.Enabled)
                    throw new Exception("Download/manual command operation did not return to idle.");
                AssertNoSetup();
            }

            var recommendation = new DownloadRecommendation("Microsoft", "https://download.invalid/es-ES",
                "Windows11_es-es_fixture.iso", "Windows 11", "10.0.26100.1", true, "Matching Spanish ISO fixture.");
            Populate(clientSource, clientImage);
            Set("_resolveDownloadRecommendation", new Func<CancellationToken, Task<DownloadRecommendation>>(_ => Task.FromResult(recommendation)));
            Pump(Begin("FindMatchingDownloadAsync"));
            if (!browser.SequenceEqual([recommendation.PageUrl]) || copied.Count != 0 ||
                !existing.Checked || sources.Text != "" || images.Items.Count != 0 ||
                Field<MountedSource?>("_preparedSource") is not null || Field<string?>("_preparedKey") is not null ||
                !notice.Visible || !notice.Text.Contains(recommendation.FileName!) || !notice.Text.Contains("not continue automatically"))
                throw new Exception("Matching download guidance opened the wrong target, retained stale media or failed to explain manual continuation.");
            var persistentNotice = notice.Text;
            Call("UpdateSourceControls");
            if (!notice.Visible || notice.Text != persistentNotice)
                throw new Exception("Source-control refresh erased browser download instructions.");
            AssertIdle();

            // Even a resolver returning success after cancellation cannot launch a
            // browser, replace the selected source or carry automatic consent.
            Populate(clientSource, clientImage);
            var pendingSearch = new TaskCompletionSource<DownloadRecommendation>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken searchToken = default;
            Set("_resolveDownloadRecommendation", new Func<CancellationToken, Task<DownloadRecommendation>>(token =>
            { searchToken = token; return pendingSearch.Task; }));
            var search = Begin("FindMatchingDownloadAsync");
            var searchOperation = Field<CancellationTokenSource>("_operation");
            if (search.IsCompleted || !searchToken.CanBeCanceled || searchToken != searchOperation.Token)
                throw new Exception("The asynchronous download fixture did not use the active cancellable operation.");
            searchOperation.Cancel();
            pendingSearch.SetResult(recommendation);
            Pump(search);
            if (!searchToken.IsCancellationRequested || browser.Count != 1 || copied.Count != 0 ||
                sources.Text != clientSource.Root || !ReferenceEquals(Field<MountedSource>("_preparedSource"), clientSource) ||
                !status.Text.Contains("cancelled", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Cancelled download discovery changed media or opened a browser after its late result.");
            AssertIdle();

            Set("_resolveDownloadRecommendation", new Func<CancellationToken, Task<DownloadRecommendation>>(_ => Task.FromResult(recommendation)));
            Set("_launchBrowser", new Action<string>(_ => throw new InvalidOperationException("Fixture browser unavailable.")));
            Pump(Begin("FindMatchingDownloadAsync"));
            if (!status.Text.Contains("Fixture browser unavailable") || !Field<Label>("_operationNotice").Visible ||
                !notice.Visible || copied.Count != 0 || sources.Text != "")
                throw new Exception("Browser-launch failure was hidden or continued with stale installation media.");
            Set("_launchBrowser", new Action<string>(browser.Add));
            AssertIdle();

            Set("_readDownloadSystem", new Func<DownloadSystem>(() => server));
            Call("ApplyDownloadSystem", server);
            Call("SetBusy", true);
            Call("SetBusy", false);
            if (start.Enabled || Field<CheckBox>("_quietSetup").Enabled || Field<CheckBox>("_autoRestart").Enabled ||
                automatic.Enabled || automatic.Checked || !copy.Visible || !preview.Visible || copy.Enabled)
                throw new Exception("Server guidance re-enabled client Setup/restart controls or copying without a selected image.");

            var serverImage = new InstallationImage(2, "Server Standard fixture", "ServerStandard", "x64",
                "10.0.26100.1", "Server", "en-US");
            var serverSource = new MountedSource(@"X:\server-fixture", @"X:\server-fixture\setup.exe",
                @"X:\server-fixture\sources\install.wim", [serverImage]);
            Populate(serverSource);
            if (copy.Enabled || images.SelectedIndex != -1)
                throw new Exception("Server media granted copying or implicitly selected an image before an explicit choice.");
            images.SelectedItem = serverImage;
            Call("UpdateImageInfo");
            if (!copy.Enabled || start.Enabled)
                throw new Exception("A matching explicit Server image did not enable manual copying exclusively.");

            var order = new List<string>();
            var command = ManualServerUpgrade.BuildCommand(serverSource, serverImage, server);
            Set("_prepareServerCommand", new Func<MountedSource, int, CancellationToken, Task<string>>((source, index, token) =>
            {
                if (!ReferenceEquals(source, serverSource) || index != serverImage.Index || !token.CanBeCanceled)
                    throw new Exception("Manual preparation lost its exact source, index or cancellation token.");
                order.Add("prepare");
                return Task.FromResult(command);
            }));
            Set("_copyCommandToClipboard", new Action<string>(value => { order.Add("copy"); copied.Add(value); }));
            Pump(Begin("CopyServerCommandAsync"));
            if (!order.SequenceEqual(["prepare", "copy"]) || !copied.SequenceEqual([command]) ||
                preview.Text != command || !status.Text.Contains("not executed") || start.Enabled)
                throw new Exception("Server command was not freshly prepared before the explicit fake clipboard write.");
            AssertIdle();

            // Both button availability and handler entry must reject stale or
            // foreign selections, even when a foreign record shares a valid index.
            foreach (var foreign in new[]
            {
                serverImage with { Index = 99 },
                serverImage with { Name = "Foreign metadata with the same index" },
                serverImage with { Language = "es-ES" }
            })
            {
                Populate(serverSource, foreign);
                if (copy.Enabled) throw new Exception("Foreign/stale/incompatible Server image retained an enabled Copy command.");
                var calls = order.Count;
                Pump(Begin("CopyServerCommandAsync"));
                if (copied.Count != 1 || order.Count != calls)
                    throw new Exception("A stale/private Copy invocation reached preparation or clipboard for a foreign image.");
                AssertIdle();
            }

            var unsafeSource = serverSource with { Root = @"X:\%TEMP%\server-fixture", SetupPath = @"X:\%TEMP%\server-fixture\setup.exe",
                ImagePath = @"X:\%TEMP%\server-fixture\sources\install.wim" };
            Populate(unsafeSource, serverImage);
            Set("_prepareServerCommand", new Func<MountedSource, int, CancellationToken, Task<string>>((source, _, _) =>
                Task.FromResult(ManualServerUpgrade.BuildCommand(source, serverImage, server))));
            Pump(Begin("CopyServerCommandAsync"));
            if (copied.Count != 1 || !Field<Label>("_operationNotice").Visible ||
                !status.Text.Contains("absolute source paths", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Rejected command paths reached the clipboard or hid the actionable failure.");
            AssertIdle();

            Populate(serverSource, serverImage);
            var pendingCommand = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationToken commandToken = default;
            Set("_prepareServerCommand", new Func<MountedSource, int, CancellationToken, Task<string>>((_, _, token) =>
            { commandToken = token; return pendingCommand.Task; }));
            var copyTask = Begin("CopyServerCommandAsync");
            if (copyTask.IsCompleted || !commandToken.CanBeCanceled || !start.Text.Contains("CANCEL", StringComparison.OrdinalIgnoreCase))
                throw new Exception("The asynchronous manual-command fixture did not start a cancellable operation.");
            Field<CancellationTokenSource>("_operation").Cancel();
            pendingCommand.SetResult(command);
            Pump(copyTask);
            if (!commandToken.IsCancellationRequested || copied.Count != 1 ||
                !status.Text.Contains("Nothing was copied", StringComparison.OrdinalIgnoreCase) || start.Enabled)
                throw new Exception("Cancellation during manual preparation allowed a late clipboard write or Setup action.");
            AssertIdle();

            preview.Text = command;
            sources.Text = @"X:\different-server-fixture";
            Call("ExistingPathChanged");
            if (Field<MountedSource?>("_preparedSource") is not null || images.Items.Count != 0 || preview.Text != "" || copy.Enabled)
                throw new Exception("Changing Server source retained a stale image/preview/copy action.");
            AssertNoSetup();
            Console.WriteLine("PASS: Independent GUI browser guidance, Spanish manual alternatives, persistent instructions, cancellation/failure recovery and Server manual-copy-only source/index/clipboard guards. All network/browser/clipboard/command preparation effects mocked; no media/Setup used.");
        }
        finally
        {
            form?.Dispose();
            SynchronizationContext.SetSynchronizationContext(priorContext);
        }
    }

    private static void Pump(Task task)
    {
        var elapsed = Stopwatch.StartNew();
        while (!task.IsCompleted)
        {
            if (elapsed.Elapsed > TimeSpan.FromSeconds(5))
                throw new Exception("GUI guidance fixture exceeded its bounded UI-pump timeout.");
            Application.DoEvents();
            Thread.Sleep(1);
        }
        task.GetAwaiter().GetResult();
        Application.DoEvents();
    }

    // With no surrounding Application.Run, an outermost DoEvents can tear down
    // WinForms' implicit thread marshalling control. A WindowsFormsSynchronizationContext
    // captured before that teardown can then strand later asynchronous callbacks.
    // Bind this test context to the actual form's lifetime/BeginInvoke queue instead;
    // continuations still run on the real WinForms UI thread, never a thread pool.
    private sealed class FormPumpContext : SynchronizationContext
    {
        private readonly Control _owner;
        private readonly int _threadId = Environment.CurrentManagedThreadId;

        internal FormPumpContext(Control owner)
        {
            _owner = owner;
            _ = owner.Handle;
        }

        public override SynchronizationContext CreateCopy() => this;

        public override void Post(SendOrPostCallback callback, object? state) =>
            _owner.BeginInvoke(new Action(() => InvokeCallback(callback, state)));

        public override void Send(SendOrPostCallback callback, object? state)
        {
            if (Environment.CurrentManagedThreadId == _threadId) InvokeCallback(callback, state);
            else _owner.Invoke(new Action(() => InvokeCallback(callback, state)));
        }

        private void InvokeCallback(SendOrPostCallback callback, object? state)
        {
            if (Environment.CurrentManagedThreadId != _threadId)
                throw new Exception("A GUI fixture continuation escaped the real WinForms UI thread.");
            var previous = Current;
            SetSynchronizationContext(this);
            try { callback(state); }
            finally { SetSynchronizationContext(previous); }
        }
    }
}
