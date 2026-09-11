param(
    [Parameter(Mandatory = $true)]
    [string]$Installer,

    [Parameter(Mandatory = $true)]
    [string]$OutputDir
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

public static class InstallerWindowCapture
{
    [StructLayout(LayoutKind.Sequential)]
    struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);

    [DllImport("user32.dll")]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    public static IntPtr FindWindow(string titleFragment)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((hWnd, lParam) =>
        {
            if (!IsWindowVisible(hWnd))
                return true;

            var title = new StringBuilder(512);
            GetWindowText(hWnd, title, title.Capacity);
            if (title.ToString().IndexOf(titleFragment, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                found = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static void Save(IntPtr hWnd, string path)
    {
        RECT rect;
        if (!GetWindowRect(hWnd, out rect))
            throw new InvalidOperationException("Could not read installer window bounds.");

        int width = Math.Max(1, rect.Right - rect.Left);
        int height = Math.Max(1, rect.Bottom - rect.Top);
        using (var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb))
        using (var graphics = Graphics.FromImage(bitmap))
        {
            IntPtr hdc = graphics.GetHdc();
            bool ok;
            try
            {
                // PW_RENDERFULLCONTENT renders the complete GDI window even if it is partly obscured.
                ok = PrintWindow(hWnd, hdc, 2);
            }
            finally
            {
                graphics.ReleaseHdc(hdc);
            }
            if (!ok)
                throw new InvalidOperationException("PrintWindow failed for the installer window.");
            bitmap.Save(path, ImageFormat.Png);
        }
    }
}
'@

function Wait-MainWindow {
    param([System.Diagnostics.Process]$Bootstrap, [int]$TimeoutSeconds = 30)

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        if (-not $Bootstrap.HasExited) {
            $Bootstrap.Refresh()
            if ($Bootstrap.MainWindowHandle -ne [IntPtr]::Zero) {
                return $Bootstrap.MainWindowHandle
            }
        }

        # Inno Setup launches a temporary child executable which owns the real wizard
        # window. Find it by title rather than assuming the bootstrap process owns it.
        $handle = [InstallerWindowCapture]::FindWindow('VideoShelf Setup')
        if ($handle -ne [IntPtr]::Zero) {
            return $handle
        }

        Start-Sleep -Milliseconds 150
    } while ([DateTime]::UtcNow -lt $deadline)

    throw 'Timed out waiting for the VideoShelf Setup window.'
}

function Get-CurrentWindow {
    param([System.Diagnostics.Process]$Bootstrap)

    if (-not $Bootstrap.HasExited) {
        $Bootstrap.Refresh()
        if ($Bootstrap.MainWindowHandle -ne [IntPtr]::Zero) {
            return $Bootstrap.MainWindowHandle
        }
    }

    return [InstallerWindowCapture]::FindWindow('VideoShelf Setup')
}

function Get-Root {
    param([IntPtr]$Handle)
    return [System.Windows.Automation.AutomationElement]::FromHandle($Handle)
}

function Get-ControlNames {
    param([System.Windows.Automation.AutomationElement]$Root)

    $names = New-Object System.Collections.Generic.List[string]
    $items = $Root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($item in $items) {
        try {
            $name = $item.Current.Name
            if (-not [string]::IsNullOrWhiteSpace($name)) {
                $names.Add($name)
            }
        } catch {
            # Ignore controls which disappeared while the page was transitioning.
        }
    }
    return ($names -join "`n")
}

function Find-Button {
    param(
        [System.Windows.Automation.AutomationElement]$Root,
        [string]$Pattern
    )

    $items = $Root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($item in $items) {
        try {
            if ($item.Current.ControlType -eq [System.Windows.Automation.ControlType]::Button -and
                $item.Current.IsEnabled -and
                $item.Current.Name -match $Pattern) {
                return $item
            }
        } catch {
        }
    }
    return $null
}

function Invoke-Button {
    param([System.Windows.Automation.AutomationElement]$Button)

    $pattern = $Button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern)
    ([System.Windows.Automation.InvokePattern]$pattern).Invoke()
}

function Disable-LaunchCheckbox {
    param([System.Windows.Automation.AutomationElement]$Root)

    $items = $Root.FindAll(
        [System.Windows.Automation.TreeScope]::Descendants,
        [System.Windows.Automation.Condition]::TrueCondition)
    foreach ($item in $items) {
        try {
            if ($item.Current.ControlType -eq [System.Windows.Automation.ControlType]::CheckBox -and
                $item.Current.Name -match '(?i)Launch VideoShelf') {
                $pattern = $item.GetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern)
                $toggle = [System.Windows.Automation.TogglePattern]$pattern
                if ($toggle.Current.ToggleState -eq [System.Windows.Automation.ToggleState]::On) {
                    $toggle.Toggle()
                }
                return
            }
        } catch {
        }
    }
}

