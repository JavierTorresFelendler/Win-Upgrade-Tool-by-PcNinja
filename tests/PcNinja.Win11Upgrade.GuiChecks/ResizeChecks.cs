using System.Reflection;

internal static class ResizeChecks
{
    internal static void Run(Form form, Panel[] cards)
    {
        if (cards.Length != 2) throw new Exception("Expected two bordered dashboard cards.");
        var getStyle = typeof(Control).GetMethod("GetStyle", BindingFlags.NonPublic | BindingFlags.Instance)!;
        foreach (var card in cards)
            foreach (var style in new[] { ControlStyles.ResizeRedraw, ControlStyles.OptimizedDoubleBuffer })
                if (!(bool)getStyle.Invoke(card, [style])!)
                    throw new Exception($"Dashboard card lacks {style}; resizing can leave old border trails.");

        var fullInvalidations = new HashSet<Control>();
        var paints = new HashSet<Control>();
        void Invalidated(object? sender, InvalidateEventArgs e)
        {
            if (sender is Control card && e.InvalidRect.Contains(card.ClientRectangle))
                fullInvalidations.Add(card);
        }
        void Painted(object? sender, PaintEventArgs e)
        {
            if (sender is Control card) paints.Add(card);
        }
        foreach (var card in cards)
        {
            card.Invalidated += Invalidated;
            card.Paint += Painted;
        }
        var originalSize = form.Size;
        var originalState = form.WindowState;
        try
        {
            form.WindowState = FormWindowState.Normal;
            // Exercise live resizing without Refresh, Invalidate or DrawToBitmap:
            // those would repaint the entire card and conceal the original bug.
            foreach (var size in new[]
            {
                form.MinimumSize, new Size(1160, 800), new Size(1200, 850),
                new Size(1100, 760), new Size(1180, 820), form.MinimumSize
            })
            {
                var before = cards.Select(card => card.Size).ToArray();
                fullInvalidations.Clear();
                paints.Clear();
                form.Size = size;
                Application.DoEvents();
                for (var i = 0; i < cards.Length; i++)
                    if (cards[i].Size != before[i] &&
                        (!fullInvalidations.Contains(cards[i]) || !paints.Contains(cards[i])))
                        throw new Exception("Resizing did not invalidate and repaint the complete dashboard card.");
            }
            form.WindowState = FormWindowState.Maximized;
            Application.DoEvents();
            form.WindowState = FormWindowState.Normal;
            Application.DoEvents();
            form.WindowState = FormWindowState.Minimized;
            Application.DoEvents();
            form.WindowState = FormWindowState.Normal;
            Application.DoEvents();
            if (cards.Any(card => !card.Visible || card.Width <= 0 || card.Height <= 0))
                throw new Exception("A dashboard card disappeared after maximize/minimize/restore.");
        }
        finally
        {
            foreach (var card in cards)
            {
                card.Invalidated -= Invalidated;
                card.Paint -= Painted;
            }
            form.WindowState = FormWindowState.Normal;
            form.Size = originalSize;
            form.WindowState = originalState;
            Application.DoEvents();
        }
        Console.WriteLine("PASS: Both dashboard cards use full resize redraw/double buffering; live grow/shrink and maximize/minimize/restore. No forced refresh masked invalidation.");
    }
}
