# Build a multi-resolution Windows .ico from the compact, lossless icon PNG.
# The source PNG was generated specifically for KNULLI USB Link and is stored
# as UTF-8 base64 text so GitHub's text-only file tools can track it.
param(
    [string]$Source = (Join-Path $PSScriptRoot '../assets/knulli-usb-link-icon.png.base64'),
    [string]$Output = (Join-Path $PSScriptRoot '../assets/knulli-usb-link.ico')
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$pngBytes = [Convert]::FromBase64String(
    ([IO.File]::ReadAllText((Resolve-Path $Source).Path)).Trim())
$sourceStream = New-Object IO.MemoryStream(,$pngBytes)
$sourceImage = [Drawing.Image]::FromStream($sourceStream)
try {
    $sizes = @(16,24,32,48,64,128,256)
    $frames = New-Object 'System.Collections.Generic.List[byte[]]'
    foreach ($size in $sizes) {
        $bmp = New-Object Drawing.Bitmap($size,$size)
        try {
            $g = [Drawing.Graphics]::FromImage($bmp)
            try {
                $g.Clear([Drawing.Color]::Transparent)
                $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $g.CompositingQuality = [Drawing.Drawing2D.CompositingQuality]::HighQuality
                $g.SmoothingMode = [Drawing.Drawing2D.SmoothingMode]::AntiAlias
                $g.DrawImage($sourceImage,0,0,$size,$size)
            }
            finally { $g.Dispose() }
            $stream = New-Object IO.MemoryStream
            try {
                $bmp.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
                $frames.Add($stream.ToArray())
            }
            finally { $stream.Dispose() }
        }
        finally { $bmp.Dispose() }
    }
    $folder = Split-Path -Parent $Output
    if (-not (Test-Path -LiteralPath $folder)) {
        [void](New-Item -ItemType Directory -Force -Path $folder)
    }
    $outputStream = [IO.File]::Create($Output)
    try {
        $writer = New-Object IO.BinaryWriter($outputStream)
        try {
            $writer.Write([UInt16]0)
            $writer.Write([UInt16]1)
            $writer.Write([UInt16]$sizes.Count)
            $offset = [UInt32](6 + 16 * $sizes.Count)
            for ($i=0; $i -lt $sizes.Count; $i++) {
                $size = $sizes[$i]
                $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
                $writer.Write([byte]$(if ($size -eq 256) { 0 } else { $size }))
                $writer.Write([byte]0)
                $writer.Write([byte]0)
                $writer.Write([UInt16]1)
                $writer.Write([UInt16]32)
                $writer.Write([UInt32]$frames[$i].Length)
                $writer.Write([UInt32]$offset)
                $offset += [UInt32]$frames[$i].Length
            }
            foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
        }
        finally { $writer.Dispose() }
    }
    finally { $outputStream.Dispose() }
    Write-Host "Generated KNULLI USB Link icon: $Output"
}
finally {
    $sourceImage.Dispose()
    $sourceStream.Dispose()
}
