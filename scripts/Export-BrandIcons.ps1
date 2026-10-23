# Rebuild the Windows icons from the same scalable geometry used in the WPF shell.
# Run with Windows PowerShell in STA mode. No concept bitmap is used.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore, WindowsBase
$repo = Split-Path $PSScriptRoot -Parent
[xml]$design = Get-Content -LiteralPath (Join-Path $repo 'src/Controlability.App/Resources/Design.xaml') -Raw
$geometryNodes = $design.SelectNodes("//*[local-name()='DrawingImage']//*[local-name()='GeometryDrawing']")
$iconDirectory = Join-Path $repo 'src/Controlability.App/Resources/Icons'
# Light and dark end of the gradient on the C, and the colour of the controller's plus buttons.
$variants = @{
    'app' = @('#5ED2FF', '#1D96F2', '#2AB4F7')
    'tray-normal' = @('#5ED2FF', '#1D96F2', '#2AB4F7')
    'tray-paused' = @('#FCD34D', '#F59E0B', '#FBBF24')
    'tray-error' = @('#FDA4AF', '#F43F5E', '#FB7185')
}
foreach ($variant in $variants.GetEnumerator()) {
    $frames = @()
    foreach ($size in @(16, 20, 24, 32, 48, 64, 128, 256)) {
        $visual = New-Object System.Windows.Media.DrawingVisual
        $drawing = $visual.RenderOpen()
        $background = [System.Windows.Media.BrushConverter]::new().ConvertFromString('#0B1420')
        $convert = [System.Windows.Media.BrushConverter]::new()
        $top = $variant.Value[0]; $bottom = $variant.Value[1]
        $gradient = [System.Windows.Media.LinearGradientBrush]::new([System.Windows.Media.ColorConverter]::ConvertFromString($top), [System.Windows.Media.ColorConverter]::ConvertFromString($bottom), 90)
        $buttons = $convert.ConvertFromString($variant.Value[2])
        $pen = [System.Windows.Media.Pen]::new($gradient, 2.4)
        $pen.LineJoin = [System.Windows.Media.PenLineJoin]::Round
        $drawing.DrawRoundedRectangle($background, $null, [System.Windows.Rect]::new(0, 0, $size, $size), $size * 0.2, $size * 0.2)
        $drawing.PushTransform([System.Windows.Media.TranslateTransform]::new($size * 0.135, $size * 0.085))
        $drawing.PushTransform([System.Windows.Media.ScaleTransform]::new($size * 0.0082, $size * 0.0082))
        # The first geometry is the C with the controller cut out, the second the controller's plus buttons.
        $drawing.DrawGeometry($gradient, $pen, [System.Windows.Media.Geometry]::Parse($geometryNodes[0].Geometry))
        $drawing.DrawGeometry($buttons, $null, [System.Windows.Media.Geometry]::Parse($geometryNodes[1].Geometry))
        $drawing.Pop(); $drawing.Pop(); $drawing.Close()
        $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new($size, $size, 96, 96, [System.Windows.Media.PixelFormats]::Pbgra32)
        $bitmap.Render($visual)
        $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
        $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
        $stream = [System.IO.MemoryStream]::new()
        $encoder.Save($stream)
        $frames += @{ Size = $size; Bytes = $stream.ToArray() }
        $stream.Dispose()
    }
    $file = [System.IO.File]::Create((Join-Path $iconDirectory ($variant.Key + '.ico')))
    $writer = [System.IO.BinaryWriter]::new($file)
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
        $offset = 6 + 16 * $frames.Count
        foreach ($frame in $frames) {
            $dimension = $frame.Size % 256
            $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
            $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
            $offset += $frame.Bytes.Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
    } finally { $writer.Dispose(); $file.Dispose() }
}
