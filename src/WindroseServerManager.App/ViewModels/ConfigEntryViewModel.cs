using System;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WindroseServerManager.App.Services;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class ConfigEntryViewModel : ObservableObject
{
    public ConfigEntrySchema Schema { get; }

    [ObservableProperty] private string _rawValue = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isPasswordVisible;

    public string Category => Schema.Category;
    public string Key => Schema.Key;

    // Localized display
    public string DisplayName => Loc.Get(Schema.DescriptionKey + ".Name");
    public string Description => Loc.Get(Schema.DescriptionKey + ".Desc");
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    // Type discriminators for the view
    public bool IsFloat    => Schema.Type == "float";
    public bool IsBool     => Schema.Type == "bool";
    public bool IsInt      => Schema.Type == "int";
    public bool IsPassword => Schema.Key == "password" && Schema.JsonSection == "rcon";
    public bool IsText     => !IsFloat && !IsBool;

    // Slider range capped at 10 for practical UX (text box allows up to schema max)
    public double SliderMin => Schema.Min ?? 0.1;
    public double SliderMax => Math.Min(Schema.Max ?? 10.0, 10.0);

    // Two-way double property for Slider (floats only)
    public double SliderValue
    {
        get => IsFloat && double.TryParse(RawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
               ? Math.Clamp(d, SliderMin, SliderMax)
               : 1.0;
        set
        {
            if (!IsFloat) return;
            var formatted = value.ToString("0.##", CultureInfo.InvariantCulture);
            if (formatted != RawValue)
                RawValue = formatted;
        }
    }

    // Two-way bool property for ToggleSwitch (bools only)
    public bool BoolValue
    {
        get => bool.TryParse(RawValue, out var b) && b;
        set => RawValue = value ? "true" : "false";
    }

    public ConfigEntryViewModel(ConfigEntrySchema schema, object? initialValue)
    {
        Schema = schema;
        _rawValue = FormatValue(initialValue, schema);
        Validate();
    }

    partial void OnRawValueChanged(string value)
    {
        Validate();
        OnPropertyChanged(nameof(SliderValue));
        OnPropertyChanged(nameof(BoolValue));
    }

    [RelayCommand]
    public void Reset() => RawValue = FormatValue(null, Schema);

    [RelayCommand]
    private void TogglePassword() => IsPasswordVisible = !IsPasswordVisible;

    private void Validate()
    {
        ErrorMessage = WindrosePlusConfigSchema.Validate(Schema.Key, RawValue);
        OnPropertyChanged(nameof(HasError));
    }

    public object? ToTypedValue()
    {
        return Schema.Type switch
        {
            "float" => double.Parse(RawValue, NumberStyles.Float, CultureInfo.InvariantCulture),
            "int"   => (object)int.Parse(RawValue, CultureInfo.InvariantCulture),
            "bool"  => bool.Parse(RawValue),
            _       => RawValue,
        };
    }

    private static string FormatValue(object? value, ConfigEntrySchema schema)
    {
        if (value is null)
            return schema.Default?.ToString() ?? string.Empty;

        if (value is System.Text.Json.JsonElement je)
        {
            return je.ValueKind switch
            {
                System.Text.Json.JsonValueKind.Number => je.GetRawText(),
                System.Text.Json.JsonValueKind.True   => "true",
                System.Text.Json.JsonValueKind.False  => "false",
                System.Text.Json.JsonValueKind.String => je.GetString() ?? string.Empty,
                _                                     => je.GetRawText(),
            };
        }

        return value is double d
            ? d.ToString(CultureInfo.InvariantCulture)
            : value.ToString() ?? string.Empty;
    }
}
