param()
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot=Split-Path $PSScriptRoot -Parent
$source=[System.Drawing.Image]::FromFile((Join-Path $projectRoot 'art/tizenpsx-icon-master.png'))
$bitmap=[System.Drawing.Bitmap]::new(117,117)
$graphics=[System.Drawing.Graphics]::FromImage($bitmap)
$graphics.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$graphics.CompositingQuality=[System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$graphics.DrawImage($source,0,0,117,117)
$bitmap.Save((Join-Path $projectRoot 'roelofs-psx-native/shared/res/PSXNative.png'),[System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose();$bitmap.Dispose();$source.Dispose()
Write-Output 'PASS: generated icon master downscaled to the existing 117x117 package dimensions.'
