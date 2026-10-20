# AutoHudLayout

AutoHudLayout 是适用于 Windows 和 Dalamud API 15 的 FFXIV 插件。登录角色后，它根据游戏窗口**客户区的精确像素尺寸**，自动切换到对应的游戏原生 HUD 布局（1–4）。

## 默认规则

| 客户区尺寸 | HUD 布局 |
| --- | --- |
| 1920 × 1080 | 1 |
| 2560 × 1440 | 2 |

每 500 毫秒检测一次窗口尺寸；尺寸连续稳定 1200 毫秒后才匹配规则。没有匹配规则时保持当前 HUD。多条规则匹配同一尺寸时，使用列表中靠前的规则。

## 使用

输入 `/autohud`，或从 Dalamud 的插件主界面及配置入口打开设置。可以启用或停用自动切换，使用常用分辨率下拉列表添加、修改、删除规则，并调整检测间隔与稳定时间。已有配置中的自定义尺寸仍会显示，也可以改选预设尺寸。

| 命令 | 作用 |
| --- | --- |
| `/autohud` | 打开设置 |
| `/autohud on` | 启用自动切换 |
| `/autohud off` | 停用自动切换 |
| `/autohud recheck` | 清除上次请求记录，重新检测并应用匹配规则 |
| `/autohud status` | 将当前状态写入插件日志 |

插件不会反复发送相同的切换请求。如果在分辨率不变时手动更换了 HUD，可使用 `/autohud recheck` 或设置中的“立即重新检测”。

## 本地构建

需要 Windows、.NET 10 SDK，以及可用的本地 Dalamud API 15 开发环境。在仓库根目录运行：

```powershell
dotnet restore .\AutoHudLayout\AutoHudLayout.csproj
dotnet build .\AutoHudLayout\AutoHudLayout.csproj -c Release
```

编译后的 DLL 位于 `AutoHudLayout\bin\Release\AutoHudLayout.dll`，打包文件位于 `AutoHudLayout\bin\Release\AutoHudLayout\latest.zip`。本地开发加载时，将 DLL 和同目录的 `AutoHudLayout.json` 放入独立插件目录，再通过 Dalamud 的 Dev Plugins 加载。

## 注意

- 匹配的是游戏窗口客户区尺寸，不是桌面分辨率或游戏内部渲染分辨率。DPI 缩放也可能影响读取到的尺寸。
- 切换通过游戏原生 `/hudlayout` 命令执行，依赖当前游戏版本的客户端结构。游戏更新后需重新验证。
- 插件记录的是上次发送的切换请求，并不读取当前实际 HUD 布局。测试前建议备份游戏 HUD 设置。

## GitHub Actions

推送到 `master` 的最新提交消息以 `ci:` 或 `ci：` 开头时，工作流会构建 Release；面向 `master` 的 PR 也会执行构建检查。一次推送包含多个提交时，以最后一个提交的消息判断是否运行。例如：

```powershell
git commit -m "ci: build plugin"
git push origin master
```

工作流参考 [FFXIV-AutoFATEGrind 的 CI](https://github.com/lizvik/FFXIV-AutoFATEGrind/blob/master/.github/workflows/ci.yml)：在 Windows 上下载 Dalamud 开发文件，使用 .NET 10 锁定依赖构建 Release，并将插件包上传为 GitHub Actions artifact。`ci:` 提交只触发构建和打包；插件仓库元数据应由独立发布流程维护。
