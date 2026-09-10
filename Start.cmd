@echo off
setlocal
cd /d "%~dp0"
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
 echo Microsoft .NET Framework 4.x is required. Enable it in Windows Features.
 exit /b 1
)
"%CSC%" /nologo /target:winexe /optimize+ /out:"VideoShelf.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Net.Http.dll /reference:System.Web.Extensions.dll /reference:System.Xml.Linq.dll /reference:System.Security.dll "AppDataPaths.cs" "FolderManagement.cs" "VideoShelf.cs" "ScreenshotHarness.cs" "Shelf.Core.cs" "Shelf.Library.cs" "Shelf.Online.cs" "PortraitLookup.cs" "OnlineSearch.cs" "OnlineThumbnailLookup.cs"
if errorlevel 1 (
 echo Build failed.
 exit /b 1
)
if /I "%~1"=="--build-only" exit /b 0
start "" "%~dp0VideoShelf.exe"
