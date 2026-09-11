@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
 echo Microsoft .NET Framework 4.x is required. Enable it in Windows Features.
 exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0BuildBrandAssets.ps1"
if errorlevel 1 (
 echo VideoShelf brand asset generation failed.
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
