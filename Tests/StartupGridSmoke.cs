using System;
using System.Collections.Generic;
using System.Reflection;
using System.ServiceProcess;
using System.Windows.Forms;

internal static class StartupGridSmoke
{
    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            using (var main = new WinServMgr.MainForm())
            {
                var gridField = typeof(WinServMgr.MainForm).GetField(
                    "dgvServicesList", BindingFlags.Instance | BindingFlags.NonPublic);
                var placeholderField = typeof(WinServMgr.MainForm).GetField(
                    "loadingPlaceholder", BindingFlags.Instance | BindingFlags.NonPublic);
                var apply = typeof(WinServMgr.MainForm).GetMethod(
                    "ApplySnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
                var reveal = typeof(WinServMgr.MainForm).GetMethod(
                    "RevealFirstSnapshot", BindingFlags.Instance | BindingFlags.NonPublic);
                Check(gridField != null && placeholderField != null &&
                    apply != null && reveal != null, "Expected startup grid members");

                var grid = (DataGridView)gridField.GetValue(main);
                var placeholder = (Label)placeholderField.GetValue(main);
                Check(grid.DataSource == null, "Grid must not bind empty data at startup");
                Check(grid.GetType().Name == "BufferedDataGridView",
                    "Buffered grid subclass must be in use");
                var buffered = typeof(Control).GetProperty("DoubleBuffered",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Check(buffered != null && (bool)buffered.GetValue(grid, null),
                    "DataGridView must enable double buffering");
                Check(placeholder.Text.Contains("读取"),
                    "Show a useful loading indicator");

                var rows = new List<WinServMgr.ServiceEntry> {
                    new WinServMgr.ServiceEntry("zz_test", "测试服务", ServiceControllerStatus.Running),
                    new WinServMgr.ServiceEntry("aa_test", "Test display name", ServiceControllerStatus.Stopped)
                };
                apply.Invoke(main, new object[] { rows });
                Check(grid.DataSource == null && grid.RowCount == 0,
                    "Snapshot must be prepared before binding");

                reveal.Invoke(main, new object[0]);
                Check(grid.DataSource != null && grid.RowCount == 2,
                    "Complete rows must bind together on reveal");
                Check(!placeholder.Visible, "Loading indicator hidden after reveal");
                Check((string)grid.Rows[0].Cells["ServiceName"].Value == "aa_test",
                    "Initial sort must be applied before reveal");
            }
            Console.WriteLine("PASS: deferred first binding; two complete sorted rows; double-buffer enabled");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error);
            return 1;
        }
    }

    private static void Check(bool ok, string description)
    {
        if (!ok) throw new InvalidOperationException(description);
    }
}
