using System.Reflection;
using PcNinja.Win11Upgrade.Core;

internal static class ImageSelectionChecks
{
    internal static void Run(Form form)
    {
        var type = form.GetType();
        object Field(string name) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(form)!;
        void SetField(string name, object? value) => type.GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(form, value);
        void Invoke(string name, params object[] values) => type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(form, values);
        var images = (ComboBox)Field("_installationImages");
        var sources = (ComboBox)Field("_sources");
        var info = (Label)Field("_imageInfo");
        var status = (Label)Field("_status");
        var notice = (Label)Field("_operationNotice");
        var details = (LinkLabel)Field("_details");
        var current = CurrentWindows.Read();
        var image = new InstallationImage(6, "Matching Windows", current.Edition, "x64",
            $"10.0.{Math.Max(22000, current.Build)}", "Client", current.Language);

        void Populate(params InstallationImage[] fixtures)
        {
            // Synthetic metadata only. These paths are never opened, mounted or launched.
            sources.Text = @"X:\Image-selection-fixture";
            SetField("_preparedSource", new MountedSource(sources.Text, @"X:\setup.exe",
                @"X:\sources\install.wim", fixtures));
            SetField("_preparedKey", sources.Text);
            images.Items.Clear();
            foreach (var fixture in fixtures) images.Items.Add(fixture);
            Invoke("UpdateImageInfo");
        }

        void AssertCue(string expected)
        {
            if (images.SelectedIndex != -1 || images.SelectedItem is not null || images.AccessibleDescription != expected)
                throw new Exception($"Image cue is missing or became a real selection: {expected}");
            if (images.Items.Cast<object>().Any(item => item is not InstallationImage))
                throw new Exception("A placeholder became a selectable image/index.");
            if (Field("_preparedSource") is MountedSource source)
            {
                try
                {
                    InstallationImages.Select(source, null, current);
                    throw new Exception("An ambiguous or incompatible cue allowed implicit image selection.");
                }
                catch (InvalidOperationException) { }
            }
            using var bitmap = new Bitmap(400, 32);
            using var graphics = Graphics.FromImage(bitmap);
            var bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var background = images.BackColor;
            graphics.Clear(background);
            var onDraw = typeof(ComboBox).GetMethod("OnDrawItem", BindingFlags.NonPublic | BindingFlags.Instance)!;
            using var draw = new DrawItemEventArgs(graphics, images.Font, bounds, -1, DrawItemState.ComboBoxEdit);
            onDraw.Invoke(images, [draw]);
            if (!Enumerable.Range(0, bitmap.Width).Any(x => Enumerable.Range(0, bitmap.Height)
                .Any(y => bitmap.GetPixel(x, y).ToArgb() != background.ToArgb())))
                throw new Exception("The empty DropDownList still paints a blank row instead of its cue.");
        }

        sources.Text = "";
        SetField("_preparedSource", null);
        SetField("_preparedKey", null);
        images.Items.Clear();
        Invoke("UpdateImageInfo");
        AssertCue("Validate a source first");
        if (images.Items.Count != 0) throw new Exception("Unvalidated media acquired an image item.");

        Populate(image, image with { Index = 9, Name = "Second matching Windows" });
        AssertCue("Choose a Windows image…");
        if (images.Items.Count != 2) throw new Exception("The prompt replaced actual image items.");
        Invoke("PresentImageInspection");
        if (!status.Text.Contains("Choose a Windows image"))
            throw new Exception("Ambiguous images did not request a real selection after Inspect.");

        var otherLanguage = current.Language == "he-IL" ? "en-US" : "he-IL";
        var mismatches = new[]
        {
            image with { Index = 1, Language = otherLanguage },
            image with { Index = 2, Edition = "FixtureOtherEdition" },
            image with { Index = 3, Architecture = "x86" },
            image with { Index = 4, InstallationType = "Server" },
            image with { Index = 5, Language = otherLanguage }
        };
        Populate(mismatches);
        AssertCue("No compatible Windows image");
        Invoke("PresentImageInspection");
        if (!status.Text.Contains("No matching image") || !notice.Visible || notice.Text != info.Text)
            throw new Exception("Inspect hid the no-compatible-media explanation behind generic selection guidance.");
        foreach (var mismatch in mismatches)
            if (info.Text.Split(InstallationImages.GetIncompatibility(mismatch, current)!, StringSplitOptions.None).Length != 2)
                throw new Exception("An existing Core incompatibility reason was lost or duplicated in the empty-selection explanation.");
        var warningColor = info.ForeColor;
        var mismatchNotice = notice.Text;
        Invoke("RefreshSources", false);
        AssertCue("No compatible Windows image");
        if (notice.Text != mismatchNotice || !notice.Visible)
            throw new Exception("Refresh erased the no-compatible-media notice.");

        Populate(image, mismatches[0]);
        images.SelectedIndex = 1;
        var selectedWarning = info.Text;
        Invoke("PresentImageInspection");
        if (!selectedWarning.Contains("Language mismatch") || !selectedWarning.Contains(otherLanguage) ||
            !selectedWarning.Contains(current.Language) || info.ForeColor != warningColor || notice.Text != selectedWarning)
            throw new Exception("The cue overrode a selected image's existing language warning.");
        images.SelectedIndex = 0;
        Invoke("PresentImageInspection");
        if (!Equals(images.SelectedItem, image) || !info.Text.Contains("Selected image 6") ||
            info.ForeColor == warningColor || !status.Text.Contains("Image 6 selected") ||
            images.AccessibleDescription != info.Text)
            throw new Exception("Matching image identity, details or ready guidance changed.");
        Invoke("RefreshSources", false);
        if (!Equals(images.SelectedItem, image)) throw new Exception("Refresh lost the real matching image selection.");

        // Updating cue text must never touch the independent error/details channels.
        sources.Text = @"X:\Changed-image-selection-fixture";
        AssertCue("Validate a source first");
        if (images.Items.Count != 0 || !info.Text.Contains("Source changed"))
            throw new Exception("A changed source retained an old index or lost revalidation guidance.");
        Invoke("ShowIssue", new InvalidOperationException("Fixture mount failed: media is unavailable."));
        var failure = status.Text;
        var failureDetails = Field("_lastErrorDetails");
        Invoke("UpdateImageInfo");
        Invoke("RefreshSources", false);
        AssertCue("Validate a source first");
        if (!notice.Visible || notice.Text != failure || !details.Visible ||
            !Equals(Field("_lastErrorDetails"), failureDetails) || info.Text.Contains("No compatible"))
            throw new Exception("A read failure was overwritten or misrepresented as an image incompatibility.");
        Populate(image, image with { Index = 9 });
        AssertCue("Choose a Windows image…");
        if (notice.Text != failure || !details.Visible || !Equals(Field("_lastErrorDetails"), failureDetails))
            throw new Exception("Image cue updates erased unrelated error information.");

        // Leave no fixture media or selection behind for the normal preview.
        sources.Text = "";
        Console.WriteLine("PASS: Image cues render without fake items; no-source/multiple/no-compatible/selected states; Core mismatch reasons, source invalidation, refresh and independent failure/details retained. No media or Setup accessed.");
    }
}
