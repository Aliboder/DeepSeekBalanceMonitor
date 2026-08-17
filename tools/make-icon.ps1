# Dev tool: generate app.ico (dark gradient circle + white Q + teal arc gauge)
# Same composition as IconFactory.cs in the app. Small sizes use BMP frames
# (required by csc), 256 uses PNG frame.
Add-Type -AssemblyName System.Drawing

function New-IconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # dark gradient circle background
    $w2 = $size - 2; $h2 = $size - 2
    $rect = New-Object System.Drawing.Rectangle(1, 1, $w2, $h2)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddEllipse($rect)
    $bg = New-Object System.Drawing.Drawing2D.PathGradientBrush($path)
    $bg.CenterColor = [System.Drawing.Color]::FromArgb(255, 74, 74, 74)
    $bg.SurroundColors = @([System.Drawing.Color]::FromArgb(255, 24, 24, 24))
    $g.FillPath($bg, $path)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 85, 85, 85), 1)
    $g.DrawPath($pen, $path)
    $pen.Dispose(); $bg.Dispose(); $path.Dispose()

    # arc gauge: start 315deg (top-right), sweep 270deg clockwise, teal->green gradient
    $r = $size * 0.34
    $w = [Math]::Max(2.0, $size * 0.09)
    $cx = $size / 2.0; $cy = $size / 2.0
    $x1 = $cx - $r; $y1 = $cy - $r; $w3 = $r * 2; $h3 = $r * 2
    $arcRect = New-Object System.Drawing.RectangleF($x1, $y1, $w3, $h3)
    $cLight = [System.Drawing.Color]::FromArgb(255, 41, 224, 208)
    $cDark = [System.Drawing.Color]::FromArgb(255, 74, 222, 128)
    $gb = New-Object System.Drawing.Drawing2D.LinearGradientBrush($arcRect, $cLight, $cDark, 90)
    $apen = New-Object System.Drawing.Pen($gb, [float]$w)
    $g.DrawArc($apen, $arcRect, -45, 270)
    $apen.Dispose(); $gb.Dispose()

    # white Q
    $fontSize = [float]($size * 0.52)
    $font = New-Object System.Drawing.Font("Segoe UI", $fontSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("Q", $font, $brush, (New-Object System.Drawing.RectangleF(0, -1, $size, $size)), $sf)
    $g.Dispose(); $font.Dispose(); $brush.Dispose(); $sf.Dispose()
    return $bmp
}

function New-BmpFrame([System.Drawing.Bitmap]$bmp) {
    $w = $bmp.Width; $h = $bmp.Height
    $rowSize = $w * 4
    $pixels = New-Object byte[] ($rowSize * $h)
    for ($y = 0; $y -lt $h; $y++) {
        for ($x = 0; $x -lt $w; $x++) {
            $c = $bmp.GetPixel($x, $h - 1 - $y)
            $off = $y * $rowSize + $x * 4
            $pixels[$off] = $c.B; $pixels[$off+1] = $c.G; $pixels[$off+2] = $c.R; $pixels[$off+3] = $c.A
        }
    }
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([int32]40)
    $bw.Write([int32]$w)
    $bw.Write([int32]($h * 2))
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([int32]0)
    $bw.Write([uint32]($rowSize * $h))
    $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([int32]0); $bw.Write([int32]0)
    $bw.Write($pixels)
    $maskRow = [int]((($w + 31) / 32) * 4)
    $bw.Write((New-Object byte[] ($maskRow * $h)))
    $bw.Flush()
    return $ms.ToArray()
}

$out = "D:\SystemFiles\Documents\Project\DEEPSEEK_MONEY\src\QuotaMonitor\app.ico"
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$frames = @()
foreach ($s in $sizes) {
    $bmp = New-IconBitmap $s
    if ($s -eq 256) {
        $pms = New-Object System.IO.MemoryStream
        $bmp.Save($pms, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames += , @{ Data = $pms.ToArray(); W = $s }
    } else {
        $frames += , @{ Data = (New-BmpFrame $bmp); W = $s }
    }
    $bmp.Dispose()
}

$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $w = $f.W
    $bw.Write([byte]($(if ($w -ge 256) { 0 } else { $w })))
    $bw.Write([byte]($(if ($w -ge 256) { 0 } else { $w })))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$f.Data.Length); $bw.Write([uint32]$offset)
    $offset += $f.Data.Length
}
foreach ($f in $frames) { $bw.Write([byte[]]$f.Data) }
$bw.Flush()
[System.IO.File]::WriteAllBytes($out, $ms.ToArray())
$size = (Get-Item $out).Length
Write-Host "icon saved: $out ($size bytes)"
