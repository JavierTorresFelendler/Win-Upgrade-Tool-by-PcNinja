#Requires -Version 5.1

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName PresentationFramework
Add-Type -AssemblyName System.Windows.Forms

function Test-Administrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Show-Message {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Text,

        [string]$Title = 'PcNinja Windows 11 Upgrade Assistant',

        [System.Windows.MessageBoxButton]$Buttons = [System.Windows.MessageBoxButton]::OK,

        [System.Windows.MessageBoxImage]$Icon = [System.Windows.MessageBoxImage]::Information
    )

    return [System.Windows.MessageBox]::Show($Text, $Title, $Buttons, $Icon)
}

function Select-WindowsIso {
    $dialog = New-Object System.Windows.Forms.OpenFileDialog
    $dialog.Title = 'Select the downloaded Windows 11 ISO'
    $dialog.Filter = 'Windows ISO images (*.iso)|*.iso'
    $dialog.InitialDirectory = Join-Path $env:USERPROFILE 'Downloads'
    $dialog.Multiselect = $false
    $dialog.CheckFileExists = $true

    if ($dialog.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) {
        throw 'No ISO file was selected.'
    }

    return $dialog.FileName
}

function Wait-ForIsoDrive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ImagePath
    )

    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        $volume = Get-DiskImage -ImagePath $ImagePath |
            Get-Volume |
            Where-Object { $_.DriveLetter } |
            Select-Object -First 1

        if ($volume) {
            return "$($volume.DriveLetter):"
        }

        Start-Sleep -Seconds 1
    }

    throw 'The ISO was mounted, but Windows did not assign a drive letter.'
}

if (-not (Test-Administrator)) {
    try {
        $elevationArguments = @(
            '-NoProfile'
            '-ExecutionPolicy'
            'Bypass'
            '-File'
            "`"$PSCommandPath`""
        )

        Start-Process -FilePath 'powershell.exe' -Verb RunAs -ArgumentList $elevationArguments
        exit 0
    }
    catch {
        Show-Message `
            -Text 'Administrator permission is required to mount the ISO and start Windows Setup.' `
            -Icon Error | Out-Null

        exit 1
    }
}

$downloadPortal = 'https://11oem.pcninja.pro'
$workFolder = Join-Path $env:SystemDrive 'PcNinja\Win11UpgradeAssistant'
$logPath = Join-Path $workFolder 'Upgrade-Assistant.log'
$runnerPath = Join-Path $PSScriptRoot 'Run-Windows-Setup.cmd'

New-Item -ItemType Directory -Path $workFolder -Force | Out-Null
Start-Transcript -Path $logPath -Append | Out-Null

try {
    $os = Get-CimInstance Win32_OperatingSystem

    if ($os.ProductType -ne 1) {
        throw 'This POC supports Windows 10 and Windows 11 client systems only. Windows Server is not supported.'
    }

    $isX64 = $env:PROCESSOR_ARCHITECTURE -eq 'AMD64' -or
        $env:PROCESSOR_ARCHITEW6432 -eq 'AMD64'

    if (-not $isX64) {
        throw 'This POC currently supports x64 computers only.'
    }

    if (-not (Test-Path -LiteralPath $runnerPath)) {
        throw "The CMD upgrade helper is missing: $runnerPath"
    }

    $isoChoice = Show-Message `
        -Text "Do you already have a Windows 11 x64 ISO?`n`nYES - Select an existing ISO file.`n`nNO - Open the PcNinja download page, then select the downloaded ISO.`n`nCANCEL - Exit without making changes." `
        -Buttons YesNoCancel `
        -Icon Question

    if ($isoChoice -eq [System.Windows.MessageBoxResult]::Cancel) {
        Write-Host 'Upgrade cancelled by the user.'
        exit 0
    }

    if ($isoChoice -eq [System.Windows.MessageBoxResult]::No) {
        Start-Process $downloadPortal

        Show-Message `
            -Text "The PcNinja download page is now open.`n`nDownload the Windows 11 ISO completely. When the download finishes, return here and click OK to select the ISO file." `
            -Icon Information | Out-Null
    }

    $isoPath = Select-WindowsIso

    if (-not (Test-Path -LiteralPath $isoPath)) {
        throw 'The selected Windows 11 ISO file was not found.'
    }

    Write-Host "Selected ISO: $isoPath"
    Write-Host 'Mounting the ISO...'

    $diskImage = Get-DiskImage -ImagePath $isoPath -ErrorAction SilentlyContinue
    if (-not $diskImage -or -not $diskImage.Attached) {
        Mount-DiskImage -ImagePath $isoPath | Out-Null
    }

    $isoDrive = Wait-ForIsoDrive -ImagePath $isoPath
    $setupPath = Join-Path $isoDrive 'setup.exe'

    if (-not (Test-Path -LiteralPath $setupPath)) {
        throw "setup.exe was not found in the mounted ISO at $isoDrive"
    }

    $signature = Get-AuthenticodeSignature -FilePath $setupPath
    if ($signature.Status -ne 'Valid' -or
        $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
        throw 'setup.exe does not have a valid Microsoft digital signature.'
    }

    if (Test-Path -LiteralPath (Join-Path $isoDrive 'Autounattend.xml')) {
        throw 'The selected ISO contains Autounattend.xml and is not suitable for this automatic upgrade.'
    }

    $readyChoice = Show-Message `
        -Text "The ISO is mounted at $isoDrive and setup.exe has a valid Microsoft signature.`n`nSave your work and back up important files before continuing.`n`nWindows Setup will restart the computer automatically when required.`n`nStart the upgrade?" `
        -Buttons YesNo `
        -Icon Warning

    if ($readyChoice -ne [System.Windows.MessageBoxResult]::Yes) {
        Write-Host 'Upgrade cancelled by the user before Windows Setup was started.'
        exit 0
    }

    Write-Host 'Starting the known-good CMD upgrade command...'

    $cmdArguments = "/d /c `"`"$runnerPath`" `"$setupPath`"`""
    $cmdProcess = Start-Process `
        -FilePath $env:ComSpec `
        -ArgumentList $cmdArguments `
        -Wait `
        -PassThru

    if ($cmdProcess.ExitCode -ne 0) {
        throw "Windows Setup stopped with exit code $($cmdProcess.ExitCode)."
    }

    Show-Message `
        -Text 'Windows Setup completed its current stage. Windows Setup controls the restart process.' `
        -Icon Information | Out-Null

    exit 0
}
catch {
    Write-Error $_.Exception.Message

    Show-Message `
        -Text "$($_.Exception.Message)`n`nThe upgrade was not started or Windows Setup stopped. No forced restart was issued.`n`nLog: $logPath" `
        -Icon Error | Out-Null

    exit 1
}
finally {
    try {
        Stop-Transcript | Out-Null
    }
    catch {
        # No transcript was active.
    }
}
