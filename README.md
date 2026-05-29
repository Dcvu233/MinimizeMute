# MinimizeMute

MinimizeMute 是一个 Windows 桌面工具，可以让指定应用在窗口最小化时自动静音，并在恢复窗口后还原原来的音量和静音状态。

## 功能

- 显示当前正在运行的窗口应用列表
- 将指定应用加入“最小化静音”列表
- 应用所有窗口都最小化后自动静音
- 恢复窗口后自动还原之前的音量/静音状态
- 支持 Chrome 等多进程应用
- 支持“所有程序最小化时静音”
- 关闭窗口时最小化到系统托盘
- 托盘菜单支持显示窗口和退出程序

## 系统要求

- Windows 10 或更新版本
- x64 系统

如果使用 Release 安装包或自包含发布版，不需要提前安装 .NET 运行时。

## 下载安装

从 GitHub Releases 下载 `MinimizeMuteSetup.exe`，双击安装即可。

默认安装位置：

```text
%LocalAppData%\Programs\MinimizeMute
```

安装程序会创建开始菜单快捷方式。

## 使用方法

1. 启动 MinimizeMute。
2. 在左侧“当前启动的应用”列表中选中一个应用。
3. 点击“加入最小化静音”。
4. 当该应用的所有窗口都最小化时，程序会自动静音它。
5. 恢复窗口后，程序会还原它原来的音量和静音状态。

勾选“所有程序最小化时静音”后，右侧列表会被忽略，所有有窗口的程序都会进入监控。

点击窗口右上角关闭按钮时，程序会隐藏到系统托盘并继续工作。右键托盘图标可以显示窗口或退出程序。

## 构建

需要安装 .NET SDK 7.0 或更新版本。

发布自包含版本：

```powershell
.\scripts\build-release.ps1
```

输出目录：

```text
dist\publish
```

构建 Windows 安装包：

```powershell
.\scripts\build-installer.ps1
```

输出文件：

```text
dist\MinimizeMuteSetup.exe
```

安装包使用 7-Zip SFX 生成。构建机器需要安装完整 7-Zip，并确保 `7z.exe` 可以在 PATH 中找到。

## 开发运行

```powershell
dotnet run
```

## 设置文件

应用设置保存在：

```text
%AppData%\MinimizeMute\settings.json
```

## 技术说明

- 界面使用 WinForms。
- 音频控制使用 Windows CoreAudio，通过 NAudio 封装访问。
- 窗口状态通过 Win32 API 枚举顶层窗口并判断是否最小化。
- 对 Chrome 这类多进程应用，静音逻辑按进程名匹配同一应用的所有音频会话。
