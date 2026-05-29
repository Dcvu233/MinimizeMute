using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MinimizeMute;

internal sealed record RunningApp(int ProcessId, string ProcessName, string Title, string FileName, bool IsMinimized)
{
    public string DisplayName => $"{ProcessName}.exe  |  PID {ProcessId}  |  {Title}";
    public string WindowStateText => IsMinimized ? "已最小化" : "运行中";
}

internal static class NativeWindowService
{
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public static IReadOnlyList<RunningApp> GetRunningApps()
    {
        var windowsByProcess = new Dictionary<int, List<(string Title, bool IsMinimized)>>();

        EnumWindows((hWnd, _) =>
        {
            if (!IsWindowVisible(hWnd))
            {
                return true;
            }

            var titleLength = GetWindowTextLength(hWnd);
            if (titleLength <= 0)
            {
                return true;
            }

            GetWindowThreadProcessId(hWnd, out var processId);
            if (processId <= 0)
            {
                return true;
            }

            var title = GetWindowTitle(hWnd, titleLength);
            if (string.IsNullOrWhiteSpace(title))
            {
                return true;
            }

            if (!windowsByProcess.TryGetValue(processId, out var windows))
            {
                windows = new List<(string Title, bool IsMinimized)>();
                windowsByProcess[processId] = windows;
            }

            windows.Add((title, IsIconic(hWnd)));
            return true;
        }, IntPtr.Zero);

        return windowsByProcess
            .Select(CreateRunningApp)
            .Where(app => app is not null)
            .Select(app => app!)
            .OrderBy(app => app.ProcessName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(app => app.ProcessId)
            .ToList();
    }

    public static bool IsProcessMinimized(int processId)
    {
        var apps = GetRunningApps();
        var app = apps.FirstOrDefault(candidate => candidate.ProcessId == processId);
        return app?.IsMinimized == true;
    }

    private static RunningApp? CreateRunningApp(KeyValuePair<int, List<(string Title, bool IsMinimized)>> item)
    {
        try
        {
            using var process = Process.GetProcessById(item.Key);
            var title = item.Value
                .Select(window => window.Title)
                .FirstOrDefault(title => !string.IsNullOrWhiteSpace(title)) ?? "(无标题窗口)";

            var fileName = SafeGetFileName(process);
            var isMinimized = item.Value.Count > 0 && item.Value.All(window => window.IsMinimized);
            return new RunningApp(process.Id, process.ProcessName, title, fileName, isMinimized);
        }
        catch
        {
            return null;
        }
    }

    private static string GetWindowTitle(IntPtr hWnd, int titleLength)
    {
        var builder = new StringBuilder(titleLength + 1);
        GetWindowText(hWnd, builder, builder.Capacity);
        return builder.ToString();
    }

    private static string SafeGetFileName(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? "";
        }
        catch
        {
            return "";
        }
    }
}
