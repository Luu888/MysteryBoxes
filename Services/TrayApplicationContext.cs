
using System.Drawing;
using System.Windows.Forms;

public sealed class TrayApplicationContext : ApplicationContext
{
    private readonly WebApplication _app;
    private readonly NotifyIcon _notifyIcon;
    private bool _isShuttingDown;

    public TrayApplicationContext(WebApplication app)
    {
        _app = app;

        var menu = new ContextMenuStrip();

        var infoItem = new ToolStripMenuItem("MysteryBoxes");
        infoItem.Enabled = false;

        var closeItem = new ToolStripMenuItem("Zamknij MysteryBoxes");
        closeItem.Click += async (_, _) => await ShutdownAsync();

        menu.Items.Add(infoItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(closeItem);

        var icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath)
                   ?? SystemIcons.Application;

        _notifyIcon = new NotifyIcon
        {
            Icon = icon,
            Text = "MysteryBoxes – działa",
            ContextMenuStrip = menu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) =>
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "http://localhost:57286",
                UseShellExecute = true
            });
        };
    }

    private async Task ShutdownAsync()
    {
        if (_isShuttingDown)
            return;

        _isShuttingDown = true;

        _notifyIcon.Visible = false;

        await _app.StopAsync();
        await _app.DisposeAsync();

        _notifyIcon.Dispose();

        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _notifyIcon.Dispose();

        base.Dispose(disposing);
    }
}
