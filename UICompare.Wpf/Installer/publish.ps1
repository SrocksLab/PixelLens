[CmdletBinding()]
param([string]$OutputDirectory, [string]$DotNetPath)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$workspaceRoot = Split-Path -Parent $projectRoot
. (Join-Path $workspaceRoot 'scripts/Build.Common.ps1')

$projectPath = Join-Path $projectRoot 'UICompare.Wpf.csproj'
$version = Get-PixelLensVersion -ProjectPath $projectPath
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $workspaceRoot "output/releases/v$version/app" }
$targetPath = Get-PixelLensOutputPath -Path $OutputDirectory
$dotnet = Resolve-PixelLensTool -Name 'dotnet' -ExplicitPath $DotNetPath
Assert-PixelLensSdk -DotNetPath $dotnet

# Keep restore enabled for clean checkouts.
$publishArgs = @(
    'publish', $projectPath, '-c', 'Release', '-p:Platform=x64', '-r', 'win-x64',
    '--self-contained', 'true', '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true',
    '-o', $targetPath
)
& $dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { throw "主程序发布失败，退出码：$LASTEXITCODE" }
if (-not (Test-Path -LiteralPath (Join-Path $targetPath 'UICompare.exe') -PathType Leaf)) {
    throw '发布结束但没有生成 UICompare.exe。'
}
Write-Host "主程序发布完成：$targetPath"