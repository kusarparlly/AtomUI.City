using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using AtomUI.City.Core.DependencyInjection;
using CityLearning.Workbench.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace CityLearning.Workbench.Views;

[Service(ServiceLifetime.Singleton)]
public sealed partial class MainWindow : Window
{
    private readonly WorkbenchViewModel _viewModel;

    public MainWindow(WorkbenchViewModel viewModel)
    {
        _viewModel = viewModel;
        AvaloniaXamlLoader.Load(this);
        DataContext = viewModel;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs eventArgs) =>
        await ExecuteAsync(() => _viewModel.LoadAsync());

    private async void OnAddClick(object? sender, RoutedEventArgs eventArgs) =>
        await AddFromInputAsync();

    private async void OnTitleInputKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Enter)
        {
            return;
        }

        eventArgs.Handled = true;
        await AddFromInputAsync();
    }

    private async void OnRefreshClick(object? sender, RoutedEventArgs eventArgs) =>
        await ExecuteAsync(() => _viewModel.LoadAsync());

    private async void OnCompleteClick(object? sender, RoutedEventArgs eventArgs) =>
        await ExecuteAsync(() => _viewModel.CompleteSelectedAsync());

    private async Task AddFromInputAsync()
    {
        var titleInput = this.FindControl<TextBox>("TitleInput")!;
        if (string.IsNullOrWhiteSpace(titleInput.Text))
        {
            return;
        }

        var title = titleInput.Text;
        await ExecuteAsync(() => _viewModel.AddAsync(title));
        titleInput.Clear();
        titleInput.Focus();
    }

    private async Task ExecuteAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception exception)
        {
            _viewModel.ReportError(exception);
        }
    }
}
