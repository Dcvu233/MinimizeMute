namespace MinimizeMute;

public sealed class Form1 : Form
{
    private readonly AppSettings settings;
    private readonly AudioSessionService audioSessionService = new();
    private readonly System.Windows.Forms.Timer refreshTimer = new();
    private readonly System.Windows.Forms.Timer monitorTimer = new();
    private readonly BindingSource runningAppsSource = new();
    private readonly BindingSource mutedAppsSource = new();
    private readonly NotifyIcon trayIcon = new();
    private readonly ContextMenuStrip trayMenu = new();

    private readonly DataGridView runningAppsGrid = new();
    private readonly ListBox mutedAppsList = new();
    private readonly CheckBox muteAllCheckBox = new();
    private readonly Label statusLabel = new();

    private List<RunningApp> runningApps = new();
    private HashSet<string> mutedProcessNames;
    private bool allowExit;

    public Form1()
    {
        settings = AppSettings.Load();
        mutedProcessNames = new HashSet<string>(settings.MutedProcessNames, StringComparer.OrdinalIgnoreCase);

        InitializeComponent();
        RefreshRunningApps();
        RefreshMutedAppsList();
        ApplySettingsToUi();

        refreshTimer.Interval = 3000;
        refreshTimer.Tick += (_, _) => RefreshRunningApps();
        refreshTimer.Start();

        monitorTimer.Interval = 500;
        monitorTimer.Tick += (_, _) => MonitorMinimizedApps();
        monitorTimer.Start();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Dispose();
            monitorTimer.Dispose();
            trayIcon.Visible = false;
            trayIcon.Dispose();
            trayMenu.Dispose();
            audioSessionService.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        Text = "最小化静音";
        MinimumSize = new Size(980, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F);
        FormClosing += HandleFormClosing;
        Resize += HandleResize;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3,
            Padding = new Padding(14),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = BuildHeader();
        root.Controls.Add(header, 0, 0);
        root.SetColumnSpan(header, 2);

        root.Controls.Add(BuildRunningAppsPanel(), 0, 1);
        root.Controls.Add(BuildMutedAppsPanel(), 1, 1);

        statusLabel.AutoSize = true;
        statusLabel.ForeColor = Color.FromArgb(70, 70, 70);
        statusLabel.Padding = new Padding(0, 10, 0, 0);
        root.Controls.Add(statusLabel, 0, 2);
        root.SetColumnSpan(statusLabel, 2);

        Controls.Add(root);
        InitializeTrayIcon();
    }

    private void InitializeTrayIcon()
    {
        var showItem = new ToolStripMenuItem("显示窗口");
        showItem.Click += (_, _) => ShowFromTray();

        var exitItem = new ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitApplication();

        trayMenu.Items.Add(showItem);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add(exitItem);

        trayIcon.Text = "最小化静音";
        trayIcon.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        trayIcon.ContextMenuStrip = trayMenu;
        trayIcon.Visible = true;
        trayIcon.DoubleClick += (_, _) => ShowFromTray();
    }

