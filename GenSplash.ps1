Add-Type -AssemblyName System.Drawing

$bmp = New-Object System.Drawing.Bitmap(600, 400)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([System.Drawing.Color]::White)

$titleFont = New-Object System.Drawing.Font("Segoe UI", 38, [System.Drawing.FontStyle]::Bold)
$subFont = New-Object System.Drawing.Font("Segoe UI", 16)
$titleColor = [System.Drawing.ColorTranslator]::FromHtml("#5C4B72")
$subColor = [System.Drawing.ColorTranslator]::FromHtml("#718096")

$titleBrush = New-Object System.Drawing.SolidBrush($titleColor)
$subBrush = New-Object System.Drawing.SolidBrush($subColor)

$titleSize = $g.MeasureString("Church Display", $titleFont)
$subSize = $g.MeasureString("Loading...", $subFont)

# Calculate positions slightly lower to make room for logo
$centerX = (600 - $titleSize.Width) / 2
$centerY = 230

# Load icon and draw it
try {
    $icon = New-Object System.Drawing.Icon("c:\Projects\ChurchDisplayApp\icon.ico")
    $iconBmp = $icon.ToBitmap()
    
    # We will draw the icon centered above the text
    # A size of 96x96 is a good proportion
    $iconX = [int]((600 - 96) / 2)
    $iconRect = New-Object System.Drawing.Rectangle($iconX, 110, 96, 96)
    $g.DrawImage($iconBmp, $iconRect)
} catch {
    Write-Host "Warning: Could not load icon.ico - $_"
}

$g.DrawString("Church Display", $titleFont, $titleBrush, $centerX, $centerY)
$g.DrawString("Loading...", $subFont, $subBrush, (600 - $subSize.Width) / 2, $centerY + $titleSize.Height + 8)

$bmp.Save("c:\Projects\ChurchDisplayApp\splash.png", [System.Drawing.Imaging.ImageFormat]::Png)

$titleFont.Dispose()
$subFont.Dispose()
$titleBrush.Dispose()
$subBrush.Dispose()
if ($iconBmp) { $iconBmp.Dispose() }
if ($icon) { $icon.Dispose() }
$g.Dispose()
$bmp.Dispose()
