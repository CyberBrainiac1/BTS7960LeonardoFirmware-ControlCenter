namespace ArduinoFFBControlCenter.Helpers;

public static class GlyphHelper
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["home"] = "\uE80F",
        ["setup"] = "\uE8D2",
        ["calibration"] = "\uE9D9",
        ["firmware"] = "\uE896",
        ["ffb"] = "\uE7C1",
        ["steering"] = "\uE804",
        ["pedals"] = "\uEA86",
        ["buttons"] = "\uE765",
        ["profiles"] = "\uEB51",
        ["telemetry"] = "\uE9D2",
        ["timeline"] = "\uE8A5",
        ["selftest"] = "\uE7BA",
        ["phone"] = "\uE8EA",
        ["ai"] = "\uE7BE",
        ["tools"] = "\uE8B8",
        ["diagnostics"] = "\uE9CE",
        ["settings"] = "\uE713",
        ["connection"] = "\uE839",
        ["health"] = "\uEA18",
        ["save"] = "\uE74E",
        ["warning"] = "\uE7BA",
        ["chevronDown"] = "\uE70D",
        ["chevronUp"] = "\uE70E",
        ["qr"] = "\uED14"
    };

    public static string Get(string key, string fallback = "\uE8A5")
    {
        return Map.TryGetValue(key, out var glyph) ? glyph : fallback;
    }
}
