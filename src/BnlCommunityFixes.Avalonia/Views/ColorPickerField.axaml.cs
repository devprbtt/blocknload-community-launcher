using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace BnlCommunityFixes.Avalonia.Views;

public partial class ColorPickerField : UserControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<ColorPickerField, string>(nameof(Label));

    public static readonly StyledProperty<string> ColorHexProperty =
        AvaloniaProperty.Register<ColorPickerField, string>(nameof(ColorHex), "#FFFFFF",
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public string Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string ColorHex
    {
        get => GetValue(ColorHexProperty);
        set => SetValue(ColorHexProperty, value);
    }

    public ColorPickerField()
    {
        InitializeComponent();

        FieldLabel.Bind(TextBlock.TextProperty, this.GetObservable(LabelProperty));

        // Sync TextBox ↔ ColorHex property
        HexBox.TextChanged += (_, _) =>
        {
            var text = HexBox.Text ?? string.Empty;
            ColorHex = text;
            UpdateSwatch(text);
        };

        PropertyChanged += (_, e) =>
        {
            if (e.Property == ColorHexProperty)
            {
                var hex = ColorHex;
                if (HexBox.Text != hex)
                    HexBox.Text = hex;
                UpdateSwatch(hex);
            }
        };

        // Click swatch to open color picker dialog
        Swatch.PointerPressed += async (_, e) =>
        {
            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
                await OpenPickerAsync();
        };
    }

    private void UpdateSwatch(string hex)
    {
        try
        {
            if (Color.TryParse(hex, out var color))
                Swatch.Background = new SolidColorBrush(color);
            else
                Swatch.Background = Brushes.Transparent;
        }
        catch
        {
            Swatch.Background = Brushes.Transparent;
        }
    }

    private async Task OpenPickerAsync()
    {
        var win = TopLevel.GetTopLevel(this) as Window;
        if (win is null) return;

        var dlg = new ColorPickerDialog(ColorHex);
        var result = await dlg.ShowDialog<string?>(win);
        if (result is not null)
            ColorHex = result;
    }
}
