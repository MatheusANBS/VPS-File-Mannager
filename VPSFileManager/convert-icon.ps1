# Script para converter PNG para ICO com múltiplos tamanhos
Add-Type -AssemblyName System.Drawing

$pngPath = "C:\Users\matheuss\Desktop\VPS\VPSFileManager\nuvem.png"
$icoPath = "C:\Users\matheuss\Desktop\VPS\VPSFileManager\nuvem.ico"

# Carregar a imagem PNG
$img = [System.Drawing.Image]::FromFile($pngPath)

# Criar bitmaps em diferentes tamanhos para o ICO
$sizes = @(16, 32, 48, 64, 128, 256)
$bitmaps = @()

foreach ($size in $sizes) {
    $bitmap = New-Object System.Drawing.Bitmap($size, $size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.DrawImage($img, 0, 0, $size, $size)
    $graphics.Dispose()
    $bitmaps += $bitmap
}

# Criar o arquivo ICO
$stream = [System.IO.FileStream]::new($icoPath, [System.IO.FileMode]::Create)
$writer = [System.IO.BinaryWriter]::new($stream)

# ICO header
$writer.Write([UInt16]0)  # Reserved
$writer.Write([UInt16]1)  # Type (1 = ICO)
$writer.Write([UInt16]$bitmaps.Count)  # Number of images

$offset = 6 + (16 * $bitmaps.Count)

# Image directory
foreach ($bitmap in $bitmaps) {
    $writer.Write([byte]$bitmap.Width)  # Width
    $writer.Write([byte]$bitmap.Height)  # Height
    $writer.Write([byte]0)  # Color palette
    $writer.Write([byte]0)  # Reserved
    $writer.Write([UInt16]1)  # Color planes
    $writer.Write([UInt16]32)  # Bits per pixel

    $ms = New-Object System.IO.MemoryStream
    $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $imageData = $ms.ToArray()
    $ms.Dispose()

    $writer.Write([UInt32]$imageData.Length)  # Size of image data
    $writer.Write([UInt32]$offset)  # Offset to image data

    $offset += $imageData.Length
}

# Write image data
foreach ($bitmap in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $bitmap.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $imageData = $ms.ToArray()
    $writer.Write($imageData)
    $ms.Dispose()
}

$writer.Close()
$stream.Close()

# Cleanup
foreach ($bitmap in $bitmaps) {
    $bitmap.Dispose()
}
$img.Dispose()

Write-Host "Arquivo ICO criado com sucesso: $icoPath"
