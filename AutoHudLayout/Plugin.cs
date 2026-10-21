using System.Diagnostics;
using System.Runtime.InteropServices;
using Dalamud.Game.Command;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;

namespace AutoHudLayout;

public sealed partial class Plugin : IDalamudPlugin
{
    public string Name => "AutoHudLayout";
    private static readonly (int Width, int Height)[] CommonResolutions =
    [
        (1280, 720), (1280, 800), (1366, 768), (1440, 900),
        (1600, 900), (1680, 1050), (1920, 1080), (1920, 1200),
        (2560, 1080), (2560, 1440), (2560, 1600),
        (3440, 1440), (3840, 1600), (3840, 2160), (5120, 1440),
    ];
    private readonly IDalamudPluginInterface _pi;
    private readonly ICommandManager _commands;
    private readonly IFramework _framework;
    private readonly IClientState _client;
    private readonly IPluginLog _log;
    private readonly Configuration _config;
    private bool _showSettings;
    private long _lastPoll;
    private long _changedAt;
    private long _lastHudRequestAt;
    private (int Width, int Height) _observed;
    private (int Width, int Height) _appliedResolution;
    private int _lastRequestedHud;
    private int? _lastMatchedHud;
    private string _status = "等待游戏窗口";

    public Plugin(IDalamudPluginInterface pi, ICommandManager commands, IFramework framework,
        IClientState client, IPluginLog log)
    {
        this._pi = pi;
        this._commands = commands;
        this._framework = framework;
        this._client = client;
        this._log = log;
        _config = pi.GetPluginConfig() as Configuration ?? new Configuration();
        commands.AddHandler("/autohud", new CommandInfo(OnCommand)
        {
            HelpMessage = "打开配置；/autohud on|off|status|recheck"
        });
        pi.UiBuilder.Draw += Draw;
        pi.UiBuilder.OpenMainUi += OpenSettings;
        pi.UiBuilder.OpenConfigUi += OpenSettings;
        framework.Update += OnUpdate;
    }

