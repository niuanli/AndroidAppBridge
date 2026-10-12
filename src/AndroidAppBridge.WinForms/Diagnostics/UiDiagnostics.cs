namespace AndroidAppBridge.Presentation;

static class UiDiagnostics
{
    public static bool Run(string[] args, Func<bool, BridgeForm> factory, ILocalPackageImporter importer)
    {
        if (args.Contains("--ui-check"))
        {
            var results = new List<string>();
            foreach (var factor in new[] { 1f, 1.25f, 1.5f })
            {
                using var form = factory(true);
                form.Opacity = 0;
                form.ShowInTaskbar = false;
                form.Show();
                System.Windows.Forms.Application.DoEvents();
                if (factor != 1)
                    form.SimulateScale(factor);
                form.PreviewBackup(longName: factor > 1);
                System.Windows.Forms.Application.DoEvents();
                form.CheckLayout();
                using var picture = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(picture, new Rectangle(Point.Empty, form.Size));
                picture.Save(Path.Combine(AppContext.BaseDirectory, $"ui-{factor * 100:F0}.png"));
                results.Add($"PASS: simulated layout + font scale {factor * 100:F0}%");
            }
            using (var wide = factory(true))
            {
                wide.Opacity = 0;
                wide.ShowInTaskbar = false;
                wide.Show();
                wide.Size = new Size(1900, 1000);
                System.Windows.Forms.Application.DoEvents();
                var check = wide.CheckIdle();
                while (!check.IsCompleted)
                {
                    System.Windows.Forms.Application.DoEvents();
                    Thread.Sleep(10);
                }
                check.GetAwaiter().GetResult();
                wide.PreviewBackup();
                System.Windows.Forms.Application.DoEvents();
                wide.CheckLayout();
                wide.CheckSearchAndLanguages();
                results.Add("PASS: search matching, empty results, reset, RU/EN switching, About notices and author");
                using var picture = new Bitmap(wide.Width, wide.Height);
                wide.DrawToBitmap(picture, new Rectangle(Point.Empty, wide.Size));
                picture.Save(Path.Combine(AppContext.BaseDirectory, "ui-wide-idle.png"));
                results.Add("PASS: wide layout, idle at 0%, action prerequisites");
            }
            File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "ui-check-result.txt"), results);
            foreach (var english in new[] { false, true })
            {
                using var dialog = new PackageImportDialog(importer, english, null);
                dialog.Opacity = 0;
                dialog.ShowInTaskbar = false;
                dialog.Show();
                System.Windows.Forms.Application.DoEvents();
                using var picture = new Bitmap(dialog.Width, dialog.Height);
                dialog.DrawToBitmap(picture, new Rectangle(Point.Empty, dialog.Size));
                picture.Save(Path.Combine(AppContext.BaseDirectory, english ? "ui-import-en.png" : "ui-import-ru.png"));
            }
            return true;
        }
        if (args.Contains("--preview"))
        {
            using var form = factory(true);
            form.Opacity = 0;
            form.ShowInTaskbar = false;
            form.Show();
            System.Windows.Forms.Application.DoEvents();
            form.PerformLayout();
            using var bitmap = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
            bitmap.Save(Path.Combine(AppContext.BaseDirectory, "preview.png"));
            form.PreviewProgress();
            System.Windows.Forms.Application.DoEvents();
            using var active = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(active, new Rectangle(Point.Empty, form.Size));
            active.Save(Path.Combine(AppContext.BaseDirectory, "preview-progress-demo.png"));
            return true;
        }
        return false;
    }
}
