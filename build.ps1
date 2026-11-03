[CmdletBinding()]
param([string]$OutputDirectory, [string]$DotNetPath, [string]$InnoSetupPath)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'scripts/Build.Common.ps1')

$appProject = Join-Path $PSScriptRoot 'UICompare.Wpf/UICompare.Wpf.csproj'
$setupProject = Join-Path $PSScriptRoot 'PixelLens.Setup/PixelLens.Setup.csproj'
$version = Get-PixelLensVersion -ProjectPath $appProject
if ((Get-PixelLensVersion -ProjectPath $setupProject) -ne $version) {
    throw '主程序和安装器的 Version 不一致，请先统一版本号。'
}
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $PSScriptRoot "output/releases/v$version" }
$release = Get-PixelLensOutputPath -Path $OutputDirectory
$dotnet = Resolve-PixelLensTool -Name 'dotnet' -ExplicitPath $DotNetPath
Assert-PixelLensSdk -DotNetPath $dotnet
$innoCandidates = @()
foreach ($base in @(${env:ProgramFiles(x86)}, $env:ProgramFiles)) {
    if ($base) { $innoCandidates += Join-Path $base 'Inno Setup 6/ISCC.exe' }
}
$iscc = Resolve-PixelLensTool -Name 'ISCC.exe' -ExplicitPath $InnoSetupPath -Candidates $innoCandidates

$appOutput = Join-Path $release 'app'
$engineOutput = Join-Path $release 'engine'
$installerOutput = Join-Path $release 'installer'
$engine = Join-Path $engineOutput "PixelLens-Engine-v$version.exe"

Write-Host '[1/3] 发布 Windows x64 主程序'
& (Join-Path $PSScriptRoot 'UICompare.Wpf/Installer/publish.ps1') -OutputDirectory $appOutput -DotNetPath $dotnet

Write-Host '[2/3] 编译 Inno Setup 安装引擎'
$innoArguments = @(
    '/Qp', "/DAppVersion=$version", "/DAppPayload=$(Join-Path $appOutput 'UICompare.exe')",
    "/O$engineOutput", "/FPixelLens-Engine-v$version",
    (Join-Path $PSScriptRoot 'UICompare.Wpf/Installer/UICompare.iss')
)
& $iscc @innoArguments
if ($LASTEXITCODE -ne 0) { throw "安装引擎编译失败，退出码：$LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $engine -PathType Leaf)) { throw "未生成安装引擎：$engine" }

Write-Host '[3/3] 发布内嵌安装引擎的深色安装器'
& (Join-Path $PSScriptRoot 'PixelLens.Setup/publish.ps1') -OutputDirectory $installerOutput -EnginePath $engine -DotNetPath $dotnet

$checksums = foreach ($relativePath in @(
    'app/UICompare.exe', "engine/PixelLens-Engine-v$version.exe", "installer/PixelLens-Setup-v$version.exe"
)) {
    $hash = Get-FileHash -LiteralPath (Join-Path $release $relativePath) -Algorithm SHA256
    '{0}  {1}' -f $hash.Hash.ToLowerInvariant(), $relativePath
}
$checksums | Set-Content -LiteralPath (Join-Path $release 'SHA256SUMS.txt') -Encoding ASCII
Write-Host "打包完成：$release"