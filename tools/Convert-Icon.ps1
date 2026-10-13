$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$pngPath = Join-Path $root 'diagram.png'
if (-not (Test-Path $pngPath)) { throw "diagram.png not found at $pngPath" }

$assets = Join-Path $root 'Assets'
New-Item -ItemType Directory -Force -Path $assets | Out-Null
Copy-Item $pngPath (Join-Path $assets 'diagram.png') -Force

$sizes = @(16, 32, 48, 64, 128, 256)
$src = [System.Drawing.Bitmap]::FromFile($pngPath)
try {
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$sizes.Count)

    $imageData = New-Object 'System.Collections.Generic.List[byte[]]'
    $offset = 6 + (16 * $sizes.Count)

    foreach ($size in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap $size, $size
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $g.Clear([System.Drawing.Color]::Transparent)
        $g.DrawImage($src, 0, 0, $size, $size)
        $g.Dispose()

        $pngMs = New-Object System.IO.MemoryStream
        $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $pngMs.ToArray()
        $pngMs.Dispose()
        $bmp.Dispose()
        $imageData.Add($bytes)

        $w = 0
        $h = 0
        if ($size -lt 256) { $w = $size; $h = $size }
        $bw.Write([byte]$w)
        $bw.Write([byte]$h)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $bw.Write([uint32]$bytes.Length)
        $bw.Write([uint32]$offset)
        $offset += $bytes.Length
    }

    foreach ($bytes in $imageData) { $bw.Write($bytes) }
    $bw.Flush()
    $icoPath = Join-Path $root 'app.ico'
    [System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
    Copy-Item $icoPath (Join-Path $assets 'app.ico') -Force
    $bw.Dispose()
    $ms.Dispose()
    Write-Output "Created $icoPath"
}
finally {
    $src.Dispose()
}
