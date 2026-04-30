# MediaPlayerGlobalHotkeys

[English version](README.md)

`MediaPlayerGlobalHotkeys` 是一个给 Windows 内置 `Media Player` 用的小助手。它提供几个稳定的全局热键，让你不用每次把播放器切回前台，也能直接播放、暂停、快进和后退。

它不是把按键乱发给当前活动窗口，而是直接走 Windows 的 media session API 去控制内置播放器会话。

## 快速开始

如果你只是想直接使用它，看这一段就够了：

1. 打开 [最新 release 页面](https://github.com/gary1110086/media-player-global-hotkeys/releases/latest)。
2. 下载其中名字像 `MediaPlayerGlobalHotkeys-...zip` 的那个资源包。
3. 解压到任意你想放的位置。
4. 运行 `MediaPlayerGlobalHotkeys.exe`。

说明：

- 这个程序没有可见 UI。启动后它会在后台运行并监听热键。
- release 页面里如果还看到 `Source code (zip)` 和 `Source code (tar.gz)`，那是 GitHub 自动生成的源码包，是给想看源码的人准备的，不是普通用户要下载的成品。

## 热键

- `Ctrl+Alt+Space`：播放 / 暂停
- `Ctrl+Alt+Left`：后退 5 秒
- `Ctrl+Alt+Right`：前进 5 秒
- 按住 `Ctrl+Alt+Left` 或 `Ctrl+Alt+Right` 不放，会在短暂延迟后持续 seek

## 运行要求

- Windows
- 系统内置的 `Media Player`
- 播放器里已经有一个可以播放、也可以 seek 的媒体会话

## 常见说明

- 如果按了没反应，先确认 `Media Player` 已经打开，而且里面真的加载了可播放内容。
- 运行日志会写到 exe 同目录下的 `logs` 文件夹里。

## 自启动

如果你希望它在你登录 Windows 后自动启动，可以运行：

```powershell
.\MediaPlayerGlobalHotkeys.exe --install-startup-task
```

以后如果想把这个计划任务自启动去掉，可以运行：

```powershell
.\MediaPlayerGlobalHotkeys.exe --uninstall-startup-task
```

## 给开发者

如果你是想看源码或自己编译，建议从这些文件开始。否则下面这些内容你都可以先忽略：

- [Program.cs](src/MediaPlayerGlobalHotkeys/Program.cs)：程序入口和单实例启动
- [HotkeyAppContext.cs](src/MediaPlayerGlobalHotkeys/HotkeyAppContext.cs)：后台应用上下文和默认热键绑定
- [LowLevelKeyboardHotkeyMonitor.cs](src/MediaPlayerGlobalHotkeys/LowLevelKeyboardHotkeyMonitor.cs)：低级键盘钩子和按住连续 seek 逻辑
- [MediaPlayerController.cs](src/MediaPlayerGlobalHotkeys/MediaPlayerController.cs)：Windows media session 控制和 seek 状态处理
- [build.ps1](scripts/build.ps1)：本地构建脚本

本地构建产物默认在 `bin\MediaPlayerGlobalHotkeys.exe`。

## 构建

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
```

## 测试

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tests\MediaPlayerGlobalHotkeys.Tests.ps1
```

## 工作方式

```mermaid
flowchart LR
    A[Ctrl+Alt+Space / Left / Right] --> B[LowLevelKeyboardHotkeyMonitor]
    B --> C[MediaPlayerController]
    C --> D[Windows media session API]
    D --> E[Built-in Media Player]
```

## 开发备注

- 项目里有一份简短开发说明：[development-notes.md](docs/development-notes.md)。
- 当前本地构建默认依赖 `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe`。
