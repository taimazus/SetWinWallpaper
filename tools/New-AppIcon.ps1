Add-Type -AssemblyName System.Drawing

function Render-LogoLayer([int]$size) {
    $bmp = [Drawing.Bitmap]::new($size, $size, [Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([Drawing.Color]::Transparent)

    $scale = $size / 256.0
    $g.ScaleTransform($scale, $scale)

    # Background rounded container with modern dark teal-navy gradient
    $rect = [Drawing.RectangleF]::new(8, 8, 240, 240)
    $path = [Drawing.Drawing2D.GraphicsPath]::new()
    $r = 54.0
    $path.AddArc(8, 8, $r, $r, 180, 90)
    $path.AddArc(248 - $r, 8, $r, $r, 270, 90)
    $path.AddArc(248 - $r, 248 - $r, $r, $r, 0, 90)
    $path.AddArc(8, 248 - $r, $r, $r, 90, 90)
    $path.CloseFigure()

    $bgBrush = [Drawing.Drawing2D.LinearGradientBrush]::new(
        [Drawing.PointF]::new(128, 8),
        [Drawing.PointF]::new(128, 248),
        [Drawing.ColorTranslator]::FromHtml('#0E2330'),
        [Drawing.ColorTranslator]::FromHtml('#07131B')
    )
    $g.FillPath($bgBrush, $path)

    # Subtle glowing outer/inner border
    $borderPen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#2D5468'), 4)
    $g.DrawPath($borderPen, $path)

    # Glowing Sun / Dawn Aura
    $sunAura = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(40, 255, 190, 70))
    $g.FillEllipse($sunAura, 136, 38, 72, 72)

    # Sun core (Warm golden radiant gradient)
    $sunBrush = [Drawing.Drawing2D.LinearGradientBrush]::new(
        [Drawing.PointF]::new(152, 48),
        [Drawing.PointF]::new(192, 88),
        [Drawing.ColorTranslator]::FromHtml('#FFE28A'),
        [Drawing.ColorTranslator]::FromHtml('#FFA62B')
    )
    $g.FillEllipse($sunBrush, 146, 48, 52, 52)

    # Secondary background mountain peak (Rich cyan-teal)
    $backMountBrush = [Drawing.Drawing2D.LinearGradientBrush]::new(
        [Drawing.PointF]::new(176, 108),
        [Drawing.PointF]::new(176, 188),
        [Drawing.ColorTranslator]::FromHtml('#1F8C78'),
        [Drawing.ColorTranslator]::FromHtml('#124A42')
    )
    $g.FillPolygon($backMountBrush, [Drawing.PointF[]]@(
        [Drawing.PointF]::new(118, 188),
        [Drawing.PointF]::new(176, 108),
        [Drawing.PointF]::new(228, 188)
    ))

    # Main Sahand Mountain Peak (Emerald / Mint Green gradient)
    $mountBrush = [Drawing.Drawing2D.LinearGradientBrush]::new(
        [Drawing.PointF]::new(96, 78),
        [Drawing.PointF]::new(96, 188),
        [Drawing.ColorTranslator]::FromHtml('#55E2B9'),
        [Drawing.ColorTranslator]::FromHtml('#1A7866')
    )
    $g.FillPolygon($mountBrush, [Drawing.PointF[]]@(
        [Drawing.PointF]::new(28, 188),
        [Drawing.PointF]::new(96, 78),
        [Drawing.PointF]::new(168, 188)
    ))

    # Snow cap on Sahand mountain peak (Pristine crisp white/cyan)
    $snowBrush = [Drawing.Drawing2D.LinearGradientBrush]::new(
        [Drawing.PointF]::new(96, 78),
        [Drawing.PointF]::new(96, 118),
        [Drawing.ColorTranslator]::FromHtml('#FFFFFF'),
        [Drawing.ColorTranslator]::FromHtml('#E2FAF4')
    )
    $g.FillPolygon($snowBrush, [Drawing.PointF[]]@(
        [Drawing.PointF]::new(96, 78),
        [Drawing.PointF]::new(74, 114),
        [Drawing.PointF]::new(95, 107),
        [Drawing.PointF]::new(118, 117)
    ))

    # Snow shadow facet
    $snowShadow = [Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(60, 20, 100, 90))
    $g.FillPolygon($snowShadow, [Drawing.PointF[]]@(
        [Drawing.PointF]::new(96, 78),
        [Drawing.PointF]::new(95, 107),
        [Drawing.PointF]::new(118, 117)
    ))

    # Tech / Automation Horizon Wave at bottom
    $wavePen = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#72EAD0'), 6)
    $wavePen.StartCap = [Drawing.Drawing2D.LineCap]::Round
    $wavePen.EndCap = [Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($wavePen, 38, 206, 218, 206)

    # Clean up GDI handles
    $wavePen.Dispose()
    $snowShadow.Dispose()
    $snowBrush.Dispose()
    $mountBrush.Dispose()
    $backMountBrush.Dispose()
    $sunBrush.Dispose()
    $sunAura.Dispose()
    $borderPen.Dispose()
    $bgBrush.Dispose()
    $path.Dispose()
    $g.Dispose()

    return $bmp
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$pngBytesList = @()

foreach ($s in $sizes) {
    $bmp = Render-LogoLayer $s
    $ms = [IO.MemoryStream]::new()
    $bmp.Save($ms, [Drawing.Imaging.ImageFormat]::Png)
    $pngBytesList += ,$ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
}

$targetIco = Join-Path $PSScriptRoot '../BingWallpaperPro/Assets/SahandNama.ico'
$writer = [IO.BinaryWriter]::new([IO.File]::Create($targetIco))
try {
    # ICO Header: 0=Reserved, 1=Type (1 for ICO), Count
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$sizes.Count)

    $offset = 6 + (16 * $sizes.Count)
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dim = if ($sizes[$i] -ge 256) { [byte]0 } else { [byte]$sizes[$i] }
        $writer.Write([byte]$dim)            # Width
        $writer.Write([byte]$dim)            # Height
        $writer.Write([byte]0)               # Color count
        $writer.Write([byte]0)               # Reserved
        $writer.Write([uint16]1)             # Color planes
        $writer.Write([uint16]32)            # Bits per pixel
        $writer.Write([uint32]$pngBytesList[$i].Length) # Size in bytes
        $writer.Write([uint32]$offset)       # File offset
        $offset += $pngBytesList[$i].Length
    }

    foreach ($bytes in $pngBytesList) {
        $writer.Write([byte[]]$bytes)
    }
} finally {
    $writer.Dispose()
}

# Also save high-res 512x512 PNG for vector-grade image presentation in UI
$targetPng = Join-Path $PSScriptRoot '../BingWallpaperPro/Assets/SahandNama.png'
$largeBmp = Render-LogoLayer 512
$largeBmp.Save($targetPng, [Drawing.Imaging.ImageFormat]::Png)
$largeBmp.Dispose()

Write-Output "Generated ultra-high quality ICO and PNG assets successfully."
