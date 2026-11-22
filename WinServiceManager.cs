using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WinServMgr
{
    public partial class MainForm : Form
    {
        private readonly Dictionary<string, ServiceEntry> allServices =
            new Dictionary<string, ServiceEntry>(StringComparer.OrdinalIgnoreCase);
        private readonly BindingList<ServiceEntry> visibleServices = new BindingList<ServiceEntry>();
        private readonly BindingSource binding = new BindingSource();
        private bool refreshBusy;
        private bool operationBusy;
        private bool firstScan = true;
        private string sortColumn = "ServiceName";
        private bool sortDescending;
        private Label countLabel;
        private Button refreshButton;
        private Label loadingPlaceholder;
        private Task<List<ServiceEntry>> initialScanTask;
        private readonly ContextMenuStrip serviceContextMenu = DarkTheme.CreateMenu();
        private string contextServiceName;

        public MainForm()
        {
            InitializeComponent();
            // Overlap the first SCM enumeration with form/control construction.
            initialScanTask = Task.Run(() => SrvController.Enumerate());
            InitializeModernLayout();
            ConfigureGrid();
            ConfigureServiceContextMenu();
            Disposed += (sender, args) => serviceContextMenu.Dispose();
            binding.DataSource = visibleServices;
            // Do not bind or paint an empty grid before the first snapshot arrives.
            DarkTheme.Apply(this);
            // SCM calls are relatively costly; a worker scans at most once per 2.5s.
            tmrRefreshGrid.Interval = 2500;
            tmrRefreshGrid.Stop();
            UpdateSortGlyph();
            UpdateCountLabel();
        }

        private void InitializeModernLayout()
        {
            Text = "WinServiceManager";
            Font = new Font("Microsoft YaHei UI", 9F);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(760, 480);
            ClientSize = new Size(1050, 680);
            StartPosition = FormStartPosition.CenterScreen;

            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3,
                BackColor = DarkTheme.Surface, Padding = new Padding(14, 12, 14, 12)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            var toolbar = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
                BackColor = DarkTheme.Surface
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            toolbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var filterLabel = new Label {
                Text = "搜索", AutoSize = true, Anchor = AnchorStyles.Left,
                Margin = new Padding(2, 1, 10, 1)
            };
            txtFilter.Text = "";
            txtFilter.Margin = new Padding(0, 7, 12, 7);
            txtFilter.Dock = DockStyle.Fill;
            txtFilter.Font = new Font("Microsoft YaHei UI", 9F);
            txtFilter.TextChanged -= txtFilter_TextChanged;
            txtFilter.Enter -= txtFilter_Enter;
            txtFilter.Leave -= txtFilter_Leave;
            txtFilter.TextChanged += txtFilter_TextChanged;

            tbxShowStopped.Text = "显示已停止的服务";
            tbxShowStopped.AutoSize = true;
            tbxShowStopped.Anchor = AnchorStyles.Left;
            tbxShowStopped.Margin = new Padding(8, 0, 18, 0);

            refreshButton = new Button {
                Name = "btnRefresh", Text = "刷新 (F5)",
                Size = new Size(110, 32), Margin = new Padding(0, 5, 0, 0)
            };
            refreshButton.Click += async (sender, e) => await RefreshServicesAsync();

            toolbar.Controls.Add(filterLabel, 0, 0);
            toolbar.Controls.Add(txtFilter, 1, 0);
            toolbar.Controls.Add(tbxShowStopped, 2, 0);
            toolbar.Controls.Add(refreshButton, 3, 0);

            dgvServicesList.Dock = DockStyle.Fill;
            dgvServicesList.Margin = new Padding(0);
            dgvServicesList.Font = new Font("Microsoft YaHei UI", 9F);
            dgvServicesList.CellMouseClick -= dgvServicesList_CellMouseClick;
            dgvServicesList.CellMouseClick += dgvServicesList_CellMouseClick;

            var footer = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
                BackColor = DarkTheme.Surface
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            countLabel = new Label {
                Name = "lblCount", Text = "", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, ForeColor = DarkTheme.Muted
            };
            btnStartService.Text = "启动";
            btnStopService.Text = "停止";
            btnRestartService.Text = "重启";
            foreach (var button in new[] { btnStartService, btnStopService, btnRestartService })
            {
                button.Dock = DockStyle.Fill;
                button.Margin = new Padding(6, 12, 0, 4);
            }

            footer.Controls.Add(countLabel, 0, 0);
            footer.Controls.Add(btnStartService, 1, 0);
            footer.Controls.Add(btnStopService, 2, 0);
            footer.Controls.Add(btnRestartService, 3, 0);

            var listHost = new Panel {
                Dock = DockStyle.Fill, BackColor = DarkTheme.Input,
                Margin = Padding.Empty
            };
            loadingPlaceholder = new Label {
                Name = "lblLoading", Text = "正在读取 Windows 服务…",
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter,
                BackColor = DarkTheme.Input, ForeColor = DarkTheme.Muted
            };
            dgvServicesList.Visible = false;
            listHost.Controls.Add(dgvServicesList);
            listHost.Controls.Add(loadingPlaceholder);
            root.Controls.Add(toolbar, 0, 0);
            root.Controls.Add(listHost, 0, 1);
            root.Controls.Add(footer, 0, 2);
            Controls.Add(root);

            KeyDown += (sender, e) => {
                if (e.KeyCode == Keys.F5)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    _ = RefreshServicesAsync();
                }
                if (e.Control && e.KeyCode == Keys.F)
                {
                    txtFilter.Focus();
                    txtFilter.SelectAll();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };
        }

        private void ConfigureGrid()
        {
            dgvServicesList.AutoGenerateColumns = false;
            dgvServicesList.Columns.Clear();
            dgvServicesList.AllowUserToAddRows = false;
            dgvServicesList.AllowUserToDeleteRows = false;
            dgvServicesList.AllowUserToResizeRows = false;
            dgvServicesList.MultiSelect = true;
            dgvServicesList.ReadOnly = true;
            dgvServicesList.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            dgvServicesList.RowHeadersVisible = false;
            dgvServicesList.RowTemplate.Height = 29;
            dgvServicesList.ColumnHeadersHeight = 36;
            dgvServicesList.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            dgvServicesList.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;

            var serviceName = new DataGridViewTextBoxColumn {
                Name = "ServiceName", DataPropertyName = "ServiceName",
                HeaderText = "服务名称", MinimumWidth = 160, Width = 260,
                SortMode = DataGridViewColumnSortMode.Programmatic
            };
            var displayName = new DataGridViewTextBoxColumn {
                Name = "DisplayName", DataPropertyName = "DisplayName",
                HeaderText = "显示名称", MinimumWidth = 220,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.Programmatic
            };
            var state = new DataGridViewTextBoxColumn {
                Name = "ServiceState", DataPropertyName = "ServiceState",
                HeaderText = "状态", Width = 120, MinimumWidth = 100,
                SortMode = DataGridViewColumnSortMode.Programmatic
            };
            dgvServicesList.Columns.AddRange(serviceName, displayName, state);
            dgvServicesList.ColumnHeaderMouseClick += GridColumnHeaderMouseClick;
            dgvServicesList.CellDoubleClick += GridCellDoubleClick;
            dgvServicesList.CellFormatting += GridCellFormatting;
        }

        public List<string> GetSelectedServices()
        {
            return dgvServicesList.SelectedRows.Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as ServiceEntry)
                .Where(s => s != null)
                .Select(s => s.ServiceName)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private async void SMATestTool_Load(object sender, EventArgs e)
        {
            await RefreshServicesAsync();
            if (!IsDisposed) tmrRefreshGrid.Start();
        }

        private async Task RefreshServicesAsync()
        {
            if (refreshBusy || IsDisposed || Disposing) return;
            refreshBusy = true;
            refreshButton.Enabled = false;
            try
            {
                // The first scan starts during construction, before Form.Load.
                var scan = initialScanTask;
                initialScanTask = null;
                var services = await (scan ?? Task.Run(() => SrvController.Enumerate()));
                if (!IsDisposed && !Disposing)
                {
                    ApplySnapshot(services);
                    if (firstScan)
                    {
                        RevealFirstSnapshot();
                        firstScan = false;
                    }
                }
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                {
                    if (firstScan)
                    {
                        loadingPlaceholder.Text = "加载失败，请点击“刷新 (F5)”重试";
                        DarkTheme.ShowError(this, "无法读取 Windows 服务：\r\n" + ex.Message);
                    }
                    else countLabel.Text = "刷新失败：" + ex.Message;
                }
            }
            finally
            {
                refreshBusy = false;
                if (!IsDisposed && !Disposing) { refreshButton.Enabled = true; UpdateCountLabel(); }
            }
        }

        private void RevealFirstSnapshot()
        {
            // One initial data binding while the grid is hidden: the first
            // visible frame already has its complete rows, column widths and styles.
            var host = dgvServicesList.Parent;
            host.SuspendLayout();
            dgvServicesList.SuspendLayout();
            try
            {
                dgvServicesList.DataSource = binding;
                dgvServicesList.CurrentCell = null;
                dgvServicesList.ClearSelection();
                loadingPlaceholder.Visible = false;
                dgvServicesList.Visible = true;
            }
            finally
            {
                dgvServicesList.ResumeLayout(false);
                host.ResumeLayout(true);
            }
        }

        private void ApplySnapshot(IList<ServiceEntry> snapshot)
        {
            var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool membershipChanged = false;
            bool sortValuesChanged = false;
            var changed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var latest in snapshot)
            {
                discovered.Add(latest.ServiceName);
                ServiceEntry existing;
                if (!allServices.TryGetValue(latest.ServiceName, out existing))
                {
                    allServices.Add(latest.ServiceName, latest);
                    membershipChanged = true;
                    continue;
                }

                if (existing.ServiceState != latest.ServiceState)
                {
                    existing.ServiceState = latest.ServiceState;
                    changed.Add(existing.ServiceName);
                    if (sortColumn == "ServiceState") sortValuesChanged = true;
                }
                if (!string.Equals(existing.DisplayName, latest.DisplayName, StringComparison.Ordinal))
                {
                    existing.DisplayName = latest.DisplayName;
                    changed.Add(existing.ServiceName);
                    if (sortColumn == "DisplayName") sortValuesChanged = true;
                }
            }

            foreach (string name in allServices.Keys.Where(k => !discovered.Contains(k)).ToList())
            {
                allServices.Remove(name);
                membershipChanged = true;
            }

            // Visibility can change after a status or display-name update.
            // Compare against the actual bound membership, not just the new state.
            bool visibilityChanged = false;
            if (changed.Count > 0)
            {
                var visibleNames = new HashSet<string>(
                    visibleServices.Select(s => s.ServiceName), StringComparer.OrdinalIgnoreCase);
                string filter = (txtFilter.Text ?? "").Trim();
                foreach (string name in changed)
                {
                    ServiceEntry entry;
                    if (!allServices.TryGetValue(name, out entry)) continue;
                    bool shouldShow = (tbxShowStopped.Checked ||
                        entry.ServiceState != ServiceControllerStatus.Stopped) &&
                        (filter.Length == 0 ||
                         entry.ServiceName.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                         entry.DisplayName.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0);
                    if (shouldShow != visibleNames.Contains(name))
                    {
                        visibilityChanged = true;
                        break;
                    }
                }
            }

            if (firstScan || membershipChanged || sortValuesChanged || visibilityChanged)
                RebuildVisibleRows();

            // Even if sorted order stays the same, changed values must be repainted.
            if (changed.Count > 0)
                for (int i = 0; i < visibleServices.Count; ++i)
                    if (changed.Contains(visibleServices[i].ServiceName))
                        binding.ResetItem(i);
            UpdateCountLabel();
        }

        private IEnumerable<ServiceEntry> FilteredAndSorted()
        {
            string filter = (txtFilter.Text ?? "").Trim();
            IEnumerable<ServiceEntry> query = allServices.Values.Where(entry =>
                (tbxShowStopped.Checked || entry.ServiceState != ServiceControllerStatus.Stopped) &&
                (filter.Length == 0 ||
                 entry.ServiceName.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                 entry.DisplayName.IndexOf(filter, StringComparison.CurrentCultureIgnoreCase) >= 0));

            Func<ServiceEntry, string> key = sortColumn == "DisplayName"
                ? (Func<ServiceEntry, string>)(s => s.DisplayName ?? "")
                : sortColumn == "ServiceState"
                    ? (Func<ServiceEntry, string>)(s => s.ServiceState.ToString())
                    : (Func<ServiceEntry, string>)(s => s.ServiceName ?? "");

            IOrderedEnumerable<ServiceEntry> ordered = sortDescending
                ? query.OrderByDescending(key, StringComparer.CurrentCultureIgnoreCase)
                : query.OrderBy(key, StringComparer.CurrentCultureIgnoreCase);
            return ordered.ThenBy(s => s.ServiceName, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private void RebuildVisibleRows()
        {
            var target = FilteredAndSorted().ToList();
            // No work if a refreshed snapshot did not change visible rows or their order.
            if (visibleServices.Count == target.Count &&
                visibleServices.SequenceEqual(target))
                return;

            // The initial list is still unbound/hidden, so don't walk grid rows.
            var selected = firstScan
                ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(GetSelectedServices(), StringComparer.OrdinalIgnoreCase);
            string currentName = firstScan || dgvServicesList.CurrentRow == null ? null
                : (dgvServicesList.CurrentRow.DataBoundItem as ServiceEntry)?.ServiceName;
            int firstRow = -1;
            if (!firstScan)
                try { firstRow = dgvServicesList.FirstDisplayedScrollingRowIndex; }
                catch (InvalidOperationException) { }

            visibleServices.RaiseListChangedEvents = false;
            try
            {
                visibleServices.Clear();
                foreach (var service in target) visibleServices.Add(service);
            }
            finally
            {
                visibleServices.RaiseListChangedEvents = true;
                binding.ResetBindings(false);
            }

            if (firstScan) return; // No selection/scroll history to restore.
            dgvServicesList.ClearSelection();
            DataGridViewRow currentRow = null;
            foreach (DataGridViewRow row in dgvServicesList.Rows)
            {
                var entry = row.DataBoundItem as ServiceEntry;
                if (entry == null) continue;
                if (selected.Contains(entry.ServiceName)) row.Selected = true;
                if (string.Equals(currentName, entry.ServiceName, StringComparison.OrdinalIgnoreCase))
                    currentRow = row;
            }

            if (currentRow != null && currentRow.Cells.Count > 0)
            {
                dgvServicesList.CurrentCell = currentRow.Cells[0];
                currentRow.Selected = selected.Contains(currentName);
            }
            if (firstRow >= 0 && firstRow < dgvServicesList.RowCount)
            {
                try { dgvServicesList.FirstDisplayedScrollingRowIndex = firstRow; }
                catch (InvalidOperationException) { }
            }
        }

        private void UpdateCountLabel()
        {
            int running = allServices.Values.Count(s => s.ServiceState == ServiceControllerStatus.Running);
            countLabel.Text = string.Format("显示 {0} / {1} 项    ·    正在运行 {2} 项{3}",
                visibleServices.Count, allServices.Count, running, refreshBusy ? "    ·    刷新中" : "");
            Text = string.Format("WinServiceManager  ·  {0} 个服务", allServices.Count);
        }

        private void GridColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0) return;
            var name = dgvServicesList.Columns[e.ColumnIndex].Name;
            sortDescending = name == sortColumn && !sortDescending;
            sortColumn = name;
            RebuildVisibleRows();
            UpdateSortGlyph();
        }

        private void UpdateSortGlyph()
        {
            foreach (DataGridViewColumn column in dgvServicesList.Columns)
                column.HeaderCell.SortGlyphDirection = column.Name == sortColumn
                    ? (sortDescending ? SortOrder.Descending : SortOrder.Ascending)
                    : SortOrder.None;
        }

        private void GridCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
                dgvServicesList.Columns[e.ColumnIndex].Name != "ServiceState") return;
            var entry = dgvServicesList.Rows[e.RowIndex].DataBoundItem as ServiceEntry;
            if (entry == null) return;
            switch (entry.ServiceState)
            {
                case ServiceControllerStatus.Running:
                    e.Value = "运行中";
                    e.CellStyle.ForeColor = Color.FromArgb(109, 219, 154);
                    break;
                case ServiceControllerStatus.Stopped:
                    e.Value = "已停止";
                    e.CellStyle.ForeColor = Color.FromArgb(255, 157, 126);
                    break;
                default:
                    e.Value = entry.ServiceState.ToString();
                    e.CellStyle.ForeColor = DarkTheme.Muted;
                    break;
            }
            e.FormattingApplied = true;
        }

        private void OpenServiceDetails(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            var details = new ServiceDetailsForm(name);
            // Reuse the main window artwork; the EXE icon uses the same icon.ico.
            var detailIcon = (Icon)Icon.Clone();
            details.Icon = detailIcon;
            details.Disposed += (sender, args) => detailIcon.Dispose();

            // Center on the right half of the parent rather than the desktop.
            // Clamp to the owner's monitor work area (including multiple displays).
            Rectangle area = Screen.FromControl(this).WorkingArea;
            int intendedX = Left + (Width * 3 / 4) - (details.Width / 2);
            int intendedY = Top + (Height - details.Height) / 2;
            int x = Math.Max(area.Left, Math.Min(intendedX, area.Right - details.Width));
            int y = Math.Max(area.Top, Math.Min(intendedY, area.Bottom - details.Height));

            details.StartPosition = FormStartPosition.Manual;
            details.Location = new Point(x, y);
            details.Show(this);
        }

        private void GridCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var entry = dgvServicesList.Rows[e.RowIndex].DataBoundItem as ServiceEntry;
            if (entry != null) OpenServiceDetails(entry.ServiceName);
        }

        private void ConfigureServiceContextMenu()
        {
            serviceContextMenu.Items.Add("查看详情", null, (sender, args) =>
            {
                if (!string.IsNullOrEmpty(contextServiceName))
                    OpenServiceDetails(contextServiceName);
            });
            serviceContextMenu.Items.Add(new ToolStripSeparator());
            serviceContextMenu.Items.Add("启动", null, btnStartService_Click);
            serviceContextMenu.Items.Add("停止", null, btnStopService_Click);
            serviceContextMenu.Items.Add("重启", null, btnRestartService_Click);
        }

        private void dgvServicesList_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var row = dgvServicesList.Rows[e.RowIndex];
            if (!row.Selected)
            {
                // Selecting a new row replaces the selection; right-clicking an
                // already selected row must preserve all selected services.
                dgvServicesList.ClearSelection();
                dgvServicesList.CurrentCell = row.Cells[e.ColumnIndex];
                row.Selected = true;
            }
            var entry = row.DataBoundItem as ServiceEntry;
            if (entry == null) return;

            contextServiceName = entry.ServiceName;
            // The same form-owned menu is reused. No disposal from Closed.
            serviceContextMenu.Items[2].Enabled =
                !operationBusy && entry.ServiceState == ServiceControllerStatus.Stopped;
            serviceContextMenu.Items[3].Enabled =
                !operationBusy && entry.ServiceState == ServiceControllerStatus.Running;
            serviceContextMenu.Items[4].Enabled =
                !operationBusy && entry.ServiceState == ServiceControllerStatus.Running;
            serviceContextMenu.Show(dgvServicesList, dgvServicesList.PointToClient(Cursor.Position));
        }

        private async Task RunForSelectedAsync(Action<ServiceController> action)
        {
            if (operationBusy) return;
            var names = GetSelectedServices();
            if (names.Count == 0) return;
            operationBusy = true;
            btnStartService.Enabled = btnStopService.Enabled = btnRestartService.Enabled = false;
            try
            {
                await SrvController.PerformAsync(names, action);
            }
            catch (Exception ex)
            {
                if (!IsDisposed && !Disposing)
                {
                    var message = ex.Message;
                    DarkTheme.ShowError(this, message);
                }
            }
            finally
            {
                operationBusy = false;
                if (!IsDisposed && !Disposing)
                {
                    btnStartService.Enabled = btnStopService.Enabled = btnRestartService.Enabled = true;
                    await RefreshServicesAsync();
                }
            }
        }

        private async void btnStartService_Click(object sender, EventArgs e)
        {
            await RunForSelectedAsync(sc => sc.Start());
        }

        private async void btnStopService_Click(object sender, EventArgs e)
        {
            await RunForSelectedAsync(sc => sc.Stop());
        }

        private async void btnRestartService_Click(object sender, EventArgs e)
        {
            await RunForSelectedAsync(sc =>
            {
                sc.Stop();
                sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(60));
                sc.Start();
            });
        }

        private void txtFilter_TextChanged(object sender, EventArgs e)
        {
            if (countLabel == null) return;
            RebuildVisibleRows();
            UpdateCountLabel();
        }

        // Designer wires these events, but the modern filter no longer uses placeholder text.
        private void txtFilter_Enter(object sender, EventArgs e) { }
        private void txtFilter_Leave(object sender, EventArgs e) { }

        private void tbxShowStopped_CheckedChanged(object sender, EventArgs e)
        {
            if (countLabel == null) return;
            RebuildVisibleRows();
            UpdateCountLabel();
        }

        private async void tmrRefreshGrid_Tick(object sender, EventArgs e)
        {
            await RefreshServicesAsync();
        }
    }
}
