# WinTiles

WinTiles 是一个基于 `WPF + .NET 8` 的 Windows 桌面工具，用来把一张图片拆分成多个开始菜单磁贴，并按照你在界面里选中的网格顺序依次固定。

它适合用来做这类场景：

- 把一张大图拆成多块，拼成开始菜单磁贴墙
- 为一组图片快速批量生成并固定磁贴
- 保留固定历史，后续按批次清理或删除单块

> 当前项目依赖 `ExplorerPatcher` 提供的经典开始菜单环境。  
> 如果系统没有启用对应能力，WinTiles 会提示无法继续固定图片。

## 功能特性

- 选择任意图片，在 `5 x 5` 网格上启用多个裁切区域
- 通过滚轮缩放、拖拽位置来微调每个区域的取景
- 按启用顺序批量生成磁贴资源并固定到开始菜单
- 保存每次固定的历史记录，支持删除单块或整批清理
- 启动时自动检查 GitHub Releases，也支持手动点击“检查更新”
- 通过 GitHub Actions 自动构建、打包并发布新版本

## 运行要求

- Windows 10 / Windows 11
- 已安装并启用 `ExplorerPatcher`
- 系统里存在 `StartTileData.dll`
- `x64` 环境

如果你只是使用成品版本，直接下载 Release 里的压缩包并解压运行即可。  
如果你要本地开发或自己构建，请继续看下面的开发说明。

## 项目结构

```text
WinTiles.slnx
├─ WinTiles/                     WPF 主程序
├─ WinTiles.Core/                核心模型与服务
├─ WinTiles.Tests/               单元测试
├─ native/TileHost/              磁贴宿主程序
├─ native/WinTiles.PinHelper/    开始菜单固定/取消固定辅助程序
├─ build-native.ps1              构建原生辅助程序
└─ publish-portable.ps1          发布便携版并生成 zip
```

## 本地开发

### 依赖

- `.NET SDK 8`
- `Visual Studio 2022` 或 `Build Tools 2022`
- `MSBuild`（用于构建 `native` 目录下的 C++ 项目）

### 常用命令

1. 构建原生辅助程序

```powershell
.\build-native.ps1 -Configuration Release
```

2. 运行测试

```powershell
dotnet test .\WinTiles.slnx -c Release
```

3. 构建主程序

```powershell
dotnet build .\WinTiles\WinTiles.csproj -c Release
```

4. 生成便携发布包

```powershell
.\publish-portable.ps1 -Configuration Release
```

如果你需要让输出版本号和 GitHub Release 一致，可以额外传入 `Version`：

```powershell
.\publish-portable.ps1 -Configuration Release -Version 0.1.123 -RuntimeIdentifier win-x64
```

生成结果默认位于：

- `artifacts/publish/WinTiles-win-x64/`
- `artifacts/release/WinTiles-v<version>-win-x64.zip`

## 使用说明

1. 启动 WinTiles
2. 点击“选择图片”
3. 在右侧 `5 x 5` 网格中点击启用你想使用的裁切区域
4. 使用滚轮缩放图片，用鼠标拖拽位置
5. 点击“固定图片”
6. 如需查看结果或后续清理，点击“固定历史”

固定完成后，WinTiles 会把相关记录、资源和历史信息保存在程序目录下，便于后续删除或排查。

## 自动发版

仓库内已经包含 GitHub Actions 工作流：

- 文件：`.github/workflows/release.yml`

当前规则如下：

- 推送到 `main`：自动构建、测试、打包，并发布一个 `prerelease`
- 推送 `v*` 标签：自动构建、测试、打包，并发布正式版
- 产物格式：`WinTiles-v<version>-win-x64.zip`

### 推荐发布方式

日常开发：

```powershell
git push origin main
```

正式发版：

```powershell
git tag v0.1.0
git push origin v0.1.0
```

## 更新检查

应用内更新基于 GitHub Releases，当前策略是“先提示，再按用户确认下载”：

- 启动后会异步检查最近的 Release
- 左侧提供“检查更新”按钮
- 检测到新版本后，会提示用户当前版本与目标版本
- 只有用户确认“更新”后，应用才会开始下载更新包
- 下载完成后，会再询问是否立即重启并完成升级

需要注意：

- 如果用户没有确认更新，应用不会自动下载任何内容
- 应用内安装仍然需要一次重启

当前更新源固定为：

- `Arihara-Satoru/WinTiles`

如果你 fork 了这个项目并想改成自己的仓库，需要调整：

- `WinTiles/App.xaml.cs`

## 设计说明

WinTiles 当前不是通过标准安装器发布，而是通过便携版目录运行。  
项目里之所以有两个原生辅助程序，是因为开始菜单磁贴固定这件事并不完全适合只靠托管代码处理：

- `TileHost.exe` 负责作为磁贴点击入口，把激活回传给主程序
- `WinTiles.PinHelper.exe` 负责调用底层固定/取消固定逻辑

主程序负责：

- 图片选择与裁切交互
- 资源生成
- 快捷方式与磁贴清单写入
- 历史记录管理
- 更新检查

## 测试

当前仓库包含 `xUnit` 单元测试，覆盖了部分核心逻辑，例如：

- 裁切布局计算
- 图片资源生成
- 固定命令参数拼接
- 历史记录读写
- 开始菜单快捷方式相关逻辑

运行命令：

```powershell
dotnet test .\WinTiles.slnx -c Release
```

## 已知限制

- 仅支持 `Windows x64`
- 依赖 `ExplorerPatcher` 经典开始菜单能力
- 应用内不会自动下载或热替换新版本
- GitHub Actions 当前产出的是便携压缩包，不是安装包

## 许可证

本项目使用 [MIT License](LICENSE)。
