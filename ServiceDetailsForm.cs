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
        private readonly Dictionary<string, TextBox> valueFields =
            new Dictionary<string, TextBox>();
        private readonly Label loadingLabel;

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegLoadMUIStringW")]
        private static extern int RegLoadMUIString(SafeRegistryHandle hKey, string valueName,
            StringBuilder output, int outputBytes, out int requiredBytes,
            int flags, string directory);

        internal ServiceDetailsForm(string name)
        {
            serviceName = name;
            Text = "服务详情  ·  " + name;
            Font = new Font("Microsoft YaHei UI", 9F);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(650, 490);
            ClientSize = new Size(800, 620);
            AutoScaleMode = AutoScaleMode.Dpi;

            var content = new Panel {
                Dock = DockStyle.Fill, AutoScroll = true, BackColor = DarkTheme.Surface
            };
            var table = new TableLayoutPanel {
                Dock = DockStyle.Top, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = 2, RowCount = 0,
                Padding = new Padding(18, 18, 18, 12),
                BackColor = DarkTheme.Surface
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 142));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            AddField(table, "服务名称", 38);
            AddField(table, "显示名称", 38);
            AddField(table, "当前状态", 38);
            AddField(table, "启动类型", 38);
            AddField(table, "服务类型", 38);
            AddField(table, "登录账户", 38);
            AddField(table, "可执行文件路径", 65);
            AddField(table, "服务描述", 108);
            AddField(table, "依赖的服务", 80);
            AddField(table, "依赖本服务的服务", 80);

            content.Controls.Add(table);
            var bottom = new TableLayoutPanel {
                Dock = DockStyle.Bottom, Height = 54,
                ColumnCount = 2, RowCount = 1, Padding = new Padding(14, 8, 18, 8)
            };
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            loadingLabel = new Label {
                Text = "正在读取服务信息…", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = DarkTheme.Muted
            };
            var close = new Button {
                Text = "关闭", DialogResult = DialogResult.OK,
                Dock = DockStyle.Fill
            };
            bottom.Controls.Add(loadingLabel, 0, 0);
            bottom.Controls.Add(close, 1, 0);
            Controls.Add(content);
            Controls.Add(bottom);
            AcceptButton = close;
            DarkTheme.Apply(this);
            loadingLabel.ForeColor = DarkTheme.Muted;
            Shown += async (sender, args) => await LoadDetailsAsync();
        }

        private void AddField(TableLayoutPanel table, string label, int height)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, height + 8));
            var caption = new Label {
                Text = label, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft,
                Padding = new Padding(0, 9, 0, 0)
            };
            var value = new TextBox {
                ReadOnly = true, Multiline = true,
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = height > 60 ? ScrollBars.Vertical : ScrollBars.None,
                Text = "—", Margin = new Padding(4, 2, 0, 6),
                Padding = new Padding(4)
            };
            table.Controls.Add(caption, 0, row);
            table.Controls.Add(value, 1, row);
            valueFields[label] = value;
        }

        private async Task LoadDetailsAsync()
        {
            try
            {
                var details = await Task.Run(() => ReadServiceDetails(serviceName));
                if (IsDisposed || Disposing) return;
                foreach (var pair in details)
                {
                    TextBox box;
                    if (valueFields.TryGetValue(pair.Key, out box))
                        box.Text = string.IsNullOrWhiteSpace(pair.Value) ? "—" : pair.Value;
                }
                loadingLabel.Text = "只读信息 · 注册表与服务控制管理器";
            }
            catch (Exception ex)
            {
                if (IsDisposed || Disposing) return;
                loadingLabel.Text = "读取部分信息失败";
                DarkTheme.ShowError(this, "服务详情读取失败：\r\n" + ex.Message);
            }
        }

        private static Dictionary<string, string> ReadServiceDetails(string name)
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

            using (var service = new ServiceController(name))
            {
                service.Refresh();
                data["显示名称"] = service.DisplayName;
                data["当前状态"] = TranslateStatus(service.Status);
                data["服务类型"] = service.ServiceType.ToString();
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
                if (registry == null) return data;
                data["可执行文件路径"] = ReadRegistryValue(registry, "ImagePath");
                data["登录账户"] = ReadRegistryValue(registry, "ObjectName");
                data["服务描述"] = ReadDescription(registry);
                int start = ConvertToInt(registry.GetValue("Start"), -1);
                data["启动类型"] = TranslateStartType(start);
                if (start == 2 && ConvertToInt(registry.GetValue("DelayedAutoStart"), 0) == 1)
                    data["启动类型"] = "自动（延迟启动）";
            }
            return data;
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
            return string.Join(Environment.NewLine, names.OrderBy(s => s, StringComparer.CurrentCultureIgnoreCase));
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
                int required;
                if (RegLoadMUIString(key.Handle, "Description", output,
                    output.Capacity * 2, out required, 0, null) == 0 &&
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
            catch (Exception) { return fallback; }
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
