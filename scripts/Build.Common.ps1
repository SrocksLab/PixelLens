Set-StrictMode -Version Latest

function Get-PixelLensVersion {
    param([string]$ProjectPath)
    [xml]$project = Get-Content -LiteralPath $ProjectPath -Raw
    $version = [string]$project.Project.PropertyGroup.Version
    if ([string]::IsNullOrWhiteSpace($version)) { throw "项目中没有 Version：$ProjectPath" }
    return $version
}

function Resolve-PixelLensTool {
    param([string]$Name, [string]$ExplicitPath, [string[]]$Candidates = @())

    if ($ExplicitPath) {
        $path = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ExplicitPath)
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "工具不存在：$path" }
        return $path
    }
    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($command) { return $command.Source }
    foreach ($candidate in $Candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { return $candidate }
    }
    throw "未找到 $Name。请安装并加入 PATH，或通过脚本参数指定完整路径。"
}

function Assert-PixelLensSdk {
    param([string]$DotNetPath)
    $version = & $DotNetPath --version
    if ($LASTEXITCODE -ne 0 -or "$version" -notmatch '^(\d+)\.' -or [int]$Matches[1] -lt 10) {
        throw '需要 .NET 10 SDK 或兼容的更新 SDK；仅安装 .NET Runtime 不足以构建。'
    }
}

function Get-PixelLensOutputPath {
    param([string]$Path)
    $absolutePath = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($Path)
    if (Test-Path -LiteralPath $absolutePath) {
        throw "输出路径已存在，禁止覆盖已保留版本；请指定新目录：$absolutePath"
    }
    return $absolutePath
}