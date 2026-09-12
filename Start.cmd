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
if not exist "VideoShelf.png" (
 echo VideoShelf transparent branding asset is missing.
 exit /b 1
)
if not exist "VideoShelfApp.ico" (
 echo VideoShelf compiler icon asset is missing.
 exit /b 1
)

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"VideoShelfApp.ico" /out:"VideoShelf.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll /reference:System.Xml.Linq.dll /reference:System.Security.dll "src\VideoShelf\Infrastructure\AppDataPaths.cs" "src\VideoShelf\Branding\AppBrand.cs" "src\VideoShelf\UI\Theme\XdolfTheme.cs" "src\VideoShelf\UI\Controls\MockupControls.cs" "src\VideoShelf\UI\Controls\MockupActionButton.cs" "src\VideoShelf\UI\Controls\MockupFilter.cs" "src\VideoShelf\Infrastructure\FolderManagement.cs" "src\VideoShelf\Media\TransferBridge.cs" "src\VideoShelf\Application\VideoShelf.cs" "src\VideoShelf\Diagnostics\ScreenshotHarness.cs" "src\VideoShelf\UI\Shelf\Shelf.Core.cs" "src\VideoShelf\UI\Shelf\Shelf.Branding.cs" "src\VideoShelf\UI\Shelf\Shelf.HomeDashboard.cs" "src\VideoShelf\UI\Shelf\Shelf.Layout.cs" "src\VideoShelf\UI\Shelf\Shelf.Visuals.cs" "src\VideoShelf\UI\Shelf\Shelf.Polish.cs" "src\VideoShelf\UI\Shelf\Shelf.Library.cs" "src\VideoShelf\UI\Shelf\Shelf.Online.cs" "src\VideoShelf\Search\PortraitLookup.cs" "src\VideoShelf\Search\OnlineSearch.cs" "src\VideoShelf\Search\BuiltInOnlineSearch.cs" "src\VideoShelf\Search\SearchRelevance.cs" "src\VideoShelf\Search\OnlineThumbnailLookup.cs"
if errorlevel 1 (
 echo Build failed.
 exit /b 1
)

if /I "%~1"=="--build-only" exit /b 0

set "TRANSFER_EXE=%~dp0TransferHostRuntime\VideoShelf.TransferHost.exe"
set "BUILD_TRANSFER=0"
if not exist "%TRANSFER_EXE%" set "BUILD_TRANSFER=1"
if "%BUILD_TRANSFER%"=="0" (
 powershell -NoProfile -Command "$exe=(Get-Item -LiteralPath $env:TRANSFER_EXE).LastWriteTimeUtc; $stale=Get-ChildItem -LiteralPath '%~dp0TransferHost' -Recurse -File | Where-Object { $_.Extension -in '.cs','.csproj' -and $_.LastWriteTimeUtc -gt $exe }; if($stale){exit 1}else{exit 0}"
 if errorlevel 1 set "BUILD_TRANSFER=1"
)

if "%BUILD_TRANSFER%"=="1" (
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
