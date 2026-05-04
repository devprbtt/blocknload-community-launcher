using System.Text.Json.Serialization;

namespace BnlCommunityFixes.Core.Models;

public sealed class FontSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("selected_font")]
    public string SelectedFont { get; set; } = "__DEFAULT__";

    [JsonPropertyName("font_style")]
    public string FontStyle { get; set; } = "Keep";

    [JsonPropertyName("size_multiplier")]
    public double SizeMultiplier { get; set; } = 1.0;

    [JsonPropertyName("line_spacing_multiplier")]
    public double LineSpacingMultiplier { get; set; } = 1.0;
}
