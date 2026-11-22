using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace WinServMgr
{
    /// <summary>FGS-derived WinForms palette, with dark menus, grid and title bar.</summary>
    internal static class DarkTheme
    {
        internal static readonly Color Surface = Color.FromArgb(37, 38, 40);
        internal static readonly Color Input = Color.FromArgb(27, 28, 30);
        internal static readonly Color TextColor = Color.FromArgb(235, 237, 240);
        internal static readonly Color Muted = Color.FromArgb(162, 167, 174);
        internal static readonly Color Accent = Color.FromArgb(0, 151, 111);
        internal static readonly Color Border = Color.FromArgb(64, 66, 71);
        internal static readonly Color Hover = Color.FromArgb(53, 56, 60);
        internal static readonly Color Selected = Color.FromArgb(35, 78, 66);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string subAppName, string subIdList);

        internal static void Apply(Form form)
        {
            form.BackColor = Surface;
            form.ForeColor = TextColor;
            ApplyControls(form);
            form.HandleCreated += (sender, args) => ApplyTitleBar(form);
            if (form.IsHandleCreated) ApplyTitleBar(form);
        }

        private static void ApplyTitleBar(Form form)
        {
            try
            {
                int enabled = 1;
                // DWMWA_USE_IMMERSIVE_DARK_MODE, Windows 10/11 (20; legacy 19).
                if (DwmSetWindowAttribute(form.Handle, 20, ref enabled, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref enabled, sizeof(int));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        private static void ApplyControls(Control root)
        {
            foreach (Control child in root.Controls)
            {
                child.ForeColor = child is Label && child.ForeColor.ToArgb() == Muted.ToArgb()
                    ? Muted : TextColor;
                child.BackColor = Surface;

                var text = child as TextBoxBase;
                if (text != null)
                {
                    text.BackColor = Input;
                    text.BorderStyle = BorderStyle.FixedSingle;
                }

                var button = child as Button;
                if (button != null)
                {
                    button.BackColor = Input;
                    button.FlatStyle = FlatStyle.Flat;
                    button.UseVisualStyleBackColor = false;
                    button.FlatAppearance.BorderSize = 1;
                    button.FlatAppearance.BorderColor = Border;
                    button.FlatAppearance.MouseOverBackColor = Hover;
                    button.FlatAppearance.MouseDownBackColor = Selected;
                }

                var checkBox = child as CheckBox;
                if (checkBox != null)
                {
                    checkBox.FlatStyle = FlatStyle.Flat;
                    checkBox.FlatAppearance.BorderColor = Border;
                }

                var grid = child as DataGridView;
                if (grid != null) ApplyGrid(grid);

                ApplyControls(child);
            }
        }

        private static void ApplyGrid(DataGridView grid)
        {
            grid.BackgroundColor = Input;
            grid.BorderStyle = BorderStyle.FixedSingle;
            grid.GridColor = Border;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.RowHeadersVisible = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Surface;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = TextColor;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Surface;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextColor;
            grid.DefaultCellStyle.BackColor = Input;
            grid.DefaultCellStyle.ForeColor = TextColor;
            grid.DefaultCellStyle.SelectionBackColor = Selected;
            grid.DefaultCellStyle.SelectionForeColor = TextColor;
            grid.RowsDefaultCellStyle.BackColor = Input;
            grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(32, 33, 35);
            grid.RowsDefaultCellStyle.ForeColor = TextColor;
            grid.RowsDefaultCellStyle.SelectionBackColor = Selected;
            grid.RowsDefaultCellStyle.SelectionForeColor = TextColor;
            grid.AlternatingRowsDefaultCellStyle.SelectionBackColor = Selected;
            grid.AlternatingRowsDefaultCellStyle.SelectionForeColor = TextColor;
            EnableNativeScrollbars(grid);
        }

        // The same native DarkMode_Explorer scrollbar policy used by FGS.
        internal static void EnableNativeScrollbars(Control root)
        {
            root.HandleCreated += (sender, args) => ApplyNativeTheme((Control)sender);
            root.ControlAdded += (sender, args) => EnableNativeScrollbars(args.Control);
            if (root.IsHandleCreated) ApplyNativeTheme(root);
            foreach (Control child in root.Controls) EnableNativeScrollbars(child);
        }

        private static void ApplyNativeTheme(Control control)
        {
            try { SetWindowTheme(control.Handle, "DarkMode_Explorer", null); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }

        internal static ContextMenuStrip CreateMenu()
        {
            var menu = new ContextMenuStrip();
            menu.ShowImageMargin = false;
            menu.BackColor = Input;
            menu.ForeColor = TextColor;
            menu.Renderer = new ToolStripProfessionalRenderer(new DarkColorTable());
            menu.Font = new Font("Microsoft YaHei UI", 9F);
            return menu;
        }

        internal static void ShowError(IWin32Window owner, string message)
        {
            using (var dialog = new Form())
            {
                dialog.Text = "操作失败";
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ClientSize = new Size(510, 190);
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                dialog.ShowInTaskbar = false;

                var body = new TextBox {
                    Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
                    ScrollBars = ScrollBars.Vertical, Text = message,
                    Margin = new Padding(14), Font = new Font("Microsoft YaHei UI", 9F)
                };
                var close = new Button {
                    Text = "关闭", DialogResult = DialogResult.OK,
                    Size = new Size(96, 32), Anchor = AnchorStyles.Right | AnchorStyles.Top
                };
                var footer = new FlowLayoutPanel {
                    Dock = DockStyle.Bottom, Height = 52, FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(14, 8, 14, 8)
                };
                footer.Controls.Add(close);
                dialog.Controls.Add(body);
                dialog.Controls.Add(footer);
                dialog.AcceptButton = close;
                DarkTheme.Apply(dialog);
                dialog.ShowDialog(owner);
            }
        }

        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Input; } }
            public override Color MenuBorder { get { return Border; } }
            public override Color MenuItemBorder { get { return Accent; } }
            public override Color MenuItemSelected { get { return Selected; } }
            public override Color MenuItemSelectedGradientBegin { get { return Selected; } }
            public override Color MenuItemSelectedGradientEnd { get { return Selected; } }
            public override Color MenuItemPressedGradientBegin { get { return Surface; } }
            public override Color MenuItemPressedGradientMiddle { get { return Surface; } }
            public override Color MenuItemPressedGradientEnd { get { return Surface; } }
            public override Color ImageMarginGradientBegin { get { return Input; } }
            public override Color ImageMarginGradientMiddle { get { return Input; } }
            public override Color ImageMarginGradientEnd { get { return Input; } }
            public override Color SeparatorDark { get { return Border; } }
            public override Color SeparatorLight { get { return Border; } }
        }
    }
}
