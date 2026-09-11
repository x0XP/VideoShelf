@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
 echo Microsoft .NET Framework 4.x is required. Enable it in Windows Features.
 exit /b 1
)

if not exist "VideoShelf.ico" (
 echo VideoShelf icon asset is missing.
 exit /b 1
)

if not exist "VideoShelfApp.ico" (
 echo VideoShelf compiler icon asset is missing.
 exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"VideoShelfApp.ico" /out:"VideoShelf.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll /reference:System.Xml.Linq.dll /reference:System.Security.dll "AppDataPaths.cs" "AppBrand.cs" "XdolfTheme.cs" "MockupControls.cs" "MockupActionButton.cs" "MockupFilter.cs" "FolderManagement.cs" "TransferBridge.cs" "VideoShelf.cs" "ScreenshotHarness.cs" "Shelf.Core.cs" "Shelf.Branding.cs" "Shelf.HomeDashboard.cs" "Shelf.Layout.cs" "Shelf.Visuals.cs" "Shelf.Polish.cs" "Shelf.Library.cs" "Shelf.Online.cs" "PortraitLookup.cs" "OnlineSearch.cs" "BuiltInOnlineSearch.cs" "SearchRelevance.cs" "OnlineThumbnailLookup.cs"
if errorlevel 1 (
 echo Build failed.
 exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "Add-Type -AssemblyName System.Drawing; $icon=[System.Drawing.Icon]::new((Resolve-Path 'VideoShelf.ico').Path,256,256); $src=$icon.ToBitmap(); $bmp=[System.Drawing.Bitmap]::new(256,256); $g=[System.Drawing.Graphics]::FromImage($bmp); $g.Clear([System.Drawing.Color]::FromArgb(13,25,36)); $g.CompositingQuality=[System.Drawing.Drawing2D.CompositingQuality]::HighQuality; $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic; $g.PixelOffsetMode=[System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality; $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::HighQuality; $g.DrawImage($src,0,0,256,256); $bmp.Save((Join-Path (Get-Location) 'VideoShelfInstallerLogo.bmp'),[System.Drawing.Imaging.ImageFormat]::Bmp); $g.Dispose(); $bmp.Dispose(); $src.Dispose(); $icon.Dispose()"
if errorlevel 1 (
 echo VideoShelf installer branding generation failed.
 exit /b 1
)

if /I "%~1"=="--build-only" exit /b 0
if not exist "TransferHostRuntime\VideoShelf.TransferHost.exe" (
 where dotnet >nul 2>nul
 if not errorlevel 1 (
  echo Building VideoShelf torrent transfer runtime...
  dotnet publish "TransferHost\VideoShelf.TransferHost.csproj" -c Release -r win-x64 --self-contained true -o "TransferHostRuntime"
  if errorlevel 1 echo Warning: transfer runtime build failed. Library browsing will still work.
 ) else (
  echo Note: .NET SDK not found, so the transfer runtime was not built. Use the packaged Windows release for integrated downloads/streaming.
 )
)
start "" "%~dp0VideoShelf.exe"
