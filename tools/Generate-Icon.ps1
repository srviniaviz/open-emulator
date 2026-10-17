# Regenerate the native Windows icon and its preview; no external dependencies.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$assets = Join-Path $PSScriptRoot '../assets'
New-Item -ItemType Directory -Force $assets | Out-Null

function New-RoundedPath([float]$X, [float]$Y, [float]$Width, [float]$Height, [float]$Radius) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = 2 * $Radius
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc(($X + $Width - $diameter), $Y, $diameter, $diameter, 270, 90)
    $path.AddArc(($X + $Width - $diameter), ($Y + $Height - $diameter), $diameter, $diameter, 0, 90)
    $path.AddArc($X, ($Y + $Height - $diameter), $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return ,$path
}

$frames = @()
foreach ($size in @(16, 20, 24, 32, 40, 48, 64, 128, 256)) {
    $canvas = New-Object System.Drawing.Bitmap ($size * 4), ($size * 4)
    $graphics = [Drawing.Graphics]::FromImage($canvas)
    $background = New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#14261E'))
    $green = New-Object Drawing.SolidBrush ([Drawing.ColorTranslator]::FromHtml('#72EA91'))
    $white = New-Object Drawing.SolidBrush ([Drawing.Color]::White)
    $tile = New-RoundedPath 8 8 240 240 52
    $phone = New-RoundedPath 64 28 128 200 23
    $screen = New-RoundedPath 76 46 104 154 12
    $homeIndicator = New-RoundedPath 110 211 36 5 2.5
    $bitmap = $null
    $stream = New-Object IO.MemoryStream
    try {
        $graphics.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.ScaleTransform(($size * 4 / 256.0), ($size * 4 / 256.0))
        $graphics.FillPath($background, $tile)
        $graphics.FillPath($green, $phone)
        $graphics.FillPath($background, $screen)
        $graphics.FillPath($background, $homeIndicator)
        $triangle = [Drawing.PointF[]]@([Drawing.PointF]::new(109, 88), [Drawing.PointF]::new(109, 157), [Drawing.PointF]::new(157, 122.5))
        $graphics.FillPolygon($white, $triangle)
        $bitmap = New-Object Drawing.Bitmap $size, $size
        $resize = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $resize.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $resize.DrawImage($canvas, 0, 0, $size, $size)
        } finally { $resize.Dispose() }
        $bitmap.Save($stream, [Drawing.Imaging.ImageFormat]::Png)
        $frames += [pscustomobject]@{ Size = $size; Bytes = $stream.ToArray() }
        if ($size -eq 256) { $bitmap.Save((Join-Path $assets 'OpenEmulator.png'), [Drawing.Imaging.ImageFormat]::Png) }
    } finally {
        $stream.Dispose()
        if ($bitmap) { $bitmap.Dispose() }
        $graphics.Dispose(); $canvas.Dispose()
        $background.Dispose(); $green.Dispose(); $white.Dispose()
        $tile.Dispose(); $phone.Dispose(); $screen.Dispose(); $homeIndicator.Dispose()
    }
}

$file = [IO.File]::Create((Join-Path $assets 'OpenEmulator.ico'))
$writer = New-Object IO.BinaryWriter $file
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
    $offset = 6 + 16 * $frames.Count
    foreach ($frame in $frames) {
        $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose() }
Write-Output 'Icon generated: assets/OpenEmulator.ico (16–256 px)'