    private void OpenSettings() => _showSettings = true;

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "on": _config.Enabled = true; Reset(); Save(); break;
            case "off": _config.Enabled = false; Save(); break;
            case "recheck": Reset(); break;
            case "status": _log.Information($"AutoHudLayout: {_status}"); break;
            default: _showSettings = true; break;
        }
    }

    private void Reset()
    {
        _observed = default;
        _appliedResolution = default;
        _changedAt = 0;
        _lastHudRequestAt = 0;
        _lastRequestedHud = 0;
        _lastMatchedHud = null;
        _lastPoll = 0;
    }

    private void Save() => _pi.SavePluginConfig(_config);

    private void OnUpdate(IFramework _)
    {
        if (!_config.Enabled) return;
        if (!_client.IsLoggedIn)
        {
            Reset();
            return;
        }
        var now = Stopwatch.GetTimestamp();
        if (Stopwatch.GetElapsedTime(_lastPoll, now).TotalMilliseconds < Math.Clamp(_config.PollMilliseconds, 200, 10000)) return;
        _lastPoll = now;

        var handle = _pi.UiBuilder.WindowHandlePtr;
        if (handle == 0 || !GetClientRect(handle, out var rect))
        {
            _status = "无法读取游戏窗口";
            return;
        }
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        if (width < 320 || height < 240) return;
        var current = (width, height);
        _status = $"检测到 {width}x{height}";
        if (current != _observed)
        {
            _observed = current;
            _changedAt = now;
            _appliedResolution = default;
            _lastHudRequestAt = 0;
            _lastRequestedHud = 0;
            return;
        }
        if (Stopwatch.GetElapsedTime(_changedAt, now).TotalMilliseconds < Math.Clamp(_config.StableMilliseconds, 300, 10000)) return;
        var rule = _config.Rules.FirstOrDefault(x => x.Width == width && x.Height == height && x.Hud is >= 1 and <= 4);
        if (rule is null)
        {
            _status += "；无匹配规则，保持 HUD 不变";
            _lastMatchedHud = null;
            return;
        }
        _lastMatchedHud = rule.Hud;
        _status += $"；目标 HUD {rule.Hud}";
        // 只有确认游戏实际切换后才停止重试；确认后仍允许玩家手动更换 HUD。
        if (_appliedResolution == current && _lastRequestedHud == rule.Hud)
        {
            _status += "；已确认";
            return;
        }
        if (!TryGetCurrentHud(out var currentHud))
        {
            _status += "；等待游戏 HUD 就绪";
            return;
        }
        if (currentHud == rule.Hud)
        {
            _appliedResolution = current;
            _lastRequestedHud = rule.Hud;
            _status += "；已确认";
            _log.Information($"AutoHudLayout: {width}x{height} -> HUD {rule.Hud} 已确认");
            return;
        }
        if (_lastRequestedHud == rule.Hud && _lastHudRequestAt != 0 &&
            Stopwatch.GetElapsedTime(_lastHudRequestAt, now).TotalMilliseconds < 2000)
        {
            _status += $"；当前 HUD {currentHud}，等待切换确认";
            return;
        }
        try
        {
            ExecuteHudCommand(rule.Hud);
            _lastRequestedHud = rule.Hud;
            _lastHudRequestAt = now;
            _status += $"；当前 HUD {currentHud}，已发送切换请求";
        }
        catch (Exception e)
        {
            _log.Error(e, "AutoHudLayout: HUD 切换失败");
            _status += "；执行失败，详见插件日志";
        }
    }

    private static unsafe bool TryGetCurrentHud(out int hud)
    {
        hud = 0;
        var ui = UIModule.Instance();
        if (ui == null || !ui->IsUIReady()) return false;
        var addonConfig = ui->GetAddonConfig();
        if (addonConfig == null || !addonConfig->IsLoaded || addonConfig->ActiveDataSet == null) return false;
        hud = addonConfig->ActiveDataSet->CurrentHudLayout + 1;
        return hud is >= 1 and <= 4;
    }

    // 在游戏主线程调用游戏原生命令解析器。该互操作依赖游戏版本。
    private static unsafe void ExecuteHudCommand(int layout)
    {
        var shell = RaptureShellModule.Instance();
        var ui = UIModule.Instance();
        if (shell == null || ui == null)
            throw new InvalidOperationException("游戏 UI 模块尚未就绪");
        using var command = new Utf8String($"/hudlayout {layout}");
        shell->ExecuteCommandInner(&command, ui);
    }

    private void Draw()
    {
        if (!_showSettings) return;
        if (!ImGui.Begin("AutoHudLayout - 分辨率 HUD 自动切换", ref _showSettings))
        {
            ImGui.End();
            return;
        }
        var dirty = false;
        var enabled = _config.Enabled;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("启用自动切换");
        ImGui.SameLine();
        if (ImGui.Checkbox("##启用自动切换", ref enabled)) { _config.Enabled = enabled; Reset(); dirty = true; }
        ImGui.TextUnformatted(_status);
        ImGui.TextWrapped("匹配游戏窗口客户区的精确像素尺寸；未知分辨率保持当前 HUD。规则从上到下优先匹配。");
        ImGui.Separator();
        for (var i = 0; i < _config.Rules.Count; i++)
        {
            var rule = _config.Rules[i];
            ImGui.PushID(i);
            var hud = rule.Hud;
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("分辨率");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(180);
            if (ImGui.BeginCombo("##分辨率", $"{rule.Width}×{rule.Height}"))
            {
                foreach (var resolution in CommonResolutions)
                {
                    var selected = rule.Width == resolution.Width && rule.Height == resolution.Height;
                    if (!ImGui.Selectable($"{resolution.Width}×{resolution.Height}", selected)) continue;
                    rule.Width = resolution.Width;
                    rule.Height = resolution.Height;
                    dirty = true;
                }
                ImGui.EndCombo();
            }
            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted("HUD");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(90);
            if (ImGui.SliderInt("##HUD", ref hud, 1, 4)) { rule.Hud = hud; dirty = true; }
            ImGui.SameLine();
            if (ImGui.Button("删除")) { _config.Rules.RemoveAt(i); dirty = true; ImGui.PopID(); break; }
            ImGui.PopID();
        }
        if (ImGui.Button("添加规则"))
        {
            var resolution = CommonResolutions.FirstOrDefault(candidate =>
                _config.Rules.All(rule => rule.Width != candidate.Width || rule.Height != candidate.Height));
            if (resolution == default) resolution = (1920, 1080);
            _config.Rules.Add(new ResolutionRule(resolution.Width, resolution.Height, 1));
            dirty = true;
        }
        ImGui.SameLine();
        if (ImGui.Button("立即重新检测")) Reset();
        ImGui.Separator();
        var poll = _config.PollMilliseconds;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("检测间隔 (ms)");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(240);
        if (ImGui.SliderInt("##检测间隔", ref poll, 200, 3000)) { _config.PollMilliseconds = poll; dirty = true; }
        var stable = _config.StableMilliseconds;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("稳定时间 (ms)");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(240);
        if (ImGui.SliderInt("##稳定时间", ref stable, 300, 5000)) { _config.StableMilliseconds = stable; dirty = true; }
        ImGui.TextWrapped("提示：切换未生效时会自动重试；手动改动 HUD 后使用 /autohud recheck。请先备份游戏 HUD 设置。");
        if (dirty) { Reset(); Save(); }
        ImGui.End();
    }

    public void Dispose()
    {
        _framework.Update -= OnUpdate;
        _pi.UiBuilder.Draw -= Draw;
        _pi.UiBuilder.OpenMainUi -= OpenSettings;
        _pi.UiBuilder.OpenConfigUi -= OpenSettings;
        _commands.RemoveHandler("/autohud");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect { public int Left, Top, Right, Bottom; }

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetClientRect(nint hWnd, out Rect rect);
}
