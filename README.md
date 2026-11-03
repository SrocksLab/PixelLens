# PixelLens · 像素之眼

用于 Windows 桌面软件视觉走查的设计稿与截图对比工具。导入设计稿，截取软件画面，对齐截图，再查看像素差异。

当前正式版为 **V1.0.0**。主程序使用 **C#、.NET 10、WPF、OpenCvSharp**，面向 **Windows x64**。完整安装包由 WPF 安装界面和内嵌的 Inno Setup 安装引擎组成。

## 功能与使用

1. 导入设计稿，再导入截图或截取桌面画面。截屏支持按设计稿尺寸选取、自由框选、移动和调整选区。
2. 选择配对的设计稿和截图。拖动、缩放截图并调整透明度，让它与固定的设计稿对齐。可拖动标记改变缩放中心；每组分别保留对齐状态。
3. 在设计稿、截图、叠加和差异视图之间切换。开始对比后，设计稿范围内的像素差异会以红色半透明区域标记。

删除截图会保留设计稿；删除设计稿会移除整组。混合 DPI、负坐标显示器布局及不同物理屏幕组合尚未全面实测。版本变化见 [版本记录](版本记录.md)。

## 获取程序

正式提供下载时，从仓库的 **Releases** 页面获取 `PixelLens-Setup-v1.0.0.exe`（安装包）或 `UICompare.exe`（免安装版）。这两个 Windows x64 自包含程序都不要求使用者单独安装 .NET Runtime。源码仓库不包含这些构建产物。

## 从源码运行

需要 Windows x64、[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) 和 PowerShell（Windows PowerShell 5.1 或 PowerShell 7）。首次还原依赖需要访问 NuGet。仅安装 .NET Runtime 不足以构建。

在仓库根目录执行：

```powershell
dotnet --version
dotnet run --project .\UICompare.Wpf\UICompare.Wpf.csproj -p:Platform=x64
```

也可在支持 .NET 10 的 Visual Studio 中打开 `UICompare.Wpf/UICompare.Wpf.csproj`。当前源码不需要解决方案文件。

## 构建和打包

完整安装包还需要 [Inno Setup 6](https://jrsoftware.org/isinfo.php)（含预处理器）。脚本会在 PATH 和 Inno Setup 6 的常用安装目录查找 `ISCC.exe`，也可通过参数指定工具路径。

在仓库根目录执行：

```powershell
.\build.ps1
```

脚本依次发布主程序、编译 Inno 安装引擎、发布内嵌引擎的深色安装器。发布时会还原依赖，无需已有的 `bin/`、`obj/`、`output/` 或 `tmp/`。

默认输出（版本号从项目文件读取）：

```text
output/releases/v1.0.0/
├── app/UICompare.exe
├── engine/PixelLens-Engine-v1.0.0.exe
├── installer/PixelLens-Setup-v1.0.0.exe
└── SHA256SUMS.txt
```

**输出目录已存在时脚本会停止。** 重复构建或失败后重试时，指定新的输出目录即可保留旧产物：

```powershell
.\build.ps1 -OutputDirectory .\output\releases\v1.0.0-rebuild-1
```

若工具安装在自定义位置，可显式指定：

```powershell
.\build.ps1 -DotNetPath 'C:\Tools\dotnet\dotnet.exe' -InnoSetupPath 'C:\Tools\Inno Setup 6\ISCC.exe' -OutputDirectory .\output\releases\v1.0.0-rebuild-2
```

相对路径按调用时的工作目录解析，默认输出位置以仓库根目录为基准。

仅发布主程序（不需要 Inno Setup）：

```powershell
.\UICompare.Wpf\Installer\publish.ps1 -OutputDirectory .\output\portable-v1.0.0
```

使用已编译的安装引擎重新发布深色安装器：

```powershell
.\PixelLens.Setup\publish.ps1 -EnginePath .\output\releases\v1.0.0\engine\PixelLens-Engine-v1.0.0.exe -OutputDirectory .\output\setup-v1.0.0-rebuild
```

两个独立发布脚本均支持 `-DotNetPath`。完整交付优先使用根目录的 `build.ps1`，确保主程序、安装引擎和安装器来自同一轮构建。构建参数见 [.NET 发布说明](https://learn.microsoft.com/en-us/dotnet/core/deploying/)和 [Inno Setup 编译器说明](https://jrsoftware.org/ishelp/topic_compilercmdline.htm)。

## 源码目录

| 路径 | 用途 |
| --- | --- |
| `UICompare.Wpf/` | 当前主程序源码、运行所需资源和 Inno 安装脚本 |
| `PixelLens.Setup/` | 深色安装界面及安装引擎调用逻辑 |
| `build.ps1`、`scripts/` | 打包入口和共享工具检查 |
| `design-assets/`、`ui-compare-prototype*.html` | 设计参考和历史原型 |
| `ui_compare_app.py`、`installer.py`、`requirements.txt`、`启动界面对比.bat` | 早期 Python 实现，不参与当前 WPF 构建 |
| `运行说明.md` | V0.0.1 历史说明，当前版本以本 README 为准 |

## GitHub 上传范围

提交源码、项目资源、构建脚本、许可证和适合公开的文档。构建缓存、发布产物、临时工具、本机协作说明及常见凭据文件已由 `.gitignore` 排除。发布时将安装包、免安装程序和校验文件作为 **Release** 附件上传。

Fluent 图标的许可证见 [WPF 资源许可证](UICompare.Wpf/Assets/Fluent-LICENSE.txt)及 [本地图标库许可证](design-assets/icon-library/fluent-ui-1.1.344/LICENSE.txt)。品牌素材来源见 [资源来源说明](UICompare.Wpf/Assets/来源说明.txt)。项目尚未声明整体开源许可证。
