using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using WindroseServerManager.Core.Services;

namespace WindroseServerManager.App.ViewModels;

public partial class ConfigEntryViewModel : ObservableObject
{
    public ConfigEntrySchema Schema { get; }

    [ObservableProperty] private string _rawValue = string.Empty;
    [ObservableProperty] private string? _errorMessage;

    public string Category => Schema.Category;
    public string Key => Schema.Key;
    public string DisplayName => Schema.Key;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    public ConfigEntryViewModel(ConfigEntrySchema schema, object? initialValue)
    {
        Schema = schema;
        _rawValue = FormatValue(initialValue, schema);
        Validate();
    }

    partial void OnRawValueChanged(string value) => Validate();

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
