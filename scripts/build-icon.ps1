# Erzeugt app.ico und app.png aus der Kompassrose.
# Hintergrund: Navy (Sidebar-Ton #1A2F4A), rounded corners, Amber-Gradient-Kompass.
[CmdletBinding()]
param(
    [int[]]$Sizes = @(16, 24, 32, 48, 64, 128, 256),
    [string]$OutDir = (Join-Path $PSScriptRoot '..\src\WindroseServerManager.App\Assets')
)

Add-Type -AssemblyName System.Drawing

$OutDir = (Resolve-Path $OutDir).Path
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function New-CompassBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    # Rounded rect background — Sidebar-Navy
    $radius = [Math]::Max(2, [int]($Size * 0.22))
    $rect = New-Object System.Drawing.Rectangle(0, 0, $Size, $Size)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 26, 47, 74))  # #1A2F4A
    $g.FillPath($bgBrush, $path)
    $bgBrush.Dispose()

    # Amber gradient brush für Kompass
    $gradRect = New-Object System.Drawing.RectangleF(0, 0, $Size, $Size)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $gradRect,
        [System.Drawing.Color]::FromArgb(255, 245, 176, 66),   # #F5B042
        [System.Drawing.Color]::FromArgb(255, 146, 64, 14),    # #92400E
        45.0)

    # Viewbox: wir mappen 0..64 (SVG-Koordinaten) auf 0..Size mit Padding
    $pad = [int]($Size * 0.14)
    $innerSize = $Size - ($pad * 2)
    $scale = $innerSize / 64.0
    $offset = $pad

    function ToPoint($x, $y) {
        return New-Object System.Drawing.PointF(($offset + $x * $scale), ($offset + $y * $scale))
    }

    # Äußerer Ring
    $outerPen = New-Object System.Drawing.Pen($grad, [float]($Size / 32.0))
    $g.DrawEllipse($outerPen, [float]($offset + 4 * $scale), [float]($offset + 4 * $scale), [float](56 * $scale), [float](56 * $scale))
    $outerPen.Dispose()

    # Innerer Ring
    $innerPen = New-Object System.Drawing.Pen($grad, [float]($Size / 48.0))
    $g.DrawEllipse($innerPen, [float]($offset + 10 * $scale), [float]($offset + 10 * $scale), [float](44 * $scale), [float](44 * $scale))
    $innerPen.Dispose()

    # 8 Kompass-Pfeile als Polygone
    $polys = @(
        @(@(32,6), @(29,32), @(32,30), @(35,32)),   # N
        @(@(32,58), @(29,32), @(32,34), @(35,32)),  # S
        @(@(6,32), @(32,29), @(30,32), @(32,35)),   # W
        @(@(58,32), @(32,29), @(34,32), @(32,35)),  # E
        @(@(14,14), @(31,31), @(32,28), @(28,32)),  # NW
        @(@(50,14), @(33,31), @(32,28), @(36,32)),  # NE
        @(@(14,50), @(31,33), @(28,32), @(32,36)),  # SW
        @(@(50,50), @(33,33), @(36,32), @(32,36))   # SE
    )

    foreach ($poly in $polys) {
        $pts = $poly | ForEach-Object { ToPoint $_[0] $_[1] }
        $g.FillPolygon($grad, $pts)
    }

    # Zentrum-Punkt
    $centerRadius = 2.5 * $scale
    $cx = $offset + 32 * $scale - $centerRadius
    $cy = $offset + 32 * $scale - $centerRadius
    $g.FillEllipse($grad, [float]$cx, [float]$cy, [float]($centerRadius * 2), [float]($centerRadius * 2))

    $grad.Dispose()
    $g.Dispose()
    return $bmp
}

function Save-MultiSizeIco {
    param([string]$Path, [int[]]$Sizes)

    $bitmaps = @()
    foreach ($s in $Sizes) { $bitmaps += ,(New-CompassBitmap -Size $s) }

    # PNG-Variante jeder Größe erzeugen
    $pngBytes = @()
    foreach ($bmp in $bitmaps) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBytes += ,($ms.ToArray())
        $ms.Dispose()
    }

    $stream = [System.IO.File]::OpenWrite($Path)
    $writer = New-Object System.IO.BinaryWriter($stream)

    # ICO-Header
    $writer.Write([uint16]0)              # Reserved
    $writer.Write([uint16]1)              # Type 1 = ICO
    $writer.Write([uint16]$Sizes.Length)  # Anzahl Images

    $headerSize = 6 + (16 * $Sizes.Length)
    $offset = $headerSize

    for ($i = 0; $i -lt $Sizes.Length; $i++) {
        $size = $Sizes[$i]
        $bytes = $pngBytes[$i]

        $writeSize = if ($size -ge 256) { 0 } else { $size }  # 0 = 256
        $writer.Write([byte]$writeSize)   # Width
        $writer.Write([byte]$writeSize)   # Height
        $writer.Write([byte]0)            # ColorCount
        $writer.Write([byte]0)            # Reserved
        $writer.Write([uint16]1)          # Planes
        $writer.Write([uint16]32)         # BitCount
        $writer.Write([uint32]$bytes.Length)   # BytesInRes
        $writer.Write([uint32]$offset)    # ImageOffset
        $offset += $bytes.Length
    }

    foreach ($bytes in $pngBytes) {
        $writer.Write($bytes)
    }

    $writer.Dispose()
    $stream.Dispose()
    foreach ($bmp in $bitmaps) { $bmp.Dispose() }
}

$icoPath = Join-Path $OutDir 'app.ico'
Save-MultiSizeIco -Path $icoPath -Sizes $Sizes
Write-Host "OK$icoPath ($((Get-Item $icoPath).Length) bytes)" -ForegroundColor Green

# Zusätzlich 256er-PNG für TrayIcon-Fallback
$png256 = New-CompassBitmap -Size 256
$pngPath = Join-Path $OutDir 'app-256.png'
$png256.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
$png256.Dispose()
Write-Host "OK$pngPath" -ForegroundColor Green
