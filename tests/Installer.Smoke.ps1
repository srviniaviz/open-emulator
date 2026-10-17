param(
    [Parameter(Mandatory)]
    [string]$InstallerPath
)

$ErrorActionPreference = 'Stop'
if ($env:GITHUB_ACTIONS -ne 'true' -or $env:RUNNER_ENVIRONMENT -ne 'github-hosted') {
    throw 'Este teste instala e desinstala o app. Execute somente no runner hospedado e descartável do GitHub Actions.'
}
$InstallerPath = (Resolve-Path -LiteralPath $InstallerPath).Path
$installDirectory = Join-Path $env:RUNNER_TEMP ('Open Emulator Test ' + [Guid]::NewGuid().ToString('N'))
$registryKey = 'HKCU:/Software/Microsoft/Windows/CurrentVersion/Uninstall/{BFF83B93-9117-47F8-AAD2-8A440F655BD4}_is1'
if (Test-Path $registryKey) { throw 'Já existe uma instalação do app neste runner; o teste não pode sobrescrevê-la.' }
$uninstaller = Join-Path $installDirectory 'unins000.exe'
try {
    $process = Start-Process -FilePath $InstallerPath -WindowStyle Hidden -Wait -PassThru -ArgumentList ('/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /TASKS="" /DIR="' + $installDirectory + '"')
    if ($process.ExitCode -ne 0) { throw "Instalação falhou com código $($process.ExitCode)." }
    foreach ($name in @('OpenEmulator.exe', 'OpenEmulator.exe.config', 'README.md', 'unins000.exe')) {
        if (-not (Test-Path -LiteralPath (Join-Path $installDirectory $name) -PathType Leaf)) { throw "Arquivo instalado ausente: $name" }
    }
    $registration = Get-ItemProperty -LiteralPath $registryKey
    if ($registration.DisplayName -notlike 'Open Emulator*') { throw 'Registro de desinstalação inválido.' }
    $shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) 'Open Emulator.lnk'
    if (-not (Test-Path -LiteralPath $shortcut)) { throw 'Atalho do menu Iniciar ausente.' }
    $shell = New-Object -ComObject WScript.Shell
    if ($shell.CreateShortcut($shortcut).TargetPath -ne (Join-Path $installDirectory 'OpenEmulator.exe')) { throw 'Atalho aponta para o local errado.' }
    Write-Output 'Instalação por usuário, arquivos, registro e atalho verificados.'
} finally {
    if (Test-Path -LiteralPath $uninstaller) {
        $process = Start-Process -FilePath $uninstaller -WindowStyle Hidden -Wait -PassThru -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
        if ($process.ExitCode -ne 0) { throw "Desinstalação falhou com código $($process.ExitCode)." }
    }
}
if ((Test-Path -LiteralPath (Join-Path $installDirectory 'OpenEmulator.exe')) -or (Test-Path -LiteralPath $registryKey) -or (Test-Path -LiteralPath $shortcut)) {
    throw 'A desinstalação deixou o executável, o registro ou o atalho.'
}
Write-Output 'Desinstalação verificada.'
