# AutoHudLayout

AutoHudLayout 是适用于 Windows 和 Dalamud API 15 的《最终幻想 XIV》插件。它将游戏窗口**客户区的精确像素尺寸**映射到游戏原生 HUD 布局（1–4）；登录角色后，当窗口尺寸变化并稳定时，插件会自动应用匹配的布局。你可以为不同分辨率配置规则，未匹配时保持当前 HUD，适合在不同显示器或窗口尺寸之间切换。

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
dotnet restore .\AutoHudLayout.sln
dotnet build .\AutoHudLayout.sln -c Release
```

编译后的 DLL 位于 `AutoHudLayout\bin\x64\Release\AutoHudLayout.dll`，打包文件位于 `AutoHudLayout\bin\x64\Release\AutoHudLayout\latest.zip`。本地开发加载时，将 DLL 和同目录的 `AutoHudLayout.json` 放入独立插件目录，再通过 Dalamud 的 Dev Plugins 加载。

## GitHub Actions 发布

修改 `AutoHudLayout/AutoHudLayout.csproj` 的 `<Version>` 并同步更新 `repo.json` 中的 `AssemblyVersion` 和 `TestingAssemblyVersion`。项目的三段版本号会补 `.0` 作为程序集版本，例如 `1.0.1` 对应 `1.0.1.0`。推送到 `master` 后，auto-tag 工作流会在该版本没有标签时创建 `v1.0.1.0`，然后启动 release 工作流。release 会从该标签构建，并将 `latest.zip` 上传为 GitHub Release 附件，供 `repo.json` 的下载链接使用。同一版本的后续提交不会重新打标签；发布修订版时需再次提高版本号。

面向 `master` 的 PR 会运行 CI。直接推送到 `master` 时，只有最后一条提交消息以 `ci:` 或 `ci：` 开头才运行 CI 构建；release 工作流始终会在发布前重新构建。已有标签需要补发时，可以在 Actions 中手动运行 Release AutoHudLayout 并填写标签。

## 注意

- 匹配的是游戏窗口客户区尺寸，不是桌面分辨率或游戏内部渲染分辨率。DPI 缩放也可能影响读取到的尺寸。
- 切换通过游戏原生 `/hudlayout` 命令执行，依赖当前游戏版本的客户端结构。游戏更新后需重新验证。
- 插件记录的是上次发送的切换请求，并不读取当前实际 HUD 布局。测试前建议备份游戏 HUD 设置。
