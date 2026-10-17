param(
    [switch]$Test,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'bin')
)

$ErrorActionPreference = 'Stop'
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$references = @('/r:System.Windows.Forms.dll', '/r:System.Drawing.dll', '/r:System.Xml.Linq.dll', '/r:System.IO.Compression.dll', '/r:System.IO.Compression.FileSystem.dll')
$sources = @(Get-ChildItem (Join-Path $PSScriptRoot 'src') -Filter *.cs | ForEach-Object FullName)
$icon = Join-Path $PSScriptRoot 'assets/OpenEmulator.ico'
$iconArguments = @("/win32icon:$icon", "/resource:$icon,OpenEmulator.Icon.ico")
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /utf8output "/out:$OutputDirectory/OpenEmulator.exe" @iconArguments @references @sources
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o aplicativo.' }
Copy-Item (Join-Path $PSScriptRoot 'src/App.config') (Join-Path $OutputDirectory 'OpenEmulator.exe.config') -Force
if ($Test) {
    & $compiler /nologo /target:exe /main:OpenEmulator.Tests /utf8output "/out:$OutputDirectory/Tests.exe" @iconArguments @references @sources (Join-Path $PSScriptRoot 'tests/Tests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar os testes.' }
    Copy-Item (Join-Path $PSScriptRoot 'src/App.config') (Join-Path $OutputDirectory 'Tests.exe.config') -Force
    & (Join-Path $OutputDirectory 'Tests.exe')
    if ($LASTEXITCODE -ne 0) { throw 'Testes falharam.' }
}
