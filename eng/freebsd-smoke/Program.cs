using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LibreWinForms.Platform;
using LibreWinForms.ProGPU;
using UiTimer = System.Windows.Forms.Timer;

const string KeepOpenEnvironmentVariable = "LIBREWINFORMS_COUNTER_KEEP_OPEN";

if (!OperatingSystem.IsFreeBSD())
{
    throw new PlatformNotSupportedException("This smoke is intended to run on FreeBSD.");
}

if (!RuntimeInformation.RuntimeIdentifier.StartsWith("freebsd.15-x64", StringComparison.Ordinal))
{
    throw new InvalidOperationException($"Unexpected FreeBSD runtime identifier: {RuntimeInformation.RuntimeIdentifier}");
}

if (typeof(Form).FullName != "System.Windows.Forms.Form")
{
    throw new InvalidOperationException("The canonical WinForms assembly did not load.");
}

using (LibrePlatformServices services = ProGpuPlatform.CreateServices())
{
    if (services.FileDialogs.GetType().FullName != typeof(ZenityLibreFileDialogService).FullName)
    {
        throw new InvalidOperationException(
            $"FreeBSD selected the wrong file-dialog service: {services.FileDialogs.GetType().FullName}");
    }

    LibreFileDialogRequest request = new(
        LibreFileDialogKind.OpenFile,
        "FreeBSD smoke",
        string.Empty,
        "/tmp",
        [],
        "txt",
        [new LibreFileDialogFilter("Text files", ["*.txt"])],
        1,
        LibreFileDialogOptions.None,
        null,
        [],
        null,
        default);
    ZenityLibreFileDialogService zenity = new(services.Dispatcher, new SmokeDialogRunner());
    LibreFileDialogResult result = zenity.Show(request);
    if (!result.Accepted || !result.SelectedPaths.SequenceEqual(["/tmp/freebsd-smoke.txt"]))
    {
        throw new InvalidOperationException("The FreeBSD Zenity adapter did not preserve the selected path.");
    }
}

ProGpuPlatform.Register();
bool keepOpen = Environment.GetEnvironmentVariable(KeepOpenEnvironmentVariable) == "1";
Form form = new()
{
    Text = "LibreWinForms Counter",
    Width = 580,
    Height = 520,
    Left = 660,
    Top = 100,
    StartPosition = FormStartPosition.Manual,
    BackColor = Color.FromArgb(38, 28, 52),
    ForeColor = Color.FromArgb(246, 242, 250),
    KeyPreview = true
};
int count = 0;
bool shown = false;
bool clicked = false;
bool closed = false;
bool firstPaintLogged = false;

Rectangle DecrementBounds() => new(form.ClientSize.Width / 2 - 160, 264, 150, 76);
Rectangle IncrementBounds() => new(form.ClientSize.Width / 2 + 10, 264, 150, 76);

void UpdateCount(int delta)
{
    count += delta;
    clicked = true;
    form.Invalidate();
}

void DrawCenteredText(Graphics graphics, string text, Font font, Brush brush, Rectangle bounds)
{
    SizeF size = graphics.MeasureString(text, font);
    graphics.DrawString(
        text,
        font,
        brush,
        bounds.X + (bounds.Width - size.Width) / 2,
        bounds.Y + (bounds.Height - size.Height) / 2);
}

