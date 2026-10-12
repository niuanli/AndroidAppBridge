using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AppBridge;

/// <summary>Coordinates the transfer workflow and keeps device operations off the UI thread.</summary>
partial class BridgeForm : Form
{
    readonly ComboBox source = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
    readonly ComboBox target = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 380 };
    readonly TextBox search = new() { Width = 380, PlaceholderText = "Найти приложение…" };
    readonly Label matchCount = new() { Dock = DockStyle.Fill, ForeColor = Color.FromArgb(100, 116, 139), Text = "Загрузите список приложений", AutoEllipsis = true };
    readonly ListBox packages = new() { Dock = DockStyle.Fill };
    readonly TextBox log = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.FromArgb(24, 29, 38), ForeColor = Color.FromArgb(220, 230, 239), Font = new Font("Consolas", 10) };
    readonly Label status = new() { Dock = DockStyle.Fill, Text = "Готов к работе", Font = new Font("Segoe UI", 11, FontStyle.Bold), AutoEllipsis = true };
    readonly Label progressDetail = new() { Dock = DockStyle.Fill, Text = "Подключите устройство и разрешите отладку по USB.", AutoEllipsis = true, ForeColor = Color.FromArgb(83, 98, 118) };
    readonly Label percentLabel = new() { Dock = DockStyle.Fill, Text = "0%", TextAlign = ContentAlignment.MiddleRight };
    readonly Label backupLabel = new() { Name = "BackupSummary", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(10), BackColor = Color.FromArgb(245, 248, 252), Text = "Копия не выбрана\r\nСохраните приложение слева или откройте готовую копию.", AutoEllipsis = false };
    readonly CheckBox withObb = new() { Text = "Копировать доступные OBB-файлы", Checked = true, AutoSize = true };
    readonly Button cancel = new() { Text = "Отмена", Enabled = false, AutoSize = true };
    readonly TransferProgress progress = new() { Dock = DockStyle.Fill, Style = ProgressBarStyle.Continuous, Visible = true };
    Button saveButton = null!, installButton = null!, launchButton = null!;
    bool transferCompleted;
    bool english = LoadEnglish();
    readonly Dictionary<Control, string> captions = new();
    readonly List<string> messages = new();
    readonly ComboBox language = new() { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill, AccessibleName = "Language / Язык" };
    string statusText = "Готов к работе", detailText = "Подключите устройство и разрешите отладку по USB.", countText = "Загрузите список приложений", summaryText = "Копия не выбрана\r\nСохраните приложение слева или откройте готовую копию.";
    readonly List<Control> actions = new();
    string[] allPackages = [];
    string? backupFolder;
    Backup? backup;
    CancellationTokenSource? work;
    readonly string adb = Path.Combine(AppContext.BaseDirectory, "platform-tools", "adb.exe");
    readonly string backupRoot = Path.Combine(AppContext.BaseDirectory, "Backups");

    public BridgeForm(bool previewOnly = false)
    {
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Text = "AndroidAppBridge 0.3.2 — перенос Android-приложений";
        Width = 1180;
        Height = 960;
        MinimumSize = new Size(1060, 940);
        Icon = Icon.ExtractAssociatedIcon(Path.Combine(AppContext.BaseDirectory, "AndroidAppBridge.exe"));
        Font = new Font("Segoe UI", 10);
        BackColor = Color.FromArgb(245, 247, 250);
        var layout = Grid(1, 4);
        layout.Padding = new Padding(20);
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 75));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 25));
        var header = Grid(3, 2);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 64));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 342));
        header.AutoSize = true;
        header.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        header.Padding = new Padding(0, 0, 0, 10);
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        header.Controls.Add(new PictureBox { Image = Icon?.ToBitmap(), Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 4, 10, 8) }, 0, 0);
        header.SetRowSpan(header.GetControlFromPosition(0, 0)!, 2);
        var title = Heading("AndroidAppBridge", 23);
        title.Name = "BrandTitle";
        title.AutoSize = true;
        header.Controls.Add(title, 1, 0);
        header.Controls.Add(new Label { AutoSize = true, Dock = DockStyle.Fill, Text = "Перенос Android-приложений через резервную копию на ПК.", ForeColor = Color.FromArgb(83, 98, 118) }, 1, 1);
        var toolbar = Grid(3, 1);
        toolbar.Dock = DockStyle.Top;
        toolbar.Height = 44;
        toolbar.Margin = new Padding(10, 8, 0, 8);
        toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 88));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 44));
        var refresh = Action("Обновить устройства", RefreshDevices);
        refresh.Name = "RefreshButton";
        refresh.Dock = DockStyle.Fill;
        refresh.Margin = Padding.Empty;
        toolbar.Controls.Add(refresh, 0, 0);
        var languageHost = new Panel { Dock = DockStyle.Fill, Margin = new Padding(10, 0, 10, 0), BackColor = Color.White };
        language.Items.AddRange(["RU", "EN"]);
        language.SelectedIndex = english ? 1 : 0;
        language.Dock = DockStyle.None;
        languageHost.Controls.Add(language);
        void CenterLanguage()
        {
            language.Width = languageHost.ClientSize.Width;
            language.Location = new Point(0, Math.Max(0, (languageHost.ClientSize.Height - language.Height) / 2));
        }
        languageHost.Resize += (_, _) => CenterLanguage();
        language.FontChanged += (_, _) => CenterLanguage();
        toolbar.Controls.Add(languageHost, 1, 0);
        var about = new Button { Name = "AboutButton", AccessibleName = "О программе", Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(41, 99, 218), Margin = Padding.Empty, TabStop = true };
        about.FlatAppearance.BorderColor = Color.FromArgb(205, 215, 228);
        about.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 241, 255);
        about.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var diameter = Math.Min(about.ClientSize.Width, about.ClientSize.Height) * .54f;
            var rect = new RectangleF((about.ClientSize.Width - diameter) / 2, (about.ClientSize.Height - diameter) / 2, diameter, diameter);
            using var pen = new Pen(about.ForeColor, 1.7f);
            e.Graphics.DrawEllipse(pen, rect);
            using var font = new Font("Segoe UI", diameter * .55f, FontStyle.Bold, GraphicsUnit.Pixel);
            using var brush = new SolidBrush(about.ForeColor);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("i", font, brush, rect, format);
        };
        about.Click += (_, _) => ShowAbout();
        toolbar.Controls.Add(about, 2, 0);
        header.Controls.Add(toolbar, 2, 0);
        header.SetRowSpan(toolbar, 2);
        layout.Controls.Add(header, 0, 0);
        var headerTips = new ToolTip();
        void UpdateHeaderTips()
        {
            headerTips.SetToolTip(about, english ? "About AndroidAppBridge" : "О программе AndroidAppBridge");
            about.AccessibleName = english ? "About AndroidAppBridge" : "О программе AndroidAppBridge";
            headerTips.SetToolTip(language, english ? "Interface language" : "Язык интерфейса");
        }
        UpdateHeaderTips();
        language.SelectedIndexChanged += (_, _) =>
        {
            BeginInvoke((Action)UpdateHeaderTips);
        };
        Disposed += (_, _) => headerTips.Dispose();
        var body = Grid(2, 1);
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var bodyViewport = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Margin = Padding.Empty };
        body.Dock = DockStyle.Top;
        body.MinimumSize = new Size(0, 504);
        bodyViewport.Controls.Add(body);
        bodyViewport.SizeChanged += (_, _) => body.Height = Math.Max(bodyViewport.ClientSize.Height, body.MinimumSize.Height);
        var left = Grid(1, 10);
        left.BackColor = Color.White;
        left.Padding = new Padding(16);
        left.Margin = new Padding(0, 0, 8, 8);
        foreach (var h in new[] { 34, 24, 40, 48, 26 })
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var h in new[] { 30, 44, 44, 30 })
            left.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        left.Controls.Add(Heading("1. Сохранить с устройства"), 0, 0);
        left.Controls.Add(new Label { Text = "Источник", Dock = DockStyle.Fill }, 0, 1);
        source.Dock = DockStyle.Fill;
        left.Controls.Add(source, 0, 2);
        var filters = Grid(2, 1);
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
        filters.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var searchField = new SearchField(search) { Name = "SearchField", Dock = DockStyle.Fill, Margin = new Padding(3, 3, 8, 5) };
        filters.Controls.Add(searchField, 0, 0);
        var listButton = Action("Загрузить список", ListPackages);
        listButton.Dock = DockStyle.Fill;
        filters.Controls.Add(listButton, 1, 0);
        left.Controls.Add(filters, 0, 3);
        left.Controls.Add(matchCount, 0, 4);
        packages.BorderStyle = BorderStyle.FixedSingle;
        packages.HorizontalScrollbar = true;
        left.Controls.Add(packages, 0, 5);
        left.Controls.Add(withObb, 0, 6);
        saveButton = Action("Сохранить выбранное приложение", Export);
        saveButton.Name = "SaveButton";
        Primary(saveButton);
        left.Controls.Add(saveButton, 0, 7);
        left.Controls.Add(new Label { Text = "Для личного бэкапа. Условия — в About it.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(83, 98, 118), Padding = new Padding(0, 2, 0, 0) }, 0, 9);
        var right = Grid(1, 10);
        right.BackColor = Color.White;
        right.Padding = new Padding(16);
        right.Margin = new Padding(8, 0, 0, 8);
        foreach (var h in new[] { 34, 24, 40 })
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        foreach (var h in new[] { 44, 44, 30 })
            right.RowStyles.Add(new RowStyle(SizeType.Absolute, h));
        right.Controls.Add(Heading("2. Установить копию"), 0, 0);
        right.Controls.Add(new Label { Text = "Получатель", Dock = DockStyle.Fill }, 0, 1);
        target.Dock = DockStyle.Fill;
        right.Controls.Add(target, 0, 2);
        right.Controls.Add(backupLabel, 0, 3);
        var open = Action("Открыть сохранённую копию…", OpenBackup);
        open.Dock = DockStyle.Fill;
        right.Controls.Add(open, 0, 4);
        var usbHelp = new Label
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.FromArgb(232, 241, 255),
            ForeColor = Color.FromArgb(30, 58, 100),
            Text = "Включите отладку по USB и разрешите\r\nдоступ этому компьютеру на устройстве.\r\nВ настройках для разработчиков включите\r\n«Установка через USB» (Install via USB),\r\nесли такой пункт есть. Держите экран\r\nразблокированным и подтвердите запрос установки."
        };
        right.Controls.Add(usbHelp, 0, 5);
        installButton = Action("Установить на выбранное устройство", Install);
        installButton.Name = "InstallButton";
        Primary(installButton);
        right.Controls.Add(installButton, 0, 7);
        launchButton = Action("Запустить приложение", Launch);
        launchButton.Dock = DockStyle.Fill;
        right.Controls.Add(launchButton, 0, 8);
        right.Controls.Add(new Label { Text = "Закрытые данные и аккаунты не копируются.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(83, 98, 118), Padding = new Padding(0, 2, 0, 0) }, 0, 9);
        // Grow the scrollable device area when wrapped metadata or instructions need more height.
        void FitBackupSummary()
        {
            var width = Math.Max(1, right.ClientSize.Width - right.Padding.Horizontal - backupLabel.Margin.Horizontal);
            var textHeight = backupLabel.GetPreferredSize(new Size(width, 0)).Height;
            var fixedHeight = (int)Math.Ceiling(right.RowStyles.Cast<RowStyle>().Where(r => r.SizeType == SizeType.Absolute).Sum(r => r.Height));
            var helpHeight = usbHelp.GetPreferredSize(new Size(Math.Max(1, right.ClientSize.Width - right.Padding.Horizontal - usbHelp.Margin.Horizontal), 0)).Height;
            var required = fixedHeight + textHeight + helpHeight + backupLabel.Margin.Vertical + usbHelp.Margin.Vertical + right.Padding.Vertical + right.Margin.Vertical;
            body.MinimumSize = new Size(0, required);
            body.Height = Math.Max(bodyViewport.ClientSize.Height, required);
        }
        backupLabel.TextChanged += (_, _) => FitBackupSummary();
        usbHelp.TextChanged += (_, _) => FitBackupSummary();
        right.SizeChanged += (_, _) => FitBackupSummary();
        body.Controls.Add(left, 0, 0);
        body.Controls.Add(right, 1, 0);
        layout.Controls.Add(bodyViewport, 0, 1);
        var activity = Grid(3, 3);
        activity.AutoSize = true;
        activity.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        activity.BackColor = Color.White;
        activity.Padding = new Padding(12);
        activity.Margin = new Padding(0, 0, 0, 10);
        activity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        activity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));
        activity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
        activity.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        activity.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
        activity.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        status.AutoSize = true;
        progressDetail.AutoSize = true;
        activity.Controls.Add(status, 0, 0);
        activity.Controls.Add(percentLabel, 1, 0);
        cancel.Name = "CancelButton";
        cancel.Dock = DockStyle.Fill;
        cancel.AutoSize = true;
        cancel.MinimumSize = new Size(96, 36);
        cancel.FlatStyle = FlatStyle.Flat;
        cancel.FlatAppearance.BorderColor = Color.FromArgb(205, 215, 228);
        cancel.Padding = Padding.Empty;
        cancel.Margin = new Padding(3, 0, 3, 6);
        activity.Controls.Add(cancel, 2, 0);
        activity.Controls.Add(progress, 0, 1);
        activity.SetColumnSpan(progress, 3);
        activity.Controls.Add(progressDetail, 0, 2);
        activity.SetColumnSpan(progressDetail, 3);
        layout.Controls.Add(activity, 0, 2);
        var journal = Grid(1, 2);
        journal.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
        journal.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        journal.Margin = new Padding(0);
        journal.Controls.Add(new Label { Text = "Журнал операций", Dock = DockStyle.Fill }, 0, 0);
        log.WordWrap = false;
        log.ScrollBars = ScrollBars.Both;
        journal.Controls.Add(log, 0, 1);
        layout.Controls.Add(journal, 0, 3);
        Controls.Add(layout);
        actions.AddRange([source, target, search, packages, withObb]);
        search.TextChanged += (_, _) => Filter();
        KeyPreview = true;
        KeyDown += (_, e) =>
        {
            if (e.Control && e.KeyCode == Keys.F)
            {
                search.Focus();
                search.SelectAll();
                e.SuppressKeyPress = true;
            }
        };
        source.SelectedIndexChanged += (_, _) =>
        {
            allPackages = [];
            Filter();
        };
        packages.SelectedIndexChanged += (_, _) => UpdateActions();
        target.SelectedIndexChanged += (_, _) => UpdateActions();
        cancel.Click += (_, _) => work?.Cancel();
        FormClosing += (_, e) =>
        {
            if (work != null)
            {
                work.Cancel();
                e.Cancel = true;
                Write("Операция отменяется. Закройте окно после её завершения.");
            }
        };
        if (!previewOnly)
            Shown += async (_, _) => await Run(RefreshDevices);
        IEnumerable<Control> AllControls(Control c)
        {
            yield return c;
            foreach (Control child in c.Controls)
                foreach (var item in AllControls(child))
                    yield return item;
        }
        foreach (var c in AllControls(this))
            if (c != status && c != progressDetail && c != matchCount && c != backupLabel && c != log && c is not TextBox && c is not ComboBox && c is not ListBox && c.Text.Length > 0)
                captions[c] = c.Text;
        language.SelectedIndexChanged += (_, _) =>
        {
            if (!changingLanguage)
                SetLanguage(language.SelectedIndex == 1);
        };
        SetLanguage(english, persist: false);
        UpdateActions();
    }
    static bool LoadEnglish()
    {
        try
        {
            return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "language.txt")).Trim() == "EN";
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
    string T(string text) => Localization.Text(text, english);
    void Detail(string text)
    {
        detailText = text;
        progressDetail.Text = T(text);
    }
    void Matches(string text)
    {
        countText = text;
        matchCount.Text = T(text);
    }
    void Summary(string text)
    {
        summaryText = text;
        backupLabel.Text = T(text);
    }
    bool changingLanguage;
    /// <summary>Rebuilds captions and logs from their original strings to make language changes reversible.</summary>
    public void SetLanguage(bool value, bool persist = true)
    {
        english = value;
        SuspendLayout();
        changingLanguage = true;
        language.SelectedIndex = value ? 1 : 0;
        changingLanguage = false;
        foreach (var p in captions)
            p.Key.Text = T(p.Value);
        status.Text = T(statusText);
        Detail(detailText);
        Matches(countText);
        Summary(summaryText);
        search.PlaceholderText = T("Найти приложение…");
        search.AccessibleName = T("Поиск приложения по имени пакета");
        log.Text = string.Join(Environment.NewLine, messages.Select(T)) + (messages.Count == 0 ? "" : Environment.NewLine);
        ResumeLayout(true);
        if (persist)
            try
            {
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "language.txt"), english ? "EN" : "RU");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
    }
    public void ShowAbout(bool preview = false)
    {
        using var dialog = new Form { Text = "AndroidAppBridge — About it", StartPosition = FormStartPosition.CenterParent, Size = new Size(720, 720), MinimumSize = new Size(660, 660), Icon = Icon, Font = Font, BackColor = Color.White, AutoScaleMode = AutoScaleMode.Dpi };
        var panel = Grid(1, 5);
        panel.Padding = new Padding(24);
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        var title = Heading("AndroidAppBridge 0.3.2", 22);
        title.AutoSize = true;
        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(new Label { Text = (english ? "Developer: " : "Разработчик: ") + Localization.Author + "\r\n© 2026. " + (english ? "All rights reserved." : "Все права защищены."), Dock = DockStyle.Fill, Padding = new Padding(0, 8, 0, 0) }, 0, 1);
        panel.Controls.Add(new Label { Text = english ? "Purpose and terms of use" : "Назначение и условия использования", Dock = DockStyle.Fill, AutoSize = true, Font = new Font(Font, FontStyle.Bold), Padding = new Padding(0, 8, 0, 8) }, 0, 2);
        panel.Controls.Add(new TextBox { Text = english ? Localization.NoticeEn : Localization.NoticeRu, Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0, 12, 0, 12) }, 0, 3);
        var close = new Button { Text = english ? "Close" : "Закрыть", Dock = DockStyle.Right, Width = 120, DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat };
        panel.Controls.Add(close, 0, 4);
        dialog.Controls.Add(panel);
        dialog.AcceptButton = close;
        dialog.CancelButton = close;
        dialog.Shown += (_, _) =>
        {
            dialog.ActiveControl = close;
        };
        if (preview)
        {
            dialog.Opacity = 0;
            dialog.ShowInTaskbar = false;
            dialog.Show();
            Application.DoEvents();
            using var bmp = new Bitmap(dialog.Width, dialog.Height);
            dialog.DrawToBitmap(bmp, new Rectangle(Point.Empty, dialog.Size));
            bmp.Save(Path.Combine(AppContext.BaseDirectory, english ? "about-en.png" : "about-ru.png"));
        }
        else
            dialog.ShowDialog(this);
    }
    static TableLayoutPanel Grid(int cols, int rows) => new() { Dock = DockStyle.Fill, ColumnCount = cols, RowCount = rows, Margin = new Padding(0) };
    static Label Heading(string text, int size = 14) => new() { Text = text, Font = new Font("Segoe UI", size, FontStyle.Bold), Dock = DockStyle.Fill };
    static void Primary(Button b)
    {
        b.Dock = DockStyle.Fill;
        b.FlatAppearance.BorderSize = 0;
        b.UseVisualStyleBackColor = false;
        void Colors()
        {
            b.BackColor = b.Enabled ? Color.FromArgb(41, 99, 218) : Color.FromArgb(228, 234, 244);
            b.ForeColor = b.Enabled ? Color.White : Color.FromArgb(107, 120, 139);
        }
        b.EnabledChanged += (_, _) => Colors();
        Colors();
    }
    // Export needs a source and selection; installation and launch need a target and valid manifest.
    void UpdateActions()
    {
        if (work != null)
            return;
        saveButton.Enabled = source.SelectedItem is Device { State: "device" } && packages.SelectedItem != null;
        installButton.Enabled = backup != null && target.SelectedItem is Device { State: "device" };
        launchButton.Enabled = installButton.Enabled;
    }
    // A null percentage means Android does not expose measurable progress for this stage.
    void Stage(string title, string detail = "", int? value = null)
    {
        statusText = title;
        status.Text = T(title);
        Detail(detail);
        progress.Style = value.HasValue ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
        progress.Value = Math.Clamp(value ?? 0, 0, 100);
        percentLabel.Text = value.HasValue ? $"{progress.Value}%" : "…";
    }
    Button Action(string title, Func<CancellationToken, Task> action)
    {
        var b = new Button { Text = title, AutoSize = false, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = Color.White, Margin = new Padding(3, 3, 3, 5), Padding = new Padding(4, 0, 4, 0) };
        b.FlatAppearance.BorderColor = Color.FromArgb(205, 215, 228);
        actions.Add(b);
        b.Click += async (_, _) => await Run(action);
        return b;
    }
    void Write(string text)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {text}";
        messages.Add(line);
        log.AppendText(T(line) + Environment.NewLine);
    }
    /// <summary>Serializes operations, owns cancellation and restores action availability after completion.</summary>
    async Task Run(Func<CancellationToken, Task> action)
    {
        if (work != null)
            return;
        work = new CancellationTokenSource();
        transferCompleted = false;
        foreach (var c in actions)
            c.Enabled = false;
        cancel.Enabled = true;
        Stage("Выполняется…");
        try
        {
            await action(work.Token);
            if (transferCompleted)
                Stage("Готово", detailText, 100);
            else
                Stage("Готов к работе", detailText, 0);
        }
        catch (OperationCanceledException)
        {
            Write("Операция отменена. Частичная копия не готова к установке; уже установленные приложения остаются на устройстве.");
            Stage("Отменено", "Подробности в журнале.", 0);
        }
        catch (Exception e)
        {
            Write("ОШИБКА: " + Explain(e.Message));
            Stage("Не удалось завершить операцию", "Причина и рекомендации — в журнале ниже.", 0);
        }
        finally
        {
            work.Dispose();
            work = null;
            foreach (var c in actions)
                c.Enabled = true;
            cancel.Enabled = false;
            UpdateActions();
        }
    }
    static string Explain(string message) => message + (message.Contains("INSTALL_FAILED_USER_RESTRICTED") ? "\r\nУстройство отклонило установку. Разблокируйте экран и подтвердите запрос установки. Проверьте «Установка через USB» (Install via USB) в настройках для разработчиков, если такой пункт есть, и ограничения установки в настройках безопасности. При наличии ограничений администратора обратитесь к нему. Ошибка не означает повреждение копии." : message.Contains("INSTALL_FAILED_NO_MATCHING_ABIS") ? "\r\nНужна сборка для архитектуры целевого устройства." : message.Contains("INSTALL_FAILED_DEPRECATED_SDK_VERSION") ? "\r\nAndroid блокирует слишком старый target SDK. Автоматическое изменение APK не выполняется." : message.Contains("INSTALL_FAILED_UPDATE_INCOMPATIBLE") ? "\r\nПодписи приложений различаются. Удаление существующего приложения удалит его данные; AppBridge не удаляет его автоматически." : message.Contains("INSTALL_FAILED_VERSION_DOWNGRADE") ? "\r\nНа устройстве более новая версия; понижение не выполняется." : message.Contains("unauthorized") ? "\r\nРазблокируйте устройство и подтвердите отладку по USB." : "");
    /// <summary>Runs ADB with separate arguments, concurrent output reads and a bounded process lifetime.</summary>
    async Task<string> Adb(string? serial, CancellationToken ct, params string[] args)
    {
        if (!File.Exists(adb))
            throw new Exception("Нет platform-tools/adb.exe рядом с программой.");
        var psi = new ProcessStartInfo(adb) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (serial != null)
        {
            psi.ArgumentList.Add("-s");
            psi.ArgumentList.Add(serial);
        }
        foreach (var a in args)
            psi.ArgumentList.Add(a);
        using var p = Process.Start(psi) ?? throw new Exception("Не удалось запустить ADB.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        using var reg = timeout.Token.Register(() => { try { if (!p.HasExited) p.Kill(true); } catch { } });
        // Drain both streams before waiting so a full output buffer cannot deadlock the process.
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(timeout.Token);
        var output = (await stdout + "\n" + await stderr).Trim();
        if (p.ExitCode != 0)
            throw new Exception(output);
        return output;
    }
    Device Selected(ComboBox box) => box.SelectedItem is Device { State: "device" } d ? d : throw new Exception("Выберите подключённое устройство со статусом device. Для unauthorized подтвердите доступ на его экране.");
    /// <summary>Measures bytes written locally and retries failed pulls without treating cancellation as a failure.</summary>
    async Task<string> Pull(string serial, string remote, string local, CancellationToken ct)
    {
        var sizeText = await Adb(serial, ct, "shell", "stat", "-c", "%s", remote);
        long.TryParse(sizeText.Trim(), out var total);
        for (var attempt = 1; ; attempt++)
        {
            Stage("Копирование файла", Path.GetFileName(remote), total > 0 ? 0 : null);
            using var timer = new System.Windows.Forms.Timer { Interval = 150 };
            timer.Tick += (_, _) =>
            {
                try
                {
                    var size = File.Exists(local) ? new FileInfo(local).Length : 0;
                    var value = total > 0 ? (int?)Math.Clamp(size * 100 / total, 0, 100) : null;
                    Stage("Копирование файла", $"{Path.GetFileName(remote)}  •  {size / 1048576.0:F1} / {total / 1048576.0:F1} МБ", value);
                }
                catch (IOException) { }
            };
            timer.Start();
            try
            {
                var output = await Adb(serial, ct, "pull", remote, local);
                timer.Stop();
                Stage("Файл скопирован", Path.GetFileName(remote), 100);
                return output;
            }
            catch (Exception e) when (e is not OperationCanceledException && attempt < 3)
            {
                timer.Stop();
                Stage("Повтор передачи", Path.GetFileName(remote));
                Write($"Передача прервана: {e.Message}. Повтор {attempt}/2 через 2 секунды…");
                await Task.Delay(2000, ct);
            }
        }
    }
    async Task RefreshDevices(CancellationToken ct)
    {
        Stage("Поиск устройств", "Подтвердите отладку по USB на подключённом устройстве.");
        var text = await Adb(null, ct, "devices", "-l");
        var devices = text.Split('\n').Select(s => Regex.Match(s.Trim(), @"^(\S+)\s+(device|unauthorized|offline)\b(.*)$")).Where(m => m.Success).Select(m => new Device(m.Groups[1].Value, m.Groups[2].Value, Regex.Match(m.Groups[3].Value, @"model:(\S+)") is var model && model.Success ? model.Groups[1].Value.Replace('_', ' ') : "Android")).ToArray();
        var oldSource = (source.SelectedItem as Device)?.Serial;
        var oldTarget = (target.SelectedItem as Device)?.Serial;
        source.Items.Clear();
        target.Items.Clear();
        source.Items.AddRange(devices);
        target.Items.AddRange(devices);
        if (devices.Length > 0)
            source.SelectedIndex = Math.Max(0, Array.FindIndex(devices, d => d.Serial == oldSource));
        if (devices.Length > 0)
            target.SelectedIndex = oldTarget != null ? Math.Max(0, Array.FindIndex(devices, d => d.Serial == oldTarget)) : Math.Min(1, devices.Length - 1);
        Write(devices.Length == 0 ? "Устройств нет. Подключите USB-кабель, включите отладку и подтвердите доступ." : $"Устройств: {devices.Length}. Можно сначала сохранить копию, затем подключить другое устройство.");
        Detail(devices.Any(d => d.State == "device") ? "Устройство подключено. Загрузите список приложений или откройте сохранённую копию." : "Подключите устройство и подтвердите отладку по USB на его экране.");
    }
    async Task ListPackages(CancellationToken ct)
    {
        Stage("Загрузка списка приложений");
        var d = Selected(source);
        var text = await Adb(d.Serial, ct, "shell", "pm", "list", "packages", "-3");
        allPackages = text.Split('\n').Select(s => s.Trim()).Where(s => s.StartsWith("package:")).Select(s => s[8..]).Where(Storage.PackageValid).Order().ToArray();
        Filter();
        Write($"Приложений пользователя: {allPackages.Length}. Выберите пакет для копирования.");
        Detail($"Приложений: {allPackages.Length}. Выберите приложение в списке слева.");
    }
    // Search package IDs without changing the complete list, preserving the selection when possible.
    void Filter()
    {
        var selected = packages.SelectedItem as string;
        var query = search.Text.Trim();
        var matches = allPackages.Where(s => s.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        packages.BeginUpdate();
        try
        {
            packages.Items.Clear();
            packages.Items.AddRange(matches);
            if (selected != null && packages.Items.Contains(selected))
                packages.SelectedItem = selected;
        }
        finally
        {
            packages.EndUpdate();
        }
        Matches(allPackages.Length == 0 ? "Загрузите список приложений" : query.Length == 0 ? $"Приложений: {allPackages.Length}" : matches.Length == 0 ? "Ничего не найдено — измените запрос" : $"Найдено: {matches.Length} из {allPackages.Length}");
    }
    /// <summary>Copies installed APK splits and accessible OBB files, then commits a verified manifest.</summary>
    async Task Export(CancellationToken ct)
    {
        Stage("Подготовка резервной копии", "Определение версии и состава APK.");
        var d = Selected(source);
        var package = packages.SelectedItem as string ?? throw new Exception("Сначала выберите приложение из списка.");
        var pathsText = await Adb(d.Serial, ct, "shell", "pm", "path", package);
        var remoteApks = pathsText.Split('\n').Where(s => s.Trim().StartsWith("package:")).Select(s => s.Trim()[8..]).ToArray();
        if (remoteApks.Length == 0 || remoteApks.Any(s => !Regex.IsMatch(s, @"^/[A-Za-z0-9_./~+=-]+\.apk$")))
            throw new Exception("Не удалось получить безопасный список APK.");
        var dump = await Adb(d.Serial, ct, "shell", "dumpsys", "package", package);
        int Field(string name) => int.TryParse(Regex.Match(dump, name + @"=(\d+)").Groups[1].Value, out var n) ? n : 0;
        var folder = Path.Combine(backupRoot, package + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(Path.Combine(folder, "apk"));
        Write($"Копирование {package} → {folder}");
        foreach (var path in remoteApks)
        {
            Write("APK: " + Path.GetFileName(path));
            Write(await Pull(d.Serial, path, Path.Combine(folder, "apk", Path.GetFileName(path)), ct));
        }
        if (withObb.Checked)
        {
            var root = "/sdcard/Android/obb/" + package;
            string found;
            try
            {
                found = await Adb(d.Serial, ct, "shell", "find", root, "-type", "f");
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                found = "";
                Write("OBB недоступны или отсутствуют: " + e.Message);
            }
            foreach (var path in found.Split('\n').Select(s => s.Trim()).Where(s => s.StartsWith(root + "/")))
            {
                if (!Regex.IsMatch(path, @"^/[A-Za-z0-9_./~+=-]+$"))
                    throw new Exception("Необычное имя OBB-файла; копирование остановлено.");
                var dest = Storage.Resolve(folder, "obb/" + path[(root.Length + 1)..]);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                Write("Данные: " + Path.GetFileName(path));
                Write(await Pull(d.Serial, path, dest, ct));
            }
        }
        // Reject a package updated during export so the backup cannot mix APK versions.
        if (pathsText != await Adb(d.Serial, ct, "shell", "pm", "path", package) || dump.Contains("versionCode=") && Regex.Match(dump, @"versionCode=\d+").Value != Regex.Match(await Adb(d.Serial, ct, "shell", "dumpsys", "package", package), @"versionCode=\d+").Value)
            throw new Exception("Приложение обновилось во время копирования. Повторите экспорт.");
        var apks = Directory.GetFiles(Path.Combine(folder, "apk"), "*.apk");
        if (!apks.Any(p => Path.GetFileName(p) == "base.apk"))
            throw new Exception("Нет base.apk в извлечённом пакете.");
        Stage("Проверка целостности", "Вычисление SHA-256 скопированных файлов.");
        var files = await Task.Run(() => Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Select(p => { ct.ThrowIfCancellationRequested(); return new SavedFile(Path.GetRelativePath(folder, p).Replace('\\', '/'), Storage.Hash(p), new FileInfo(p).Length); }).ToArray(), ct);
        var b = new Backup(package, Regex.Match(dump, @"versionName=([^\r\n]+)").Groups[1].Value.Trim(), Field("minSdk"), Field("targetSdk"), Storage.Abis(apks), files, DateTimeOffset.Now.ToString("O"));
        if (b.MinSdk == 0)
            throw new Exception("Не удалось определить minSdk.");
        // Write the manifest last: an interrupted export must not appear ready for installation.
        await File.WriteAllTextAsync(Path.Combine(folder, "backup.json"), JsonSerializer.Serialize(b, Storage.JsonOptions), ct);
        backup = b;
        backupFolder = folder;
        ShowBackup();
        transferCompleted = true;
        Detail("Копия сохранена. Подключите получателя и нажмите «Установить».");
        Write("Копия готова. Закрытые данные, сохранения и учётные записи не скопированы. APK сохранены с оригинальной подписью.");
    }
    async Task OpenBackup(CancellationToken ct)
    {
        using var dialog = new FolderBrowserDialog { Description = T("Выберите папку с backup.json") };
        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;
        Stage("Проверка резервной копии", "Сверка размеров и SHA-256 файлов.");
        Write("Проверка файлов и SHA-256…");
        var b = await Task.Run(() => Storage.Load(dialog.SelectedPath), ct);
        backup = b;
        backupFolder = dialog.SelectedPath;
        ShowBackup();
        Detail("Копия проверена. Выберите получателя и нажмите «Установить».");
        Write("Целостность копии подтверждена.");
    }
    void ShowBackup() => Summary($"{backup!.Package}\r\nВерсия {backup.Version}  •  Файлов: {backup.Files.Length}  •  {backup.Files.Sum(f => f.Size) / 1048576.0:F0} МБ\r\nАрхитектура: {(backup.Abis.Length == 0 ? "без нативных библиотек" : string.Join(", ", backup.Abis))}");
    /// <summary>Validates the backup and target, requests confirmation, installs APKs, then transfers OBB files.</summary>
    async Task Install(CancellationToken ct)
    {
        var d = Selected(target);
        var folder = backupFolder ?? throw new Exception("Сначала сохраните или откройте копию.");
        Stage("Проверка перед установкой", "Целостность копии и совместимость устройства.");
        Write("Проверка резервной копии…");
        var b = await Task.Run(() => Storage.Load(folder), ct);
        var sdk = int.Parse((await Adb(d.Serial, ct, "shell", "getprop", "ro.build.version.sdk")).Trim());
        var abis = (await Adb(d.Serial, ct, "shell", "getprop", "ro.product.cpu.abilist")).Trim().Split(',');
        var problem = Storage.Compatibility(b, sdk, abis);
        if (problem != null)
            throw new Exception(problem);
        var page = (await Adb(d.Serial, ct, "shell", "getconf", "PAGE_SIZE")).Trim();
        Write($"Устройство: API {sdk}, {string.Join(", ", abis)}, страницы {page} байт. Это предварительная проверка; запуск всё равно нужно проверить.");
        var space = await Adb(d.Serial, ct, "shell", "df", "-k", "/data");
        var spaceLine = space.Split('\n').LastOrDefault(s => Regex.IsMatch(s.Trim(), @"^/\S+\s+\d+\s+\d+\s+\d+"));
        if (spaceLine != null)
        {
            var columns = Regex.Split(spaceLine.Trim(), @"\s+");
            if (long.TryParse(columns[3], out var kb) && kb * 1024 < b.Files.Sum(f => f.Size) * 2)
                throw new Exception("Недостаточно свободного места с запасом для установки.");
        }
        var existing = await Adb(d.Serial, ct, "shell", "pm", "list", "packages", b.Package);
        var installed = existing.Split('\n').Any(s => s.Trim() == "package:" + b.Package);
        var warning = $"Установить {b.Package} v{b.Version} на {d.Model} ({d.Serial})?\n\n" +
            "Если такой пункт есть, включите:\nДля разработчиков → Установка через USB (Install via USB).\n\n" +
            "Разблокируйте устройство и подтвердите запрос установки на его экране.\n\n" +
            (installed ? "Приложение уже есть: будет выполнено обновление с сохранением данных, если подпись совпадает. OBB-файлы с теми же именами будут заменены.\n\n" : "") +
            "Закрытые данные и аккаунты не переносятся. Совместимость при запуске не гарантируется.";
        if (MessageBox.Show(this, T(warning), T("Установка"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            throw new OperationCanceledException();
        var args = new List<string> { "install-multiple" };
        if (installed)
            args.Add("-r");
        args.AddRange(b.Files.Where(f => f.Path.StartsWith("apk/")).Select(f => Storage.Resolve(folder, f.Path)));
        Write("Установка: держите устройство разблокированным и подтвердите запрос на его экране. Если в настройках есть «Установка через USB», включите её.");
        Stage("Установка — проверьте экран устройства", "Подтвердите запрос установки. Android не сообщает процент этого этапа.");
        Write(await Adb(d.Serial, ct, args.ToArray()));
        Write("APK установлены.");
        // OBB transfer follows installation; a later failure does not roll back the installed APK.
        foreach (var f in b.Files.Where(f => f.Path.StartsWith("obb/")))
        {
            var relative = f.Path[4..];
            if (!Regex.IsMatch(relative, @"^[A-Za-z0-9_./~+=-]+$"))
                throw new Exception("Необычное имя OBB; APK установлены, данные не перенесены.");
            var remote = "/sdcard/Android/obb/" + b.Package + "/" + relative;
            Stage("Передача игровых данных", $"{Path.GetFileName(relative)}  •  {f.Size / 1048576.0:F1} МБ");
            await Adb(d.Serial, ct, "shell", "mkdir", "-p", remote[..remote.LastIndexOf('/')]);
            Write(await Adb(d.Serial, ct, "push", Storage.Resolve(folder, f.Path), remote));
        }
        backup = b;
        transferCompleted = true;
        Detail("Приложение установлено. Нажмите «Запустить приложение».");
        Write("Перенос завершён. Нажмите «Запустить приложение» и проверьте работу на устройстве.");
    }
    /// <summary>Resolves and starts the installed launcher activity; the user verifies behavior on the device.</summary>
    async Task Launch(CancellationToken ct)
    {
        Stage("Запуск приложения", "Результат будет виден на экране устройства.");
        var d = Selected(target);
        var b = backup ?? throw new Exception("Откройте копию приложения.");
        var resolved = await Adb(d.Serial, ct, "shell", "cmd", "package", "resolve-activity", "--brief", b.Package);
        var component = resolved.Split('\n').Select(s => s.Trim()).LastOrDefault(s => s.StartsWith(b.Package + "/") && Regex.IsMatch(s, @"^[A-Za-z0-9_.$/]+$")) ?? throw new Exception("Нет запускаемой Activity. Проверьте, установлено ли приложение.");
        var result = await Adb(d.Serial, ct, "shell", "am", "start", "-W", "-n", component);
        Write(result);
        if (result.Contains("Error:") || !result.Contains("Status: ok"))
            throw new Exception("Android не подтвердил запуск приложения.");
        Write("Android подтвердил запуск Activity. Проверьте изображение, звук и управление на экране устройства.");
    }
}
