using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace WinServMgr
{
    // Owner-painted dropdown: no native bright combobox arrow or scroll track.
    // Four startup modes fit in a single fully dark popup.
    internal sealed class DarkStartupDropdown : Control
    {
        internal static readonly string[] Modes = {
            "自动", "自动（延迟启动）", "手动", "禁用"
        };

        private string selectedText = "";
        internal string SelectedText
        {
            get { return selectedText; }
            set
            {
                if (selectedText == (value ?? "")) return;
                selectedText = value ?? "";
                Invalidate();
                if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
            }
        }

        internal event EventHandler SelectionChanged;

        internal DarkStartupDropdown()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Font = new Font("Microsoft YaHei UI", 9F);
            Height = Math.Max(Font.Height + 10, 27);
            MinimumSize = new Size(110, Font.Height + 10);
            TabStop = true;
            Cursor = Cursors.Hand;
            BackColor = DarkTheme.Input;
            ForeColor = DarkTheme.TextColor;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            using (var background = new SolidBrush(Enabled ? DarkTheme.Input : DarkTheme.Surface))
                g.FillRectangle(background, ClientRectangle);
            using (var border = new Pen(Focused ? DarkTheme.Accent : DarkTheme.Border))
                g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
            var color = Enabled ? DarkTheme.TextColor : DarkTheme.Muted;
            var rect = new Rectangle(9, 1, Math.Max(0, Width - 38), Math.Max(0, Height - 2));
            TextRenderer.DrawText(g, selectedText, Font, rect, color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            int ax = Width - 19, ay = Height / 2 + 1;
            using (var brush = new SolidBrush(color))
                g.FillPolygon(brush, new Point[] {
                    new Point(ax - 5, ay - 3),
                    new Point(ax + 5, ay - 3),
                    new Point(ax, ay + 2)
                });
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            Focus();
            OpenMenu();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space ||
                e.KeyCode == Keys.F4 || (e.Alt && e.KeyCode == Keys.Down))
            {
                OpenMenu();
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            else if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                int index = Array.IndexOf(Modes, SelectedText);
                if (index < 0) index = 0;
                else index = Math.Max(0, Math.Min(Modes.Length - 1,
                    index + (e.KeyCode == Keys.Up ? -1 : 1)));
                SelectedText = Modes[index];
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
            base.OnKeyDown(e);
        }

        // A single menu belongs to this control. Never dispose it in Closed:
        // WinForms ModalMenuFilter still touches it after the Closed callback
        // when clicking outside, otherwise ObjectDisposedException terminates UI.
        private ContextMenuStrip dropdownMenu;

        private void OpenMenu()
        {
            if (!Enabled || IsDisposed || Disposing) return;

            if (dropdownMenu == null)
            {
                dropdownMenu = DarkTheme.CreateMenu();
                dropdownMenu.ShowImageMargin = false;
                foreach (string mode in Modes)
                {
                    string capturedMode = mode;
                    var item = new ToolStripMenuItem(mode) { AutoSize = true };
                    item.Click += (sender, args) => SelectedText = capturedMode;
                    dropdownMenu.Items.Add(item);
                }
            }

            dropdownMenu.MinimumSize = new Size(Width, 0);
            foreach (ToolStripMenuItem item in dropdownMenu.Items)
                item.Checked = string.Equals(item.Text, SelectedText, StringComparison.Ordinal);

            if (!dropdownMenu.Visible)
                dropdownMenu.Show(this, new Point(0, Height));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && dropdownMenu != null)
            {
                // The owner is being destroyed, not a popup-close notification.
                dropdownMenu.Dispose();
                dropdownMenu = null;
            }
            base.Dispose(disposing);
        }
    }

    // Multiline read-only text can scroll with the mouse wheel but never shows a scrollbar.
    internal sealed class WheelTextBox : TextBox
    {
        private const int WmMouseWheel = 0x020A;
        private const int EmLineScroll = 0x00B6;
        private int wheelRemainder;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hwnd, int message,
            IntPtr wParam, IntPtr lParam);

        internal WheelTextBox()
        {
            Multiline = true;
            ReadOnly = true;
            ScrollBars = ScrollBars.None;
            WordWrap = true;
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmMouseWheel && IsHandleCreated)
            {
                long raw = m.WParam.ToInt64();
                int delta = unchecked((short)((raw >> 16) & 0xffff));
                wheelRemainder += delta;
                int notches = wheelRemainder / 120;
                wheelRemainder %= 120;
                if (notches != 0)
                {
                    int lines = SystemInformation.MouseWheelScrollLines;
                    if (lines <= 0 || lines > 20) lines = 3;
                    SendMessage(Handle, EmLineScroll, IntPtr.Zero,
                        new IntPtr(-notches * lines));
                }
                return;
            }
            base.WndProc(ref m);
        }
    }
}
