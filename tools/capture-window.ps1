# 开发工具：PrintWindow 截取 QuotaMonitor 进程的可见窗口（被遮挡也能截）
# 用法：-TitlePattern "统计" 按标题匹配；-Float 截取无标题的悬浮窗
param([string]$TitlePattern = "统计", [switch]$Float)

Add-Type @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public class WinCap2 {
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}
"@

$proc = Get-Process QuotaMonitor -ErrorAction SilentlyContinue | Select-Object -First 1
if (-not $proc) { Write-Host "NO PROCESS"; exit 1 }
$targetPid = $proc.Id
Write-Host "process id: $targetPid"

$found = @()
$cb = {
    param($h, $l)
    if ([WinCap2]::IsWindowVisible($h)) {
        $pidOut = [uint32]0
        [WinCap2]::GetWindowThreadProcessId($h, [ref]$pidOut) | Out-Null
        if ($pidOut -eq $targetPid) {
            $sb = New-Object System.Text.StringBuilder 256
            [WinCap2]::GetWindowText($h, $sb, 256) | Out-Null
            $script:found += "$h|$sb"
        }
    }
    return $true
}
[WinCap2]::EnumWindows($cb, [IntPtr]::Zero) | Out-Null

$target = $null
foreach ($t in $found) {
    Write-Host "  win: $t"
    if ($Float) {
        if ($t -match '^\d+\|$') { $target = ($t -split '\|')[0]; break }
    }
    elseif ($t -match $TitlePattern) { $target = ($t -split '\|')[0]; break }
}
if (-not $target -and $found.Count -gt 0) { $target = ($found[0] -split '\|')[0] }
if (-not $target) { Write-Host "NOT FOUND"; exit 1 }

$hwnd = [IntPtr]::new([int]$target)
Add-Type -AssemblyName System.Drawing
$bmp = New-Object System.Drawing.Bitmap(2000, 1500)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.Clear([System.Drawing.Color]::White)
$hdc = $g.GetHdc()
[WinCap2]::PrintWindow($hwnd, $hdc, 2) | Out-Null
$g.ReleaseHdc($hdc)
$out = "D:\SystemFiles\Documents\Project\DEEPSEEK_MONEY\docs\settings-window.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "saved: $out hwnd=$target"
