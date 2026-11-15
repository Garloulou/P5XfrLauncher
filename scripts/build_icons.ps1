Add-Type -AssemblyName System.Drawing

$sourcePath = Join-Path $PSScriptRoot "..\Assets\Image_App_P5X.jpg"
$icoPath = Join-Path $PSScriptRoot "..\Assets\AppIcon.ico"
$pngPath = Join-Path $PSScriptRoot "..\Assets\AppIcon.png"

Write-Host "Reading source from: $sourcePath"
$source = [System.Drawing.Image]::FromFile($sourcePath)

# Save PNG version
$source.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved PNG: $pngPath"

# Sizes to generate in ICO
$sizes = @(256, 128, 64, 48, 32, 24, 16)

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ms

# ICO header
$bw.Write([UInt16]0)
$bw.Write([UInt16]1)
$bw.Write([UInt16]$sizes.Count)

$dataList = @()
foreach ($s in $sizes) {
    $destBmp = New-Object System.Drawing.Bitmap $s, $s, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($destBmp)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.DrawImage($source, 0, 0, $s, $s)
    $g.Dispose()

    $pms = New-Object System.IO.MemoryStream
    $destBmp.Save($pms, [System.Drawing.Imaging.ImageFormat]::Png)
    $destBmp.Dispose()
    $dataList += ,$pms.ToArray()
    $pms.Dispose()
}

$offset = 6 + (16 * $sizes.Count)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bWidth = if ($s -ge 256) { [byte]0 } else { [byte]$s }
    $bHeight = if ($s -ge 256) { [byte]0 } else { [byte]$s }
    $bw.Write($bWidth)
    $bw.Write($bHeight)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([UInt16]1)
    $bw.Write([UInt16]32)
    $len = $dataList[$i].Length
    $bw.Write([UInt32]$len)
    $bw.Write([UInt32]$offset)
    $offset += $len
}

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $bytes = $dataList[$i]
    $bw.Write($bytes, 0, $bytes.Length)
}

$source.Dispose()
$bw.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
$ms.Dispose()

Write-Host "Successfully generated ICO: $icoPath ($((Get-Item $icoPath).Length) bytes)"
Write-Host "Successfully generated PNG: $pngPath ($((Get-Item $pngPath).Length) bytes)"
