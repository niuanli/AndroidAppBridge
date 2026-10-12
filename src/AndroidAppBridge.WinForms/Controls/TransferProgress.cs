namespace AndroidAppBridge.Presentation;

/// <summary>Draws determinate file progress or an animated waiting indicator independently of Windows themes.</summary>
class TransferProgress : Control
{
    readonly System.Windows.Forms.Timer animation = new() { Interval = 40 };
    ProgressBarStyle style = ProgressBarStyle.Continuous;
    int value, offset;
    public ProgressBarStyle Style
    {
        get => style;
        set
        {
            style = value;
            animation.Enabled = value == ProgressBarStyle.Marquee;
            Invalidate();
        }
    }
    public int Value
    {
        get => value;
        set
        {
            this.value = Math.Clamp(value, 0, 100);
            Invalidate();
        }
    }
    public TransferProgress()
    {
        DoubleBuffered = true;
        animation.Tick += (_, _) =>
        {
            offset = (offset + 8) % Math.Max(1, Width + Width / 4);
            Invalidate();
        };
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        using var track = new SolidBrush(Color.FromArgb(226, 234, 245));
        using var fill = new SolidBrush(Color.FromArgb(41, 99, 218));
        e.Graphics.FillRectangle(track, ClientRectangle);
        var rect = style == ProgressBarStyle.Marquee ? new Rectangle(offset - Width / 4, 0, Width / 4, Height) : new Rectangle(0, 0, (int)((long)Width * value / 100), Height);
        e.Graphics.FillRectangle(fill, rect);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            animation.Dispose();
        base.Dispose(disposing);
    }
}