function Save-Shot {
    param(
        [IntPtr]$Handle,
        [string]$Name
    )

    $path = Join-Path $OutputDir ("VideoShelf-installer-{0}.png" -f $Name)
    [InstallerWindowCapture]::Save($Handle, $path)
    if (!(Test-Path $path) -or (Get-Item $path).Length -lt 10000) {
        throw "Installer screenshot was not created correctly: $path"
    }
    Write-Host "Captured $path"
}

function Classify-Page {
    param([string]$Text)

    if ($Text -match '(?i)Select Additional Tasks') { return 'tasks' }
    if ($Text -match '(?i)Ready to Install') { return 'ready' }
    if ($Text -match '(?i)Installing') { return 'installing' }
    if ($Text -match '(?i)VideoShelf installed|Completing the VideoShelf Setup Wizard') { return 'finish' }
    if ($Text -match '(?i)Select Destination Location') { return 'destination' }
    if ($Text -match '(?i)Welcome|Install VideoShelf') { return 'welcome' }
    return 'page'
}

$visualInstallDir = Join-Path $env:RUNNER_TEMP 'VideoShelf-installer-visual-test'
if (Test-Path $visualInstallDir) {
    Remove-Item -Recurse -Force $visualInstallDir
}

$args = @(
    '/SP-',
    '/NORESTART',
    '/SUPPRESSMSGBOXES',
    ('/DIR="' + $visualInstallDir + '"')
)

$bootstrap = Start-Process -FilePath $Installer -ArgumentList $args -PassThru
$handle = Wait-MainWindow -Bootstrap $bootstrap
$captured = @{}
$finished = $false

try {
    for ($step = 0; $step -lt 14 -and -not $finished; $step++) {
        Start-Sleep -Milliseconds 500

        $handle = Get-CurrentWindow -Bootstrap $bootstrap
        if ($handle -eq [IntPtr]::Zero) {
            if ($bootstrap.HasExited) { break }
            $handle = Wait-MainWindow -Bootstrap $bootstrap -TimeoutSeconds 10
        }

        $root = Get-Root -Handle $handle
        $text = Get-ControlNames -Root $root
        $page = Classify-Page -Text $text
        Write-Host "Installer page $step classified as '$page'."
        Write-Host ($text -replace "`n", ' | ')

        if (-not $captured.ContainsKey($page)) {
            Save-Shot -Handle $handle -Name $page
            $captured[$page] = $true
        }

        if ($page -eq 'installing') {
            $deadline = [DateTime]::UtcNow.AddSeconds(120)
            do {
                Start-Sleep -Milliseconds 500
                $handle = Get-CurrentWindow -Bootstrap $bootstrap
                if ($handle -eq [IntPtr]::Zero) {
                    if ($bootstrap.HasExited) { break }
                    continue
                }
                $root = Get-Root -Handle $handle
                $text = Get-ControlNames -Root $root
                if ((Classify-Page -Text $text) -eq 'finish') { break }
            } while ([DateTime]::UtcNow -lt $deadline)
            continue
        }

        if ($page -eq 'finish') {
            Disable-LaunchCheckbox -Root $root
            $finish = Find-Button -Root $root -Pattern '(?i)^Finish$'
            if ($null -ne $finish) {
                Invoke-Button -Button $finish
            }
            $finished = $true
            break
        }

        $primary = Find-Button -Root $root -Pattern '(?i)^(Next\s*>?|Install)$'
        if ($null -eq $primary) {
            Start-Sleep -Milliseconds 500
            continue
        }
        Invoke-Button -Button $primary
    }

    if (-not $captured.ContainsKey('tasks')) { throw 'Select Additional Tasks page was not captured.' }
    if (-not $captured.ContainsKey('ready')) { throw 'Ready to Install page was not captured.' }
    if (-not $captured.ContainsKey('installing')) { throw 'Installing page was not captured.' }
    if (-not $captured.ContainsKey('finish')) { throw 'Finish page was not captured.' }
}
finally {
    if (-not $bootstrap.HasExited) {
        try { $bootstrap.Kill() } catch { }
        try { $bootstrap.WaitForExit(5000) | Out-Null } catch { }
    }

    Get-Process -ErrorAction SilentlyContinue | Where-Object {
        $_.ProcessName -like 'VideoShelf-Setup-v1.7*'
    } | ForEach-Object {
        try { $_.Kill() } catch { }
    }

    $uninstaller = Join-Path $visualInstallDir 'unins000.exe'
    if (Test-Path $uninstaller) {
        $uninstall = Start-Process -FilePath $uninstaller -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -Wait -PassThru
        if ($uninstall.ExitCode -ne 0) {
            throw "Visual-test uninstall failed with exit code $($uninstall.ExitCode)."
        }
    }
}
