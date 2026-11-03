param([string]$Source,[string]$Destination)
Add-Type -AssemblyName System.Drawing
$sourceImage=[Drawing.Image]::FromFile([IO.Path]::GetFullPath($Source))
$frames=@()
try {
 foreach($size in @(16,24,32,48,64,128,256)){
  $bitmap=[Drawing.Bitmap]::new($size,$size)
  $graphics=[Drawing.Graphics]::FromImage($bitmap)
  try{ $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic;$graphics.Clear([Drawing.Color]::Transparent);$scale=[Math]::Min($size/$sourceImage.Width,$size/$sourceImage.Height);$width=[int]($sourceImage.Width*$scale);$height=[int]($sourceImage.Height*$scale);$graphics.DrawImage($sourceImage,[int](($size-$width)/2),[int](($size-$height)/2),$width,$height);$stream=[IO.MemoryStream]::new();try{$bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png);$frames+=,[pscustomobject]@{size=$size;bytes=$stream.ToArray()}}finally{$stream.Dispose()} }finally{$graphics.Dispose();$bitmap.Dispose()}
 }
 $writer=[IO.BinaryWriter]::new([IO.File]::Create([IO.Path]::GetFullPath($Destination)))
 try{ $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$frames.Count);$offset=6+16*$frames.Count;foreach($frame in $frames){$dimension=if($frame.size -eq 256){0}else{$frame.size};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frame.bytes.Length);$writer.Write([uint32]$offset);$offset+=$frame.bytes.Length};foreach($frame in $frames){$writer.Write([byte[]]$frame.bytes)} }finally{$writer.Dispose()}
}finally{$sourceImage.Dispose()}
