namespace AppBridge;

// Offline UI fixtures; these checks never connect to or modify Android devices.
partial class BridgeForm
{
    public void PreviewProgress() => Stage("Копирование файла", "split_UnityDataAssetPack.apk  •  288.3 / 600.6 МБ", 48);
    public async Task CheckIdle()
    {
        await Run(_ => Task.CompletedTask);
        if (progress.Value != 0 || percentLabel.Text != "0%" || saveButton.Enabled || installButton.Enabled)
            throw new Exception("Idle state regression.");
    }
    public void CheckLayout()
    {
        foreach (var name in new[] { "BrandTitle", "RefreshButton", "CancelButton", "SaveButton", "InstallButton", "BackupSummary" })
        {
            var c = Controls.Find(name, true).Single();
            var preferred = c.GetPreferredSize(new Size(c.Width, 0));
            if (c.Height + 1 < preferred.Height)
                throw new Exception($"Clipped {name}: height {c.Height}, needs {preferred.Height}");
        }
        if (Math.Abs(saveButton.PointToScreen(Point.Empty).Y - installButton.PointToScreen(Point.Empty).Y) > 1)
            throw new Exception("Primary actions not aligned.");
        var refresh = Controls.Find("RefreshButton", true).Single();
        var about = Controls.Find("AboutButton", true).Single();
        if (refresh.Height != about.Height || Math.Abs(refresh.PointToScreen(Point.Empty).Y - about.PointToScreen(Point.Empty).Y) > 1)
            throw new Exception("Header buttons not aligned");
        if (Math.Abs(language.PointToScreen(Point.Empty).Y + language.Height / 2 - about.PointToScreen(Point.Empty).Y - about.Height / 2) > 1)
            throw new Exception("Language selector not centered");
    }
    // Simulates larger fonts and layout dimensions; this is not a real monitor DPI transition.
    public void SimulateScale(float factor)
    {
        IEnumerable<Control> Tree(Control c)
        {
            yield return c;
            foreach (Control child in c.Controls)
                foreach (var item in Tree(child))
                    yield return item;
        }
        var fonts = Tree(this).Select(c => (c, c.Font)).ToArray();
        AutoScaleMode = AutoScaleMode.None;
        MinimumSize = Size.Empty;
        SuspendLayout();
        Scale(new SizeF(factor, factor));
        foreach (var (c, f) in fonts)
            c.Font = new Font(f.FontFamily, f.Size * factor, f.Style, f.Unit);
        ResumeLayout(true);
        PerformLayout();
    }
    public void PreviewBackup(bool longName = false)
    {
        backup = new Backup(longName ? "com.example.application.with.a.long.package.name.for.layout.verification" : "org.example.notes", "1.0", 23, 36, ["arm64-v8a", "armeabi-v7a", "x86_64", "x86"], [new("apk/base.apk", "preview", 98 * 1048576)], "preview");
        ShowBackup();
    }
    public void CheckSearchAndLanguages()
    {
        allPackages = ["org.example.notes", "org.example.reader", "org.example.messenger"];
        Filter();
        search.Text = " READER ";
        if (packages.Items.Count != 1 || (string?)packages.Items[0] != "org.example.reader")
            throw new Exception("Search matching failed");
        search.Text = "missing";
        if (packages.Items.Count != 0)
            throw new Exception("Search empty results failed");
        search.Clear();
        if (packages.Items.Count != 3)
            throw new Exception("Search reset failed");
        SetLanguage(true, persist: false);
        PreviewBackup();
        Application.DoEvents();
        if (backupLabel.Text.Contains("Версия") || !backupLabel.Text.Contains("Version") || !saveButton.Text.Contains("Back up"))
            throw new Exception("English translation failed");
        CheckLayout();
        ShowAbout(preview: true);
        using (var bmp = new Bitmap(Width, Height))
        {
            DrawToBitmap(bmp, new Rectangle(Point.Empty, Size));
            bmp.Save(Path.Combine(AppContext.BaseDirectory, "ui-en.png"));
        }
        SetLanguage(false, persist: false);
        Application.DoEvents();
        CheckLayout();
        ShowAbout(preview: true);
        if (!saveButton.Text.Contains("Сохранить") || !Localization.NoticeRu.Contains(Localization.Author) || !Localization.NoticeEn.Contains(Localization.Author))
            throw new Exception("Russian translation or author failed");
    }
}
