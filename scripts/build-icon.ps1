$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$brandDirectory = Join-Path $PSScriptRoot '..\src\PTBox.Launcher\Assets\Brand'
$source = [System.Drawing.Bitmap]::FromFile((Join-Path $brandDirectory 'ptbox-icon.png'))
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = New-Object 'System.Collections.Generic.List[byte[]]'
try {
    foreach ($size in $sizes) {
        $bitmap = New-Object System.Drawing.Bitmap($size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb))
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $buffer = New-Object System.IO.MemoryStream
        try {
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source, 0, 0, $size, $size)
            $bitmap.Save($buffer, [System.Drawing.Imaging.ImageFormat]::Png)
            $frames.Add($buffer.ToArray())
        } finally { $buffer.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
    }
} finally { $source.Dispose() }
# Windows ICO directory followed by PNG frames; 0 encodes a 256-pixel dimension.
$output = Join-Path $brandDirectory 'ptbox.ico'
$stream = [System.IO.File]::Create($output)
$writer = New-Object System.IO.BinaryWriter($stream)
try {
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -eq 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
        $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
        $offset += $frames[$i].Length
    }
    foreach ($frame in $frames) { $writer.Write($frame) }
} finally { $writer.Dispose(); $stream.Dispose() }
Write-Output "Generated: $output"
