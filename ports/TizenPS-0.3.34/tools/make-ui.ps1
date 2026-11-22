param([string]$Only = '')
Add-Type -AssemblyName System.Drawing
$assetRoot = Join-Path $PSScriptRoot '../roelofs-psx-native/res/ui'
New-Item -ItemType Directory -Force $assetRoot | Out-Null
function Save-Asset($name, $width, $height, $paint) {
 if($Only -and $name -ne $Only){return}
 $bitmap = [Drawing.Bitmap]::new($width,$height)
 $graphics = [Drawing.Graphics]::FromImage($bitmap)
 $graphics.SmoothingMode = 'AntiAlias'
 & $paint $graphics $width $height
 $bitmap.Save((Join-Path $assetRoot $name),[Drawing.Imaging.ImageFormat]::Png)
 $graphics.Dispose(); $bitmap.Dispose()
}
Save-Asset 'background.png' 1920 1080 {
 param($g,$w,$h)
 $g.Clear([Drawing.Color]::FromArgb(9,9,20))
 $rect=[Drawing.Rectangle]::new(0,0,$w,$h)
 $brush=[Drawing.Drawing2D.LinearGradientBrush]::new($rect,[Drawing.Color]::FromArgb(17,12,29),[Drawing.Color]::FromArgb(8,10,19),30)
 $g.FillRectangle($brush,$rect);$brush.Dispose()
 for($i=200;$i -ge 1;$i--){
  $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(2,143,42,201))
  $g.FillEllipse($brush,[single](1430-$i*3),[single](440-$i*2.5),[single]($i*6),[single]($i*5));$brush.Dispose()
 }
 $pen=[Drawing.Pen]::new([Drawing.Color]::FromArgb(70,140,83,190),1)
 $g.DrawLine($pen,72,169,1848,169);$g.DrawLine($pen,72,995,1848,995);$pen.Dispose()
 for($i=0;$i -lt 9;$i++){
  $pen=[Drawing.Pen]::new([Drawing.Color]::FromArgb((30-$i*2),168,80,228),1)
  $g.DrawBezier($pen,0,(730+$i*12),680,(570+$i*20),1120,(1090-$i*9),1920,(690+$i*8));$pen.Dispose()
 }
}
foreach($name in @('tab.png','tab-focus.png','button-focus.png','hero-frame.png','cover-focus.png')) {
 Save-Asset $name 520 480 {
  param($g,$w,$h)
  $g.Clear([Drawing.Color]::Transparent)
  $fill=if($name -eq 'hero-frame.png'){[Drawing.Color]::FromArgb(245,17,13,25)}else{[Drawing.Color]::FromArgb(255,54,26,80)}
  $brush=[Drawing.SolidBrush]::new($fill);$g.FillRectangle($brush,3,3,514,474);$brush.Dispose()
  $col=if($name -eq 'tab.png'){[Drawing.Color]::FromArgb(105,66,133)}else{[Drawing.Color]::FromArgb(196,126,255)}
  $pen=[Drawing.Pen]::new($col,3);$g.DrawRectangle($pen,3,3,514,474);$pen.Dispose()
 }
}
Save-Asset 'missing.png' 512 512 {
 param($g,$w,$h)
 $g.Clear([Drawing.Color]::FromArgb(28,22,44))
 $font=[Drawing.Font]::new('Segoe UI',32)
 $brush=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(180,154,207))
 $g.DrawString('TizenPSX',$font,$brush,128,185)
 $font.Dispose();$font=[Drawing.Font]::new('Segoe UI',15)
 $g.DrawString('Add your cover art',$font,$brush,160,260)
 $font.Dispose();$brush.Dispose()
}
