[CmdletBinding()]
param([string]$OutputDirectory, [string]$EnginePath, [string]$DotNetPath)

$ErrorActionPreference = 'Stop'
$workspaceRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $workspaceRoot 'scripts/Build.Common.ps1')

$projectPath = Join-Path $PSScriptRoot 'PixelLens.Setup.csproj'
$version = Get-PixelLensVersion -ProjectPath $projectPath
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $workspaceRoot "output/releases/v$version/installer" }
if (-not $EnginePath) { $EnginePath = Join-Path $workspaceRoot "output/releases/v$version/engine/PixelLens-Engine-v$version.exe" }
$setupOutput = Get-PixelLensOutputPath -Path $OutputDirectory
$engine = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($EnginePath)
if (-not (Test-Path -LiteralPath $engine -PathType Leaf)) {
    throw "找不到安装引擎：$engine。请运行根目录 build.ps1，或通过 -EnginePath 指定已有引擎。"
}
$dotnet = Resolve-PixelLensTool -Name 'dotnet' -ExplicitPath $DotNetPath
Assert-PixelLensSdk -DotNetPath $dotnet

$publishArgs = @(
    'publish', $projectPath, '-c', 'Release', '-p:PlatformTarget=x64', '-r', 'win-x64',
    '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    "-p:SetupEnginePath=$engine", '-o', $setupOutput
)
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "安装器发布失败，退出码：$LASTEXITCODE" }
if (-not (Test-Path -LiteralPath (Join-Path $setupOutput "PixelLens-Setup-v$version.exe") -PathType Leaf)) {
    throw '发布结束但没有生成安装器。'
}
Write-Host "深色安装器发布完成：$setupOutput"
