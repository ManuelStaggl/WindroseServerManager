using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using WindroseServerManager.App.Services;
using WindroseServerManager.App.ViewModels;

namespace WindroseServerManager.App.Views.Dialogs;

public partial class InstallWizardWindow : Window
{
    private InstallWizardViewModel? _vm;

    public InstallWizardWindow()
    {
        InitializeComponent();
        KeyDown += OnKeyDown;
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.CloseRequested -= OnCloseRequested;
        }
        _vm = DataContext as InstallWizardViewModel;
        if (_vm is not null)
        {
            _vm.PropertyChanged += OnVmPropertyChanged;
            _vm.CloseRequested += OnCloseRequested;
            UpdateStepper(_vm.CurrentStep);
        }
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstallWizardViewModel.CurrentStep) && _vm is not null)
            UpdateStepper(_vm.CurrentStep);
    }

    private void OnCloseRequested(bool success) => Close(success);

    private void UpdateStepper(int currentStep)
    {
        SetStepState(1, currentStep);
        SetStepState(2, currentStep);
        SetStepState(3, currentStep);
    }

    private void SetStepState(int step, int currentStep)
    {
        var circle = this.FindControl<Border>($"Step{step}Circle");
        var glyph = this.FindControl<TextBlock>($"Step{step}Glyph");
        if (circle is null || glyph is null) return;

        if (step < currentStep)
        {
            // Completed
            circle.Background = ResolveBrush("BrandSuccessBrush");
            glyph.Text = "\u2713";
            glyph.Foreground = ResolveBrush("BrandTextPrimaryBrush");
        }
        else if (step == currentStep)
        {
            // Active
            circle.Background = ResolveBrush("BrandAmberBrush");
            glyph.Text = step.ToString();
            glyph.Foreground = ResolveBrush("BrandTextPrimaryBrush");
        }
        else
        {
            // Inactive
            circle.Background = ResolveBrush("BrandNavySurfaceAltBrush");
            glyph.Text = step.ToString();
            glyph.Foreground = ResolveBrush("BrandTextMutedBrush");
        }
    }

    private IBrush ResolveBrush(string key)
    {
        if (Application.Current is { } app &&
            app.Resources.TryGetResource(key, app.ActualThemeVariant, out var res) &&
            res is IBrush brush)
        {
            return brush;
        }
        return Brushes.Gray;
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (_vm is null) return;
        var top = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null;
        var provider = StorageProvider;
        if (provider is null) return;
        var picks = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.Get("Installation.PickFolder.Title"),
            AllowMultiple = false,
        });
        if (picks.Count > 0)
            _vm.InstallDir = picks[0].Path.LocalPath;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (_vm is { IsInstalling: true })
        {
            _vm.RequestCancelInstall();
            return;
        }
        Close(false);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            OnCancelClick(sender, new RoutedEventArgs());
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _vm is not null)
        {
            if (_vm.CurrentStep < 3)
            {
                if (_vm.GoNextCommand.CanExecute(null))
                    _vm.GoNextCommand.Execute(null);
            }
            else if (!_vm.IsInstalling && _vm.InstallCommand.CanExecute(null))
            {
                _vm.InstallCommand.Execute(null);
            }
            e.Handled = true;
        }
    }
}
