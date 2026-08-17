# 启动修复后的 QuotaMonitor 并打开设置窗口「其他」页，供人工检查
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @'
using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
public class W32 {
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    public delegate bool EnumProc(IntPtr h, IntPtr lParam);
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
    public const uint LEFTDOWN = 0x02, LEFTUP = 0x04, RIGHTDOWN = 0x08, RIGHTUP = 0x10;
    public static void LeftClick(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(150);
        mouse_event(LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(LEFTUP, 0, 0, 0, UIntPtr.Zero);
    }
    public static void RightClick(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(150);
        mouse_event(RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(RIGHTUP, 0, 0, 0, UIntPtr.Zero);
    }
    public static List<int[]> WindowsOf(int pid) {
        var list = new List<int[]>();
        EnumWindows((h, l) => {
            uint p; GetWindowThreadProcessId(h, out p);
            if (p == pid) {
                var sb = new StringBuilder(256);
                GetWindowText(h, sb, 256);
                RECT r; GetWindowRect(h, out r);
                list.Add(new int[] { r.L, r.T, r.R, r.B, h.ToInt32() });
            }
            return true;
        }, IntPtr.Zero);
        return list;
    }
}
'@

Get-Process QuotaMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800
Start-Process "D:\SystemFiles\Documents\Project\DEEPSEEK_MONEY\publish\Release\QuotaMonitor.exe"
Start-Sleep -Seconds 3

$proc = Get-Process QuotaMonitor -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Host "APP NOT RUNNING"; exit 1 }
$pid0 = $proc.Id
Write-Host "pid=$pid0"

# 列出所有窗口，找出悬浮窗（小而窄）和设置窗口（~570 宽）
$wins = [W32]::WindowsOf($pid0)
foreach ($wn in $wins) { Write-Host ("win L={0} T={1} R={2} B={3} hwnd={4}" -f $wn[0], $wn[1], $wn[2], $wn[3], $wn[4]) }

# 找设置窗口（已打开的）或悬浮窗
$settings = $wins | Where-Object { ($_[2] - $_[0]) -gt 400 } | Select-Object -First 1
$float = $wins | Where-Object { ($_[2] - $_[0]) -le 300 -and ($_[3] - $_[1]) -gt 30 } | Select-Object -First 1

if (-not $settings) {
    if (-not $float) { Write-Host "NO FLOAT WINDOW"; exit 1 }
    $fx = [int](($float[0] + $float[2]) / 2)
    $fy = [int](($float[1] + $float[3]) / 2)
    Write-Host "Right-click float center ($fx,$fy)"
    [W32]::RightClick($fx, $fy)
    Start-Sleep -Milliseconds 700
    [System.Windows.Forms.SendKeys]::SendWait("{DOWN}")
    Start-Sleep -Milliseconds 300
    [System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
    Start-Sleep -Milliseconds 1200
    $wins = [W32]::WindowsOf($pid0)
    $settings = $wins | Where-Object { ($_[2] - $_[0]) -gt 400 } | Select-Object -First 1
}

if (-not $settings) { Write-Host "SETTINGS NOT OPENED"; exit 1 }
$sx = [int](($settings[0] + $settings[2]) / 2)
$sy = [int](($settings[1] + $settings[3]) / 2)
Write-Host "Settings at ($($settings[0]),$($settings[1]))"
[W32]::SetForegroundWindow([IntPtr]$settings[4]) | Out-Null
Start-Sleep -Milliseconds 400

# 点击左侧导航第 4 项（其他）：导航宽 110，项高 56，第 4 项中心 y=196（客户区，标题栏 ~31，边框 ~8）
$cx = $settings[0] + 8 + 55
$cy = $settings[1] + 31 + 196
Write-Host "Click nav '其他' at ($cx,$cy)"
[W32]::LeftClick($cx, $cy)
Start-Sleep -Milliseconds 500
Write-Host "OPEN - please test the dropdown"
