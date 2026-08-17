# 开发工具：模拟鼠标打开设置窗口并截图（验证 UI 布局用）
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Mouse {
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);
    public const uint LEFTDOWN = 0x02, LEFTUP = 0x04, RIGHTDOWN = 0x08, RIGHTUP = 0x10;
    public static void RightClick(int x, int y) {
        SetCursorPos(x, y);
        System.Threading.Thread.Sleep(120);
        mouse_event(RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
        mouse_event(RIGHTUP, 0, 0, 0, UIntPtr.Zero);
    }
}
"@

# 悬浮窗位置：读配置文件（默认屏幕中央）
$cfgPath = "D:\SystemFiles\Documents\QuotaMonitor\设置.json"
$fx = $null; $fy = $null
try {
    $cfg = Get-Content $cfgPath -Raw -Encoding UTF8 | ConvertFrom-Json
    $fx = $cfg.FloatX; $fy = $cfg.FloatY
} catch {}
if ($fx -eq $null -or $fy -eq $null) {
    $w = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea.Width
    $h = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea.Height
    $fx = [int]($w/2); $fy = [int]($h/2)
}

# 右键点击悬浮窗中心（假定窗口约 200x70，点位置偏移）
[Mouse]::RightClick($fx + 100, $fy + 35)
Start-Sleep -Milliseconds 600

# 菜单弹出后：向下选择第一项（设置...）并回车
[System.Windows.Forms.SendKeys]::SendWait("{DOWN}")
Start-Sleep -Milliseconds 200
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")
Start-Sleep -Milliseconds 900

# 全屏截图
$b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$bmp = New-Object System.Drawing.Bitmap($b.Width, $b.Height)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(0, 0, 0, 0, $bmp.Size)
$out = "D:\SystemFiles\Documents\Project\DEEPSEEK_MONEY\docs\settings-check.png"
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "saved: $out"
