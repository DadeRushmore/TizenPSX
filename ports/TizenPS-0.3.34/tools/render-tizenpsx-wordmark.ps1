$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$projectRoot = Split-Path $PSScriptRoot -Parent
$bitmap = [Drawing.Bitmap]::new(520,152)
$graphics = [Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = 'AntiAlias'
$graphics.Clear([Drawing.Color]::Transparent)
$family = [Drawing.FontFamily]::new('Segoe UI')
$format = [Drawing.StringFormat]::GenericTypographic
$plain = [Drawing.Drawing2D.GraphicsPath]::new()
$plain.AddString('Tizen',$family,[int][Drawing.FontStyle]::Bold,104,[Drawing.PointF]::new(-5,20),$format)
$white = [Drawing.SolidBrush]::new([Drawing.Color]::White)
$graphics.FillPath($white,$plain)
$colorText = [Drawing.Drawing2D.GraphicsPath]::new()
$colorText.AddString('PSX',$family,[int][Drawing.FontStyle]::Bold,104,[Drawing.PointF]::new(($plain.GetBounds().Right-3),20),$format)
$graphics.FillPath($white,$colorText)
$target = Join-Path $projectRoot 'roelofs-psx-native/res/ui/tizenps-wordmark.png'
$bitmap.Save($target,[Drawing.Imaging.ImageFormat]::Png)
Copy-Item -LiteralPath $target -Destination (Join-Path $projectRoot 'preview/assets/tizenps-wordmark.png')
$colorText.Dispose();$plain.Dispose();$white.Dispose();$family.Dispose();$graphics.Dispose();$bitmap.Dispose()
Write-Output 'Generated transparent TizenPSX wordmark with clean white lettering.'
