param(
  [string]$OutputDir = $PSScriptRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function New-RoundedPath([System.Drawing.RectangleF]$Rect, [float]$Radius) {
  $path = New-Object System.Drawing.Drawing2D.GraphicsPath
  $d = [Math]::Max(2.0, $Radius * 2.0)
  $path.AddArc($Rect.X, $Rect.Y, $d, $d, 180, 90)
  $path.AddArc($Rect.Right - $d, $Rect.Y, $d, $d, 270, 90)
  $path.AddArc($Rect.Right - $d, $Rect.Bottom - $d, $d, $d, 0, 90)
  $path.AddArc($Rect.X, $Rect.Bottom - $d, $d, $d, 90, 90)
  $path.CloseFigure()
  return $path
}

function New-BrandBitmap([int]$Size, [bool]$OpaqueBackground = $false) {
  $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  try {
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    if ($OpaqueBackground) {
      $g.Clear([System.Drawing.Color]::FromArgb(13, 25, 36))
    } else {
      $g.Clear([System.Drawing.Color]::Transparent)
    }

    $m = [float]($Size * 0.075)
    $body = New-Object System.Drawing.RectangleF($m, $m, ($Size - 2 * $m), ($Size - 2 * $m))
    $radius = [float]($Size * 0.14)
    $path = New-RoundedPath $body $radius
    try {
      $shadow = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(95, 0, 0, 0))
      try {
        $g.TranslateTransform([float]($Size * 0.018), [float]($Size * 0.028))
        $g.FillPath($shadow, $path)
        $g.ResetTransform()
      } finally { $shadow.Dispose() }

      $bodyBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 7, 18, 27))
      try { $g.FillPath($bodyBrush, $path) } finally { $bodyBrush.Dispose() }

      $borderWidth = [Math]::Max(1.0, $Size * 0.018)
      $border = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 43, 69, 86), [float]$borderWidth)
      try { $g.DrawPath($border, $path) } finally { $border.Dispose() }
    } finally { $path.Dispose() }

    $accentWidth = [Math]::Max(1.2, $Size * 0.034)
    $blue = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 0, 157, 235), [float]$accentWidth)
    $red  = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 58, 66), [float]$accentWidth)
    try {
      $blue.StartCap = $blue.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
      $red.StartCap  = $red.EndCap  = [System.Drawing.Drawing2D.LineCap]::Round
      $g.DrawLine($blue, [float]($Size * 0.20), [float]($Size * 0.145), [float]($Size * 0.79), [float]($Size * 0.145))
      $g.DrawLine($red,  [float]($Size * 0.145), [float]($Size * 0.21), [float]($Size * 0.145), [float]($Size * 0.79))
    } finally {
      $blue.Dispose()
      $red.Dispose()
    }

    $bar1 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 45, 133, 210))
    $bar2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 33, 151, 229))
    $bar3 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 53, 178, 244))
    try {
      $g.FillRectangle($bar1, [float]($Size * 0.27), [float]($Size * 0.55), [float]($Size * 0.085), [float]($Size * 0.18))
      $g.FillRectangle($bar2, [float]($Size * 0.385), [float]($Size * 0.44), [float]($Size * 0.085), [float]($Size * 0.29))
      $g.FillRectangle($bar3, [float]($Size * 0.50), [float]($Size * 0.33), [float]($Size * 0.085), [float]($Size * 0.40))
    } finally {
      $bar1.Dispose(); $bar2.Dispose(); $bar3.Dispose()
    }

    $play = New-Object System.Drawing.Drawing2D.GraphicsPath
    try {
      $play.AddPolygon([System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF([float]($Size * 0.64), [float]($Size * 0.38))),
        (New-Object System.Drawing.PointF([float]($Size * 0.64), [float]($Size * 0.70))),
        (New-Object System.Drawing.PointF([float]($Size * 0.84), [float]($Size * 0.54)))
      ))
      $white = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(245, 244, 248, 250))
      try { $g.FillPath($white, $play) } finally { $white.Dispose() }
    } finally { $play.Dispose() }
  } finally {
    $g.Dispose()
  }
  return $bmp
}

function Write-Icon([string]$Path, [int[]]$Sizes) {
  $frames = New-Object System.Collections.Generic.List[object]
  foreach ($size in $Sizes) {
    $bmp = New-BrandBitmap $size $false
    try {
      $ms = New-Object System.IO.MemoryStream
      try {
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $frames.Add([PSCustomObject]@{ Size = $size; Data = $ms.ToArray() })
      } finally { $ms.Dispose() }
    } finally { $bmp.Dispose() }
  }

  $file = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
  $writer = New-Object System.IO.BinaryWriter($file)
  try {
    $writer.Write([UInt16]0)
    $writer.Write([UInt16]1)
    $writer.Write([UInt16]$frames.Count)
    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
      $dimension = if ($frame.Size -ge 256) { 0 } else { $frame.Size }
      $writer.Write([byte]$dimension)
      $writer.Write([byte]$dimension)
      $writer.Write([byte]0)
      $writer.Write([byte]0)
      $writer.Write([UInt16]1)
      $writer.Write([UInt16]32)
      $writer.Write([UInt32]$frame.Data.Length)
      $writer.Write([UInt32]$offset)
      $offset += $frame.Data.Length
    }
    foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Data) }
  } finally {
    $writer.Dispose()
    $file.Dispose()
  }
}

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
$runtimeIcon = Join-Path $OutputDir 'VideoShelf.ico'
$compilerIcon = Join-Path $OutputDir 'VideoShelfApp.ico'
$brandPng = Join-Path $OutputDir 'VideoShelf.png'
$installerLogo = Join-Path $OutputDir 'VideoShelfInstallerLogo.bmp'

Write-Icon $runtimeIcon @(16, 24, 32, 48, 64, 96, 128, 256)
Write-Icon $compilerIcon @(16, 24, 32, 48, 64)

$master = New-BrandBitmap 512 $false
try { $master.Save($brandPng, [System.Drawing.Imaging.ImageFormat]::Png) } finally { $master.Dispose() }

$installer = New-BrandBitmap 256 $true
try { $installer.Save($installerLogo, [System.Drawing.Imaging.ImageFormat]::Bmp) } finally { $installer.Dispose() }

if ((Get-Item $runtimeIcon).Length -lt 12000) { throw 'High-resolution VideoShelf icon generation failed.' }
if ((Get-Item $compilerIcon).Length -lt 3000) { throw 'Compiler VideoShelf icon generation failed.' }
if ((Get-Item $brandPng).Length -lt 5000) { throw 'Transparent VideoShelf PNG generation failed.' }
$verifyPng = [System.Drawing.Bitmap]::FromFile($brandPng)
try {
  if ($verifyPng.Width -ne 512 -or $verifyPng.Height -ne 512) { throw 'VideoShelf PNG has the wrong dimensions.' }
  if ($verifyPng.GetPixel(0, 0).A -ne 0) { throw 'VideoShelf PNG background is not transparent.' }
} finally { $verifyPng.Dispose() }
if ((Get-Item $installerLogo).Length -lt 10000) { throw 'Installer branding generation failed.' }
