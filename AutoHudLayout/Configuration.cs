using Dalamud.Configuration;
using Newtonsoft.Json;

namespace AutoHudLayout;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public int PollMilliseconds { get; set; } = 500;
    public int StableMilliseconds { get; set; } = 1200;
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<ResolutionRule> Rules { get; set; } =
    [
        new(1920, 1080, 1),
        new(2560, 1440, 2),
    ];
}

[Serializable]
public sealed class ResolutionRule
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int Hud { get; set; }
    public ResolutionRule() { }
    public ResolutionRule(int width, int height, int hud) => (Width, Height, Hud) = (width, height, hud);
}
