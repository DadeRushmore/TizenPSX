param([string]$InputJson='../outputs/v036-scenes.json',[string]$OutputDirectory='../outputs/v036-previews')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=Join-Path $PSScriptRoot $InputJson
$destination=Join-Path $PSScriptRoot $OutputDirectory
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$scenes=Get-Content -LiteralPath $source -Raw | ConvertFrom-Json
foreach($scene in $scenes.PSObject.Properties) {
 $bitmap=[Drawing.Bitmap]::new(1920,1080);$g=[Drawing.Graphics]::FromImage($bitmap)
 $g.Clear([Drawing.Color]::FromArgb(8,10,22));$g.TextRenderingHint='AntiAliasGridFit';$g.InterpolationMode='HighQualityBilinear'
 foreach($node in $scene.Value) {
  if($node.Kind -eq 'image') {
   $asset=[Drawing.Image]::FromFile($node.Content)
   if($node.DecodeWidth -gt 0) {
    $small=[Drawing.Bitmap]::new([int]$node.DecodeWidth,[int]$node.DecodeHeight)
    $sg=[Drawing.Graphics]::FromImage($small);$sg.DrawImage($asset,0,0,$small.Width,$small.Height);$sg.Dispose();$asset.Dispose();$asset=$small
   }
   $x=[single]$node.X;$y=[single]$node.Y;$w=[single]$node.W;$h=[single]$node.H
   if($node.Contain){$scale=[Math]::Min($w/$asset.Width,$h/$asset.Height);$nw=$asset.Width*$scale;$nh=$asset.Height*$scale;$x+=($w-$nw)/2;$y+=($h-$nh)/2;$w=$nw;$h=$nh}
   $attrs=[Drawing.Imaging.ImageAttributes]::new()
   if($node.Tint) {
    $matrix=[Drawing.Imaging.ColorMatrix]::new();$alpha=[single]$node.Tint[3]
    $matrix.Matrix00=$node.Tint[0]/$alpha;$matrix.Matrix11=$node.Tint[1]/$alpha;$matrix.Matrix22=$node.Tint[2]/$alpha;$matrix.Matrix33=$alpha/255
    $attrs.SetColorMatrix($matrix)
   }
   $rect=[Drawing.Rectangle]::new([int]$x,[int]$y,[int]$w,[int]$h)
   $g.DrawImage($asset,$rect,0,0,$asset.Width,$asset.Height,[Drawing.GraphicsUnit]::Pixel,$attrs)
   $attrs.Dispose();$asset.Dispose()
  } else {
   $font=[Drawing.Font]::new('Segoe UI',[single]$node.Size,[Drawing.FontStyle]::Regular,[Drawing.GraphicsUnit]::Pixel)
   $brush=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml($node.Color));$format=[Drawing.StringFormat]::new();$format.Trimming='EllipsisCharacter'
   $rect=[Drawing.RectangleF]::new($node.X,$node.Y,$node.W,$node.H)
   $measured=$g.MeasureString($node.Content,$font,[int]$node.W,$format)
   if($measured.Height -gt $node.H+3){throw "Text overflow in $($scene.Name): $($node.Content)"}
   $g.DrawString($node.Content,$font,$brush,$rect,$format);$font.Dispose();$brush.Dispose();$format.Dispose()
  }
 }
 $bitmap.Save((Join-Path $destination ($scene.Name+'.png')),[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$bitmap.Dispose()
}
Write-Output "PASS: rendered $($scenes.PSObject.Properties.Name.Count) native scene previews; text bounds verified."
