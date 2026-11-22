using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal static class DropdownDismissalSmoke
{
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(
        IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

    [STAThread]
    private static int Main()
    {
        try
        {
            Application.EnableVisualStyles();
            using (var form = new Form())
            {
                form.Width = 500;
                form.Height = 300;
                var type = typeof(WinServMgr.MainForm).Assembly.GetType(
                    "WinServMgr.DarkStartupDropdown", true);
                var control = (Control)Activator.CreateInstance(type, true);
                control.Left = 15;
                control.Top = 15;
                control.Width = 240;
                control.Enabled = true;
                form.Controls.Add(control);
                form.Show();
                Application.DoEvents();

                var click = type.GetMethod("OnClick",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var menuField = type.GetField("dropdownMenu",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert(click != null && menuField != null, "Dropdown reflection");

                click.Invoke(control, new object[] { EventArgs.Empty });
                Application.DoEvents();
                var menu = (ContextMenuStrip)menuField.GetValue(control);
                Assert(menu != null && menu.Visible, "Menu opens");

                // Simulate clicking an empty part of the host form.
                var location = new IntPtr((120 << 16) | 15);
                PostMessage(form.Handle, 0x0201, IntPtr.Zero, location); // WM_LBUTTONDOWN
                PostMessage(form.Handle, 0x0202, IntPtr.Zero, location); // WM_LBUTTONUP
                Application.DoEvents();
                if (menu.Visible)
                {
                    menu.Close(ToolStripDropDownCloseReason.AppClicked);
                    Application.DoEvents();
                }
                Assert(!menu.IsDisposed, "Menu survives outside-click dismissal");

                // Re-open, select a value and close again: the same menu must work.
                click.Invoke(control, new object[] { EventArgs.Empty });
                Application.DoEvents();
                Assert(ReferenceEquals(menu, menuField.GetValue(control)) &&
                    menu.Visible, "Reused menu opens");
                ((ToolStripMenuItem)menu.Items[2]).PerformClick();
                menu.Close(ToolStripDropDownCloseReason.ItemClicked);
                Application.DoEvents();
                var selection = type.GetProperty("SelectedText",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                Assert(selection != null &&
                    (string)selection.GetValue(control, null) == "手动",
                    "Selection updates");
                Assert(!menu.IsDisposed, "Menu survives selection dismissal");

                form.Close();
                Application.DoEvents();
                Assert(menu.IsDisposed, "Menu disposed with owner");
            }
            Console.WriteLine("PASS: outside-click dismissal, reopen, selection and owner disposal");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static void Assert(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
    }
}
