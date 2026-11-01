param([string]$OutputPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'icon.png'))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$canvas = [Drawing.Bitmap]::new(1024, 1024)
$g = [Drawing.Graphics]::FromImage($canvas)
$g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([Drawing.ColorTranslator]::FromHtml('#101719'))
$green = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#A4FF60'))
$muted = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#6D9182'))
$white = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#EDF6EB'))
$orange = [Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#FF824A'))
$frame = [Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml('#385346'), 8)
$g.DrawRectangle($frame, 38, 38, 948, 948)
for ($y = 64; $y -lt 960; $y += 32) {
    $line = [Drawing.Pen]::new([Drawing.Color]::FromArgb(16, 144, 218, 147), 2)
    $g.DrawLine($line, 56, $y, 968, $y)
    $line.Dispose()
}
$label = [Drawing.Font]::new('Consolas', 36, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
$g.DrawString('R.E.P.O.', $label, $muted, 84, 74)
$g.FillRectangle($orange, 876, 84, 28, 28)
$g.FillRectangle($orange, 916, 84, 28, 28)
# Three rising steps and an upward arrow: a code-authored symbol for difficulty growth.
$g.FillRectangle($muted, 142, 472, 148, 176)
$g.FillRectangle($green, 334, 366, 148, 282)
$g.FillRectangle($green, 526, 260, 148, 388)
$arrow = [Drawing.Point[]]@(
    [Drawing.Point]::new(754, 476), [Drawing.Point]::new(754, 316),
    [Drawing.Point]::new(698, 316), [Drawing.Point]::new(810, 172),
    [Drawing.Point]::new(922, 316), [Drawing.Point]::new(866, 316),
    [Drawing.Point]::new(866, 476)
)
$g.FillPolygon($orange, $arrow)
$title = [Drawing.Font]::new('Consolas', 176, [Drawing.FontStyle]::Bold, [Drawing.GraphicsUnit]::Pixel)
$format = [Drawing.StringFormat]::new()
$format.Alignment = [Drawing.StringAlignment]::Center
$g.DrawString('IDM', $title, $white, [Drawing.RectangleF]::new(56, 678, 912, 210), $format)
$g.FillRectangle($green, 144, 904, 736, 12)
$output = [Drawing.Bitmap]::new(256, 256)
$og = [Drawing.Graphics]::FromImage($output)
$og.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$og.DrawImage($canvas, 0, 0, 256, 256)
$output.Save([IO.Path]::GetFullPath($OutputPath), [Drawing.Imaging.ImageFormat]::Png)
$og.Dispose(); $output.Dispose(); $g.Dispose(); $canvas.Dispose()
$green.Dispose(); $muted.Dispose(); $white.Dispose(); $orange.Dispose()
$frame.Dispose(); $label.Dispose(); $title.Dispose(); $format.Dispose()
