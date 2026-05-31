using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class ColorPickerDialog : Window
{
    // 48-color palette matching the Windows ColorDialog basic colors (8 cols × 6 rows)
    private static readonly string[] BasicColors =
    [
        "#FF8080", "#FFFF80", "#80FF80", "#00FF80", "#80FFFF", "#0080FF", "#FF80C0", "#FF80FF",
        "#FF0000", "#FFFF00", "#80FF00", "#00FF40", "#00FFFF", "#0080C0", "#8080C0", "#FF00FF",
        "#804040", "#FF8040", "#00FF00", "#008080", "#004080", "#8080FF", "#800040", "#FF0080",
        "#800000", "#FF8000", "#008000", "#008040", "#0000FF", "#0000A0", "#800080", "#8000FF",
        "#400000", "#804000", "#004000", "#004040", "#000080", "#000040", "#400040", "#400080",
        "#000000", "#808000", "#808040", "#808080", "#408080", "#C0C0C0", "#400040", "#FFFFFF",
    ];

    private bool _updating;

    public ColorPickerDialog() { InitializeComponent(); }

     public ColorPickerDialog(string initialHex)
    {
        InitializeComponent();

        // Populate palette
        var brushes = BasicColors.Select(static h =>
            Color.TryParse(h, out var c) ? (object)new SolidColorBrush(c) : Brushes.Gray).ToList();
        PaletteItems.ItemsSource = brushes;

        RSlider.ValueChanged += (_, _) => OnSliderChanged();
        GSlider.ValueChanged += (_, _) => OnSliderChanged();
        BSlider.ValueChanged += (_, _) => OnSliderChanged();
        HexInput.TextChanged += (_, _) => OnHexChanged();

        SetFromHex(initialHex);
    }

    private void SetFromHex(string hex)
    {
        _updating = true;
        try
        {
            if (Color.TryParse(hex, out var c))
            {
                RSlider.Value = c.R;
                GSlider.Value = c.G;
                BSlider.Value = c.B;
                HexInput.Text = ToHex(c.R, c.G, c.B);
                UpdatePreview(c.R, c.G, c.B);
            }
        }
        finally { _updating = false; }
    }

    private void OnSliderChanged()
    {
        if (_updating) return;
        _updating = true;
        try
        {
            var r = (byte)RSlider.Value;
            var g = (byte)GSlider.Value;
            var b = (byte)BSlider.Value;
            HexInput.Text = ToHex(r, g, b);
            UpdatePreview(r, g, b);
        }
        finally { _updating = false; }
    }

    private void OnHexChanged()
    {
        if (_updating) return;
        var text = HexInput.Text ?? string.Empty;
        if (Color.TryParse(text, out var c))
        {
            _updating = true;
            try
            {
                RSlider.Value = c.R;
                GSlider.Value = c.G;
                BSlider.Value = c.B;
                UpdatePreview(c.R, c.G, c.B);
            }
            finally { _updating = false; }
        }
    }

    private void UpdatePreview(byte r, byte g, byte b)
    {
        var color = Color.FromRgb(r, g, b);
        PreviewBorder.Background = new SolidColorBrush(color);
        RLabel.Text = r.ToString();
        GLabel.Text = g.ToString();
        BLabel.Text = b.ToString();
    }

    private void PaletteColor_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { Background: SolidColorBrush brush })
            SetFromHex($"#{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}");
    }

    private static string ToHex(byte r, byte g, byte b) => $"#{r:X2}{g:X2}{b:X2}";

    private void Ok_Click(object? sender, RoutedEventArgs e) =>
        Close(HexInput.Text?.ToUpperInvariant() ?? "#FFFFFF");

    private void Cancel_Click(object? sender, RoutedEventArgs e) => Close(null);
}
