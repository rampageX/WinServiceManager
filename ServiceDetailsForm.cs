using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace WinServMgr
{
    internal sealed class ServiceDetailsForm : Form
    {
        private readonly string serviceName;
        private readonly Dictionary<string, TextBox> fields =
            new Dictionary<string, TextBox>();
        private readonly DarkStartupDropdown startupBox;
        private readonly Label statusLabel;
        private readonly Button confirmButton;
        private readonly Button cancelButton;
        private string originalDisplayName;
        private string originalStartup;
        private bool saving;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegLoadMUIStringW")]
        private static extern int RegLoadMUIString(SafeRegistryHandle hKey,
            string valueName, StringBuilder output, int outputBytes,
            out int requiredBytes, int flags, string directory);

        internal ServiceDetailsForm(string name)
        {
            serviceName = name;
            Text = "服务详情  ·  " + name;
            Font = new Font("Microsoft YaHei UI", 9F);
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = FormBorderStyle.Sizable;
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(800, 690);
            MinimumSize = new Size(700, 630);

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                Padding = Padding.Empty, Margin = Padding.Empty,
                AutoScroll = false, BackColor = DarkTheme.Surface
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));

            var table = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 10,
                Padding = new Padding(18, 14, 18, 8), Margin = Padding.Empty,
                AutoScroll = false, BackColor = DarkTheme.Surface
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 7; ++i)
                table.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 44));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 28));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 28));

            AddSingleField(table, 0, "服务名称", false);
            AddSingleField(table, 1, "显示名称", true);
            AddSingleField(table, 2, "当前状态", false);
            startupBox = new DarkStartupDropdown {
                Name = "startupType", Dock = DockStyle.Fill,
                Margin = new Padding(4, 4, 0, 5),
                Enabled = false
            };
            table.Controls.Add(AddCaption("启动类型"), 0, 3);
            table.Controls.Add(startupBox, 1, 3);
            AddSingleField(table, 4, "服务类型", false);
            AddSingleField(table, 5, "登录账户", false);
            AddSingleField(table, 6, "可执行文件路径", false);
            AddMultiField(table, 7, "服务描述");
            AddMultiField(table, 8, "依赖的服务");
            AddMultiField(table, 9, "依赖本服务的服务");

            var footer = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1,
                Margin = Padding.Empty, Padding = new Padding(18, 8, 18, 9),
                BackColor = DarkTheme.Surface
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            statusLabel = new Label {
                Text = "正在读取服务信息…", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = DarkTheme.Muted
            };
            confirmButton = new Button {
                Text = "确定", Name = "btnConfirm", Enabled = false,
                Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0)
            };
            cancelButton = new Button {
                Text = "取消", Name = "btnCancel",
                DialogResult = DialogResult.Cancel,
                Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0)
            };
            confirmButton.Click += async (sender, args) => await SaveChangesAsync();
            cancelButton.Click += (sender, args) => { if (!saving) Close(); };
            footer.Controls.Add(statusLabel, 0, 0);
            footer.Controls.Add(confirmButton, 1, 0);
            footer.Controls.Add(cancelButton, 2, 0);

            root.Controls.Add(table, 0, 0);
            root.Controls.Add(footer, 0, 1);
            Controls.Add(root);
            AcceptButton = confirmButton;
            CancelButton = cancelButton;
            DarkTheme.Apply(this);
            statusLabel.ForeColor = DarkTheme.Muted;
            fields["显示名称"].ReadOnly = false;
            fields["显示名称"].MaxLength = 256;
            fields["显示名称"].Enabled = false;

            FormClosing += (sender, args) => { if (saving) args.Cancel = true; };
            Shown += async (sender, args) => await LoadDetailsAsync();
        }

        private static Label AddCaption(string label)
        {
            return new Label {
                Text = label, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 0, 10, 0)
            };
        }

        private void AddSingleField(TableLayoutPanel table,
            int row, string title, bool editable)
        {
            var field = new TextBox {
                Name = "field" + row,
                Font = Font, Multiline = false,
                ReadOnly = !editable,
                Anchor = AnchorStyles.Left | AnchorStyles.Right,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(4, 0, 0, 0),
                Text = "—"
            };
            table.Controls.Add(AddCaption(title), 0, row);
            table.Controls.Add(field, 1, row);
            fields[title] = field;
        }

        private void AddMultiField(TableLayoutPanel table, int row, string title)
        {
            var field = new WheelTextBox {
                Name = "field" + row, Font = Font,
                Dock = DockStyle.Fill,
                Multiline = true, ReadOnly = true, WordWrap = true,
                ScrollBars = ScrollBars.None,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(4, 3, 0, 3),
                Text = "—"
            };
            table.Controls.Add(AddCaption(title), 0, row);
            table.Controls.Add(field, 1, row);
            fields[title] = field;
        }

        private async Task LoadDetailsAsync()
        {
            confirmButton.Enabled = false;
            fields["显示名称"].Enabled = false;
            startupBox.Enabled = false;
            statusLabel.Text = "正在读取服务信息…";
            try
            {
                var details = await Task.Run(() => ReadServiceDetails(serviceName));
                if (IsDisposed || Disposing) return;
                foreach (var pair in details.Values)
                {
                    TextBox field;
                    if (fields.TryGetValue(pair.Key, out field))
                        field.Text = string.IsNullOrWhiteSpace(pair.Value) ? "—" : pair.Value;
                }

                originalDisplayName = details.Values["显示名称"];
                originalStartup = details.Values["启动类型"];
                startupBox.SelectedText = originalStartup;
                startupBox.Enabled = details.StartupEditable;
                fields["显示名称"].Enabled = true;
                confirmButton.Enabled = true;
                statusLabel.Text = details.StartupEditable
                    ? "仅显示名称和启动类型可编辑"
                    : "此服务启动类型只读；显示名称可以修改";
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;
                statusLabel.Text = "读取失败";
                DarkTheme.ShowError(this, "读取服务详情失败：\r\n" + ex.Message);
            }
        }

        private async Task SaveChangesAsync()
        {
            if (saving || originalDisplayName == null) return;
            string proposedName = fields["显示名称"].Text.Trim();
            if (proposedName.Length == 0 || proposedName.Length > 256 ||
                proposedName.Any(c => char.IsControl(c)))
            {
                DarkTheme.ShowError(this, "显示名称必须为 1～256 个字符，且不能包含换行或控制字符。");
                fields["显示名称"].Focus();
                return;
            }

            string proposedStartup = startupBox.SelectedText;
            string changedName = string.Equals(proposedName, originalDisplayName,
                StringComparison.Ordinal) ? null : proposedName;
            string changedStartup = startupBox.Enabled &&
                !string.Equals(proposedStartup, originalStartup, StringComparison.Ordinal)
                ? proposedStartup : null;

            if (changedName == null && changedStartup == null)
            {
                Close();
                return;
            }
            if (changedStartup == "禁用" && !ConfirmDisable(this, serviceName))
                return;

            saving = true;
            confirmButton.Enabled = false;
            cancelButton.Enabled = false;
            fields["显示名称"].Enabled = false;
            startupBox.Enabled = false;
            statusLabel.Text = "正在保存服务配置…";
            try
            {
                await Task.Run(() => ServiceConfiguration.Save(
                    serviceName, changedName, changedStartup));
                if (IsDisposed || Disposing) return;
                CloseAfterSave();
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;
                statusLabel.Text = "保存失败，请核对实际配置";
                DarkTheme.ShowError(this, "保存服务配置失败：\r\n" + ex.Message);
            }
            finally
            {
                saving = false;
                if (!IsDisposed && !Disposing && Visible)
                {
                    // A two-stage SCM update may have partially succeeded.
                    // Always reload authoritative state rather than leaving stale fields.
                    await LoadDetailsAsync();
                    cancelButton.Enabled = true;
                }
            }
        }

        private void CloseAfterSave()
        {
            saving = false;
            statusLabel.Text = "保存成功";
            DialogResult = DialogResult.OK;
            // This form is shown modelessly by the main window.
            Close();
        }

        private static bool ConfirmDisable(IWin32Window owner, string name)
        {
            using (var dialog = new Form())
            {
                dialog.Text = "确认禁用服务";
                dialog.Font = new Font("Microsoft YaHei UI", 9F);
                dialog.ClientSize = new Size(480, 168);
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.ShowInTaskbar = false;
                dialog.MinimizeBox = false;
                dialog.MaximizeBox = false;
                var message = new Label {
                    Text = "即将禁用服务：" + name +
                        "\r\n\r\n禁用可能使系统功能或依赖该服务的软件无法正常工作。" +
                        "\r\n更改不会立即停止已在运行的服务。",
                    Dock = DockStyle.Fill,
                    Padding = new Padding(16, 14, 16, 2),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var footer = new FlowLayoutPanel {
                    Dock = DockStyle.Bottom, Height = 50,
                    FlowDirection = FlowDirection.RightToLeft,
                    Padding = new Padding(12, 7, 12, 7)
                };
                var no = new Button {
                    Text = "取消", DialogResult = DialogResult.No,
                    Size = new Size(94, 30)
                };
                var yes = new Button {
                    Text = "确认禁用", DialogResult = DialogResult.Yes,
                    Size = new Size(100, 30)
                };
                footer.Controls.Add(no);
                footer.Controls.Add(yes);
                dialog.Controls.Add(message);
                dialog.Controls.Add(footer);
                dialog.AcceptButton = no;
                dialog.CancelButton = no;
                DarkTheme.Apply(dialog);
                return dialog.ShowDialog(owner) == DialogResult.Yes;
            }
        }

        private sealed class ServiceSnapshot
        {
            internal Dictionary<string, string> Values;
            internal bool StartupEditable;
        }

        private static ServiceSnapshot ReadServiceDetails(string name)
        {
            var data = new Dictionary<string, string> {
                { "服务名称", name },
                { "显示名称", "" },
                { "当前状态", "" },
                { "启动类型", "" },
                { "服务类型", "" },
                { "登录账户", "" },
                { "可执行文件路径", "" },
                { "服务描述", "" },
                { "依赖的服务", "" },
                { "依赖本服务的服务", "" }
            };
            bool driverService;
            using (var service = new ServiceController(name))
            {
                service.Refresh();
                data["显示名称"] = service.DisplayName;
                data["当前状态"] = TranslateStatus(service.Status);
                data["服务类型"] = service.ServiceType.ToString();
                driverService = (service.ServiceType &
                    (ServiceType.KernelDriver | ServiceType.FileSystemDriver)) != 0;
                try
                {
                    data["依赖的服务"] = JoinServiceNames(service.ServicesDependedOn);
                    data["依赖本服务的服务"] = JoinServiceNames(service.DependentServices);
                }
                catch (InvalidOperationException) { }
            }

            using (var registry = Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Services\" + name, false))
            {
                if (registry != null)
                {
                    data["可执行文件路径"] = ReadRegistryValue(registry, "ImagePath");
                    data["登录账户"] = ReadRegistryValue(registry, "ObjectName");
                    data["服务描述"] = ReadDescription(registry);
                    int start = ConvertToInt(registry.GetValue("Start"), -1);
                    data["启动类型"] = TranslateStartType(start);
                    if (start == 2 && ConvertToInt(
                        registry.GetValue("DelayedAutoStart"), 0) == 1)
                        data["启动类型"] = "自动（延迟启动）";
                }
            }
            return new ServiceSnapshot {
                Values = data,
                StartupEditable = !driverService &&
                    DarkStartupDropdown.Modes.Contains(data["启动类型"])
            };
        }

        private static string JoinServiceNames(ServiceController[] services)
        {
            if (services == null || services.Length == 0) return "无";
            var names = new List<string>();
            foreach (var service in services)
            {
                using (service)
                {
                    try { names.Add(service.DisplayName + " (" + service.ServiceName + ")"); }
                    catch (InvalidOperationException) { }
                }
            }
            return string.Join(Environment.NewLine,
                names.OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase));
        }

        private static string ReadRegistryValue(RegistryKey key, string name)
        {
            object raw = key.GetValue(name, "");
            var strings = raw as string[];
            if (strings != null) return string.Join(Environment.NewLine, strings);
            return raw == null ? "" : raw.ToString();
        }

        private static string ReadDescription(RegistryKey key)
        {
            string raw = ReadRegistryValue(key, "Description");
            if (!raw.StartsWith("@", StringComparison.Ordinal)) return raw;
            try
            {
                var output = new StringBuilder(8192);
                int requiredBytes;
                if (RegLoadMUIString(key.Handle, "Description",
                    output, output.Capacity * 2, out requiredBytes, 0, null) == 0 &&
                    output.Length > 0)
                    return output.ToString();
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            return raw;
        }

        private static int ConvertToInt(object value, int fallback)
        {
            if (value == null) return fallback;
            try { return Convert.ToInt32(value); }
            catch { return fallback; }
        }

        private static string TranslateStartType(int start)
        {
            switch (start)
            {
                case 0: return "引导";
                case 1: return "系统";
                case 2: return "自动";
                case 3: return "手动";
                case 4: return "禁用";
                default: return "未知";
            }
        }

        private static string TranslateStatus(ServiceControllerStatus status)
        {
            switch (status)
            {
                case ServiceControllerStatus.Running: return "运行中";
                case ServiceControllerStatus.Stopped: return "已停止";
                case ServiceControllerStatus.StartPending: return "正在启动";
                case ServiceControllerStatus.StopPending: return "正在停止";
                case ServiceControllerStatus.Paused: return "已暂停";
                case ServiceControllerStatus.PausePending: return "正在暂停";
                case ServiceControllerStatus.ContinuePending: return "正在继续";
                default: return status.ToString();
            }
        }
    }
}
