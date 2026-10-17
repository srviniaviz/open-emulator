param(
    [string]$BuildDirectory = (Join-Path $PSScriptRoot 'bin'),
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist'),
    [string]$CompilerPath = (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe')
)

$ErrorActionPreference = 'Stop'
$BuildDirectory = [IO.Path]::GetFullPath($BuildDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (-not (Test-Path -LiteralPath $CompilerPath -PathType Leaf)) {
    throw 'Instale o Inno Setup 6 ou informe -CompilerPath com o caminho de ISCC.exe.'
}
$executable = Join-Path $BuildDirectory 'OpenEmulator.exe'
$configuration = "$executable.config"
foreach ($file in @($executable, $configuration)) {
    if (-not (Test-Path -LiteralPath $file -PathType Leaf)) { throw "Build ausente: $file. Execute ./build.ps1 -Test primeiro." }
}
$version = (Get-Item -LiteralPath $executable).VersionInfo.ProductVersion
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "Versão inválida no executável: $version" }
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
& $CompilerPath "/DBuildDir=$BuildDirectory" "/DDistDir=$OutputDirectory" (Join-Path $PSScriptRoot 'installer/OpenEmulator.iss')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar o instalador.' }
$installer = Join-Path $OutputDirectory "OpenEmulator-$version-Setup.exe"
if (-not (Test-Path -LiteralPath $installer)) { throw 'O compilador não gerou o instalador esperado.' }
$portable = Join-Path $OutputDirectory "OpenEmulator-$version-Portable.zip"
Compress-Archive -LiteralPath @($executable, $configuration, (Join-Path $PSScriptRoot 'README.md')) -DestinationPath $portable -Force
$checksums = @($installer, $portable) | ForEach-Object {
    $hash = Get-FileHash -LiteralPath $_ -Algorithm SHA256
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), [IO.Path]::GetFileName($_)
}
$checksums | Set-Content -LiteralPath (Join-Path $OutputDirectory "OpenEmulator-$version-SHA256SUMS.txt") -Encoding ascii
Write-Output "Instalador e pacote portátil $version gerados em $OutputDirectory"
