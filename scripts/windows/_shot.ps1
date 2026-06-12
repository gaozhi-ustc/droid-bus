Add-Type -AssemblyName System.Windows.Forms,System.Drawing
$b = [System.Windows.Forms.SystemInformation]::VirtualScreen
$bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
$bmp.Save("C:\Users\gaozhi\droid-bus\_screen.png")
$g.Dispose(); $bmp.Dispose()
Write-Output ("saved {0}x{1}" -f $b.Width, $b.Height)