form.Paint += (_, e) =>
{
    Graphics graphics = e.Graphics;
    int width = form.ClientSize.Width;
    int height = form.ClientSize.Height;
    graphics.Clear(form.BackColor);

    using SolidBrush panel = new(Color.FromArgb(51, 39, 69));
    using SolidBrush headingBrush = new(Color.FromArgb(232, 214, 255));
    using SolidBrush textBrush = new(Color.FromArgb(250, 247, 255));
    using SolidBrush mutedBrush = new(Color.FromArgb(192, 178, 211));
    using SolidBrush minusBrush = new(Color.FromArgb(140, 108, 213));
    using SolidBrush plusBrush = new(Color.FromArgb(255, 183, 76));
    using SolidBrush buttonTextBrush = new(Color.FromArgb(32, 22, 48));
    using Font headingFont = new(FontFamily.GenericSansSerif, 21, FontStyle.Bold);
    using Font valueFont = new(FontFamily.GenericSansSerif, 62, FontStyle.Bold);
    using Font buttonFont = new(FontFamily.GenericSansSerif, 32, FontStyle.Bold);
    using Font bodyFont = new(FontFamily.GenericSansSerif, 14, FontStyle.Regular);

    graphics.FillRectangle(panel, 24, 24, width - 48, height - 48);
    DrawCenteredText(graphics, "LibreWinForms Counter", headingFont, headingBrush,
        new Rectangle(40, 36, width - 80, 48));
    DrawCenteredText(graphics, "Current count", bodyFont, mutedBrush,
        new Rectangle(80, 105, width - 160, 32));
    DrawCenteredText(graphics, count.ToString(System.Globalization.CultureInfo.InvariantCulture),
        valueFont, textBrush, new Rectangle(80, 140, width - 160, 116));

    Rectangle decrement = DecrementBounds();
    Rectangle increment = IncrementBounds();
    graphics.FillRectangle(minusBrush, decrement);
    graphics.FillRectangle(plusBrush, increment);
    DrawCenteredText(graphics, "-", buttonFont, buttonTextBrush, decrement);
    DrawCenteredText(graphics, "+", buttonFont, buttonTextBrush, increment);
    DrawCenteredText(graphics, "Click a button or press + / -", bodyFont, mutedBrush,
        new Rectangle(48, 354, width - 96, 32));

    if (!firstPaintLogged)
    {
        firstPaintLogged = true;
        Console.WriteLine("LibreWinForms counter Paint event rendered its content.");
    }
};

form.MouseDown += (_, e) =>
{
    if (e.Button != MouseButtons.Left)
    {
        return;
    }

    Point position = new(e.X, e.Y);
    if (DecrementBounds().Contains(position))
    {
        UpdateCount(-1);
    }
    else if (IncrementBounds().Contains(position))
    {
        UpdateCount(1);
    }
};

form.KeyDown += (_, e) =>
{
    if (e.KeyCode is Keys.Add or Keys.Oemplus)
    {
        UpdateCount(1);
        e.Handled = true;
    }
    else if (e.KeyCode is Keys.Subtract or Keys.OemMinus)
    {
        UpdateCount(-1);
        e.Handled = true;
    }
    else if (e.KeyCode == Keys.Escape)
    {
        form.Close();
        e.Handled = true;
    }
};
form.Shown += (_, _) =>
{
    shown = true;
    form.Invalidate();
    form.Update();
    if (keepOpen)
    {
        Console.WriteLine("LibreWinForms Counter is open; use the +/- buttons or keys, then close from the title bar.");
    }
    else
    {
        UpdateCount(1);
    }
};
form.FormClosed += (_, _) => closed = true;

using (UiTimer timer = new() { Interval = 800 })
{
    if (!keepOpen)
    {
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            form.Close();
        };
        form.Shown += (_, _) => timer.Start();
    }
    Application.Run(form);
}

form.Dispose();
if (!shown || !closed || (!keepOpen && !clicked))
{
    throw new InvalidOperationException(
        $"WinForms lifecycle incomplete: shown={shown}, clicked={clicked}, closed={closed}.");
}

Console.WriteLine(
    keepOpen
        ? "LibreWinForms Counter closed."
        : $"LibreWinForms FreeBSD Form smoke passed ({RuntimeInformation.RuntimeIdentifier}).");

sealed class SmokeDialogRunner : ILibreDesktopDialogProcessRunner
{
    public LibreDesktopDialogProcessResult Run(string executable, IReadOnlyList<string> arguments)
    {
        if (executable != "zenity" || !arguments.Contains("--file-selection"))
        {
            throw new InvalidOperationException("Zenity was not invoked with the expected file-selection request.");
        }

        return new LibreDesktopDialogProcessResult(0, "/tmp/freebsd-smoke.txt\n", string.Empty);
    }
}
