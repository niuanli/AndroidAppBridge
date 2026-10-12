namespace AppBridge;

/// <summary>Padded package search with a drawn magnifier, clear action and keyboard support.</summary>
class SearchField : UserControl
{
    readonly TextBox input;
    readonly Button clear = new() { Text = "×", AccessibleName = "Очистить поиск", FlatStyle = FlatStyle.Flat, TabStop = true, BackColor = Color.White, ForeColor = Color.FromArgb(100, 116, 139), Margin = Padding.Empty, Padding = Padding.Empty, Visible = false };
    public SearchField(TextBox input)
    {
        this.input = input;
        DoubleBuffered = true;
        BackColor = Color.White;
        MinimumSize = new Size(180, 40);
        input.BorderStyle = BorderStyle.None;
        input.PlaceholderText = "Найти приложение…";
        input.AccessibleName = "Поиск приложения по имени пакета";
        clear.FlatAppearance.BorderSize = 0;
        clear.FlatAppearance.MouseOverBackColor = Color.FromArgb(237, 242, 249);
        Controls.Add(input);
        Controls.Add(clear);
        input.TextChanged += (_, _) =>
        {
            clear.Visible = input.Text.Length > 0;
            Arrange();
        };
        input.Enter += (_, _) => Invalidate();
        input.Leave += (_, _) => Invalidate();
        input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
            {
                input.Clear();
                e.SuppressKeyPress = true;
            }
        };
        clear.Click += (_, _) =>
        {
            input.Clear();
            input.Focus();
        };
        Click += (_, _) => input.Focus();
    }
    // Keep the native text box vertically centered as the font and monitor scale change.
    void Arrange()
    {
        if (input == null)
            return;
        var inset = Font.Height + 20;
        var buttonWidth = Font.Height + 18;
        input.SetBounds(inset, Math.Max(3, (Height - input.PreferredHeight) / 2), Math.Max(1, Width - inset - (clear.Visible ? buttonWidth + 8 : 12)), input.PreferredHeight);
        clear.SetBounds(Width - buttonWidth - 5, 4, buttonWidth, Math.Max(1, Height - 8));
        Invalidate();
    }
    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Arrange();
    }
    protected override void OnFontChanged(EventArgs e)
    {
        base.OnFontChanged(e);
        Arrange();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var border = new Pen(input.Focused ? Color.FromArgb(41, 99, 218) : Color.FromArgb(206, 216, 230), input.Focused ? 2 : 1);
        using var outline = new System.Drawing.Drawing2D.GraphicsPath();
        const int radius = 8;
        outline.AddArc(1, 1, radius * 2, radius * 2, 180, 90);
        outline.AddArc(Width - radius * 2 - 2, 1, radius * 2, radius * 2, 270, 90);
        outline.AddArc(Width - radius * 2 - 2, Height - radius * 2 - 2, radius * 2, radius * 2, 0, 90);
        outline.AddArc(1, Height - radius * 2 - 2, radius * 2, radius * 2, 90, 90);
        outline.CloseFigure();
        e.Graphics.DrawPath(border, outline);
        using var glass = new Pen(Color.FromArgb(113, 129, 150), 1.7f);
        var r = Math.Max(5, Font.Height / 3f);
        var x = 17f;
        var y = Height / 2f - 2;
        e.Graphics.DrawEllipse(glass, x - r, y - r, r * 2, r * 2);
        e.Graphics.DrawLine(glass, x + r * .7f, y + r * .7f, x + r * 1.5f, y + r * 1.5f);
    }
}
