namespace AndroidAppBridge.Presentation;

/// <summary>Selects local files, locating adjacent expansion files before asking the user for them.</summary>
class PackageImportDialog : Form
{
    public string[] Apks { get; private set; } = [];
    public string[] Obbs { get; private set; } = [];
    readonly ListBox apkList = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    readonly ListBox obbList = new() { Dock = DockStyle.Fill, HorizontalScrollbar = true };
    readonly Label status = new() { Dock = DockStyle.Fill, AutoSize = true };
    readonly Button ready = new() { Dock = DockStyle.Fill, Enabled = false, DialogResult = DialogResult.OK };
    readonly bool english;
    readonly ILocalPackageImporter importer;
    string T(string text) => Localization.Text(text, english);

    public PackageImportDialog(ILocalPackageImporter importer, bool english, Icon? icon)
    {
        this.english = english;
        this.importer = importer;
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = icon;
        Text = T("Импорт APK + OBB");
        Font = new Font("Segoe UI", 10);
        Size = new Size(780, 600);
        MinimumSize = new Size(680, 520);
        StartPosition = FormStartPosition.CenterParent;
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(20), ColumnCount = 2, RowCount = 7 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        var help = new Label { Text = T("Выберите основной APK и, при необходимости, split APK. OBB будут найдены рядом с APK или выбраны вручную."), Dock = DockStyle.Fill, AutoSize = true };
        layout.Controls.Add(help, 0, 0);
        layout.SetColumnSpan(help, 2);
        layout.Controls.Add(new Label { Text = "APK", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        var pickApk = new Button { Text = T("Выбрать APK…"), Dock = DockStyle.Fill };
        layout.Controls.Add(pickApk, 1, 1);
        layout.Controls.Add(apkList, 0, 2);
        layout.SetColumnSpan(apkList, 2);
        layout.Controls.Add(new Label { Text = T("OBB (необязательно)"), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
        var pickObb = new Button { Text = T("Выбрать OBB…"), Dock = DockStyle.Fill };
        layout.Controls.Add(pickObb, 1, 3);
        layout.Controls.Add(obbList, 0, 4);
        layout.SetColumnSpan(obbList, 2);
        status.Text = T("Сначала выберите APK.");
        layout.Controls.Add(status, 0, 5);
        layout.SetColumnSpan(status, 2);
        ready.Text = T("Импортировать файлы");
        layout.Controls.Add(ready, 0, 6);
        var cancel = new Button { Text = T("Отмена"), Dock = DockStyle.Fill, DialogResult = DialogResult.Cancel };
        layout.Controls.Add(cancel, 1, 6);
        Controls.Add(layout);
        AcceptButton = ready;
        CancelButton = cancel;
        pickApk.Click += async (_, _) =>
        {
            using var dialog = new OpenFileDialog { Title = T("Выбрать APK…"), Filter = "APK (*.apk)|*.apk", Multiselect = true };
            if (dialog.ShowDialog(this) != DialogResult.OK)
                return;
            pickApk.Enabled = pickObb.Enabled = ready.Enabled = false;
            try
            {
                var selected = dialog.FileNames;
                var package = await Task.Run(() => importer.Inspect(selected, []));
                var found = await Task.Run(() => importer.FindObbs(selected[0], package.Info.Package));
                if (IsDisposed)
                    return;
                Apks = selected;
                Obbs = found;
                RefreshLists();
                if (found.Length == 0)
                {
                    status.Text = T("OBB рядом с APK не найден. Укажите файл или отмените выбор для установки только APK.");
                    PickObb(Path.GetDirectoryName(selected[0]));
                }
                ValidateSelection();
            }
            catch (Exception e) { if (!IsDisposed) { status.Text = T(e.Message); ready.Enabled = false; } }
            finally { if (!IsDisposed) pickApk.Enabled = pickObb.Enabled = true; }
        };
        pickObb.Click += (_, _) => { PickObb(Apks.Length > 0 ? Path.GetDirectoryName(Apks[0]) : null); ValidateSelection(); };
        var menu = new ContextMenuStrip();
        menu.Items.Add(T("Очистить список OBB"), null, (_, _) => { Obbs = []; RefreshLists(); ValidateSelection(); });
        obbList.ContextMenuStrip = menu;
        Disposed += (_, _) => menu.Dispose();
        ready.Click += (_, _) => { if (!ValidateSelection()) DialogResult = DialogResult.None; };
    }
    void PickObb(string? initialDirectory)
    {
        using var dialog = new OpenFileDialog { Title = T("Выберите OBB или отмените выбор для установки только APK"), Filter = "OBB (*.obb)|*.obb", Multiselect = true, InitialDirectory = initialDirectory ?? "" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
            Obbs = dialog.FileNames;
        RefreshLists();
    }
    void RefreshLists()
    {
        apkList.Items.Clear();
        apkList.Items.AddRange(Apks);
        obbList.Items.Clear();
        obbList.Items.AddRange(Obbs);
    }
    bool ValidateSelection()
    {
        try
        {
            var package = importer.Inspect(Apks, Obbs);
            status.Text = package.Info.Package + " • v" + package.Info.Version + " • APK: " + Apks.Length + " • OBB: " + Obbs.Length;
            ready.Enabled = true;
            return true;
        }
        catch (Exception e) { status.Text = T(e.Message); ready.Enabled = false; return false; }
    }
}
