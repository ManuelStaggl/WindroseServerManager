using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using WindroseServerManager.App.ViewModels;

namespace WindroseServerManager.App;

[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var vmTypeName = param.GetType().FullName!;
        var candidates = new[]
        {
            vmTypeName.Replace(".ViewModels.", ".Views.Pages.", StringComparison.Ordinal)
                      .Replace("ViewModel", "View", StringComparison.Ordinal),
            vmTypeName.Replace(".ViewModels.", ".Views.", StringComparison.Ordinal)
                      .Replace("ViewModel", "View", StringComparison.Ordinal),
            vmTypeName.Replace("ViewModel", "View", StringComparison.Ordinal),
        };

        foreach (var candidate in candidates)
        {
            var type = Type.GetType(candidate);
            if (type != null)
                return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "View not found for: " + vmTypeName };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
