# Dev tool: drive Inno Setup wizard by sending Enter to its window
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class WinKeys {
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
}
"@

$proc = Get-Process 'QuotaMonitor-Setup*' -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowHandle -ne 0 } | Select-Object -First 1
if (-not $proc) { Write-Host "NO WIZARD"; exit 1 }

$hwnd = $proc.MainWindowHandle
Write-Host "wizard hwnd=$hwnd"
# WM_KEYDOWN(VK_RETURN) + WM_KEYUP
[WinKeys]::PostMessage($hwnd, 0x100, [IntPtr]0x0D, [IntPtr]0) | Out-Null
Start-Sleep -Milliseconds 80
[WinKeys]::PostMessage($hwnd, 0x101, [IntPtr]0x0D, [IntPtr]0) | Out-Null
Write-Host "enter sent"
