# Genera el logo en varios tamanos: src\icono.ico (icono del .exe) y site\img\logo-512.png.
# Ejecutar con Windows PowerShell 5:  powershell.exe -File site\generar-iconos.ps1
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent

function New-LogoBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 64.0

    # Fondo: cuadrado redondeado con degradado azul
    $r = 14 * $s
    $bg = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bg.AddArc(0, 0, 2*$r, 2*$r, 180, 90)
    $bg.AddArc($size - 2*$r, 0, 2*$r, 2*$r, 270, 90)
    $bg.AddArc($size - 2*$r, $size - 2*$r, 2*$r, 2*$r, 0, 90)
    $bg.AddArc(0, $size - 2*$r, 2*$r, 2*$r, 90, 90)
    $bg.CloseFigure()
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush ((New-Object System.Drawing.PointF 0,0), (New-Object System.Drawing.PointF $size,$size), ([System.Drawing.Color]::FromArgb(255,0x1f,0x6f,0xeb)), ([System.Drawing.Color]::FromArgb(255,0x4c,0x9b,0xff)))
    $g.FillPath($grad, $bg)

    # Hoja "copiada" detras del escudo
    $sr = 4 * $s
    $sheet = New-Object System.Drawing.Drawing2D.GraphicsPath
    $x = 11*$s; $y = 17*$s; $w = 24*$s; $h = 31*$s
    $sheet.AddArc($x, $y, 2*$sr, 2*$sr, 180, 90)
    $sheet.AddArc($x + $w - 2*$sr, $y, 2*$sr, 2*$sr, 270, 90)
    $sheet.AddArc($x + $w - 2*$sr, $y + $h - 2*$sr, 2*$sr, 2*$sr, 0, 90)
    $sheet.AddArc($x, $y + $h - 2*$sr, 2*$sr, 2*$sr, 90, 90)
    $sheet.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(95,255,255,255))), $sheet)

    # Escudo blanco
    $sh = New-Object System.Drawing.Drawing2D.GraphicsPath
    $sh.AddLine(40*$s, 13*$s, 55*$s, 19*$s)
    $sh.AddLine(55*$s, 19*$s, 55*$s, 32*$s)
    $sh.AddBezier(55*$s, 32*$s, 55*$s, 42*$s, 48*$s, 48*$s, 40*$s, 52*$s)
    $sh.AddBezier(40*$s, 52*$s, 32*$s, 48*$s, 25*$s, 42*$s, 25*$s, 32*$s)
    $sh.AddLine(25*$s, 32*$s, 25*$s, 19*$s)
    $sh.CloseFigure()
    $g.FillPath([System.Drawing.Brushes]::White, $sh)

    # Check azul
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255,0x1f,0x6f,0xeb)), (4.2 * $s)
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'; $pen.LineJoin = 'Round'
    $g.DrawLines($pen, @((New-Object System.Drawing.PointF (33*$s), (33*$s)), (New-Object System.Drawing.PointF (38.5*$s), (38.5*$s)), (New-Object System.Drawing.PointF (47.5*$s), (26.5*$s))))
    $g.Dispose()
    return $bmp
}

function Get-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return ,$ms.ToArray()
}

# --- ICO con PNG incrustados (Windows Vista en adelante, incluido Windows 7) ---
$sizes = 16, 24, 32, 48, 64, 128, 256
$pngs = @{}
foreach ($sz in $sizes) { $b = New-LogoBitmap $sz; $pngs[$sz] = Get-PngBytes $b; $b.Dispose() }
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
foreach ($sz in $sizes) {
    $dim = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$sz].Length); $bw.Write([uint32]$offset)
    $offset += $pngs[$sz].Length
}
foreach ($sz in $sizes) { $bw.Write($pngs[$sz]) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $root 'src\icono.ico'), $ms.ToArray())

# --- PNG grande para la web ---
New-Item -ItemType Directory -Force (Join-Path $root 'site\img') | Out-Null
$big = New-LogoBitmap 512
$big.Save((Join-Path $root 'site\img\logo-512.png'), [System.Drawing.Imaging.ImageFormat]::Png)
$big.Dispose()
Write-Host "Iconos generados: src\icono.ico y site\img\logo-512.png"