    private Control BuildHeader()
    {
        var panel = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(0, 0, 0, 12)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "选择应用，最小化时自动静音",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 15F, FontStyle.Bold),
            Dock = DockStyle.Left
        };

        muteAllCheckBox.Text = "所有程序最小化时静音";
        muteAllCheckBox.AutoSize = true;
        muteAllCheckBox.CheckedChanged += (_, _) =>
        {
            settings.MuteAllPrograms = muteAllCheckBox.Checked;
            settings.Save();
            MonitorMinimizedApps();
            RefreshMutedAppsList();
        };

        panel.Controls.Add(title, 0, 0);
        panel.Controls.Add(muteAllCheckBox, 1, 0);
        return panel;
    }

    private Control BuildRunningAppsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            Padding = new Padding(0, 0, 12, 0)
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = "当前启动的应用",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 8)
        };

        runningAppsGrid.Dock = DockStyle.Fill;
        runningAppsGrid.AllowUserToAddRows = false;
        runningAppsGrid.AllowUserToDeleteRows = false;
        runningAppsGrid.AllowUserToResizeRows = false;
        runningAppsGrid.AutoGenerateColumns = false;
        runningAppsGrid.BackgroundColor = Color.White;
        runningAppsGrid.BorderStyle = BorderStyle.FixedSingle;
        runningAppsGrid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        runningAppsGrid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
        runningAppsGrid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 247, 250);
        runningAppsGrid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(40, 40, 40);
        runningAppsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(245, 247, 250);
        runningAppsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(220, 235, 252);
        runningAppsGrid.DefaultCellStyle.SelectionForeColor = Color.Black;
        runningAppsGrid.EnableHeadersVisualStyles = false;
        runningAppsGrid.GridColor = Color.FromArgb(230, 230, 230);
        runningAppsGrid.MultiSelect = false;
        runningAppsGrid.ReadOnly = true;
        runningAppsGrid.RowHeadersVisible = false;
        runningAppsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        runningAppsGrid.DataSource = runningAppsSource;

        runningAppsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RunningApp.ProcessName),
            HeaderText = "应用",
            Width = 150
        });
        runningAppsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RunningApp.ProcessId),
            HeaderText = "PID",
            Width = 70
        });
        runningAppsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RunningApp.Title),
            HeaderText = "窗口标题",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        });
        runningAppsGrid.Columns.Add(new DataGridViewTextBoxColumn
        {
            DataPropertyName = nameof(RunningApp.WindowStateText),
            HeaderText = "窗口状态",
            Width = 90
        });

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 10, 0, 0)
        };

        var addButton = new Button { Text = "加入最小化静音", AutoSize = true };
        addButton.Click += (_, _) => AddSelectedApp();

        var refreshButton = new Button { Text = "刷新列表", AutoSize = true };
        refreshButton.Click += (_, _) => RefreshRunningApps();

        buttons.Controls.Add(addButton);
        buttons.Controls.Add(refreshButton);

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(runningAppsGrid, 0, 1);
        panel.Controls.Add(buttons, 0, 2);
        return panel;
    }

    private Control BuildMutedAppsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var label = new Label
        {
            Text = "最小化静音列表",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10.5F, FontStyle.Bold),
            Padding = new Padding(0, 0, 0, 8)
        };

        mutedAppsList.Dock = DockStyle.Fill;
        mutedAppsList.DataSource = mutedAppsSource;
        mutedAppsList.BorderStyle = BorderStyle.FixedSingle;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(0, 10, 0, 0)
        };

        var removeButton = new Button { Text = "移除选中", AutoSize = true };
        removeButton.Click += (_, _) => RemoveSelectedApp();

        var clearButton = new Button { Text = "清空列表", AutoSize = true };
        clearButton.Click += (_, _) => ClearMutedApps();

        buttons.Controls.Add(removeButton);
        buttons.Controls.Add(clearButton);

        panel.Controls.Add(label, 0, 0);
        panel.Controls.Add(mutedAppsList, 0, 1);
        panel.Controls.Add(buttons, 0, 2);
        return panel;
    }

    private void ApplySettingsToUi()
    {
        muteAllCheckBox.Checked = settings.MuteAllPrograms;
    }

    private void HandleFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (allowExit || e.CloseReason != CloseReason.UserClosing)
        {
            return;
        }

        e.Cancel = true;
        HideToTray();
    }

    private void HandleResize(object? sender, EventArgs e)
    {
        if (WindowState == FormWindowState.Minimized)
        {
            HideToTray();
        }
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;
        trayIcon.BalloonTipTitle = "最小化静音仍在运行";
        trayIcon.BalloonTipText = "双击托盘图标可恢复窗口，右键可退出。";
        trayIcon.ShowBalloonTip(2500);
    }

    private void ShowFromTray()
    {
        Show();
        ShowInTaskbar = true;
        WindowState = FormWindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        allowExit = true;
        Close();
    }

    private void RefreshRunningApps()
    {
        runningApps = NativeWindowService.GetRunningApps()
            .Where(app => app.ProcessId != Environment.ProcessId)
            .ToList();

        runningAppsSource.DataSource = runningApps;
        statusLabel.Text = $"已发现 {runningApps.Count} 个窗口应用。监控中：{GetMonitoredDescription()}";
    }

    private void RefreshMutedAppsList()
    {
        mutedAppsSource.DataSource = mutedProcessNames
            .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
            .Select(name => $"{name}.exe")
            .ToList();
    }

    private void AddSelectedApp()
    {
        if (runningAppsGrid.CurrentRow?.DataBoundItem is not RunningApp app)
        {
            return;
        }

        if (mutedProcessNames.Add(app.ProcessName))
        {
            SaveMutedProcessNames();
            RefreshMutedAppsList();
            MonitorMinimizedApps();
        }
    }

    private void RemoveSelectedApp()
    {
        if (mutedAppsList.SelectedItem is not string selected)
        {
            return;
        }

        var processName = Path.GetFileNameWithoutExtension(selected);
        if (mutedProcessNames.Remove(processName))
        {
            SaveMutedProcessNames();
            audioSessionService.RestoreAll();
            RefreshMutedAppsList();
            MonitorMinimizedApps();
        }
    }

    private void ClearMutedApps()
    {
        mutedProcessNames.Clear();
        SaveMutedProcessNames();
        audioSessionService.RestoreAll();
        RefreshMutedAppsList();
        MonitorMinimizedApps();
    }

    private void SaveMutedProcessNames()
    {
        settings.MutedProcessNames = mutedProcessNames
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        settings.Save();
        statusLabel.Text = $"设置已保存。监控中：{GetMonitoredDescription()}";
    }

    private void MonitorMinimizedApps()
    {
        var apps = NativeWindowService.GetRunningApps()
            .Where(app => app.ProcessId != Environment.ProcessId)
            .ToList();

        var monitoredApps = apps
            .Where(app => settings.MuteAllPrograms || mutedProcessNames.Contains(app.ProcessName))
            .ToList();

        var minimizedProcessNames = monitoredApps
            .GroupBy(app => app.ProcessName, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.All(app => app.IsMinimized))
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var mutedProcessName in audioSessionService.MutedProcessNames.Where(name => !minimizedProcessNames.Contains(name)).ToList())
        {
            audioSessionService.RestoreProcessFamily(mutedProcessName);
        }

        foreach (var processName in minimizedProcessNames)
        {
            audioSessionService.MuteProcessFamily(processName);
        }

        statusLabel.Text = $"已发现 {apps.Count} 个窗口应用。监控中：{GetMonitoredDescription()}";
    }

    private string GetMonitoredDescription()
    {
        if (settings.MuteAllPrograms)
        {
            return "所有程序";
        }

        return mutedProcessNames.Count == 0 ? "未选择应用" : $"{mutedProcessNames.Count} 个应用";
    }
}
