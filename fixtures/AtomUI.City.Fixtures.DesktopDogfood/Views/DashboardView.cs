using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using AtomUI.City.Presentation;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DashboardView : UserControl, IViewDataContextAware
{
    public DashboardView()
    {
        var title = new TextBlock { FontSize = 28, FontWeight = FontWeight.SemiBold };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.Title)));
        var subtitle = new TextBlock { FontSize = 14, Foreground = Brush.Parse("#52606D") };
        subtitle.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.Subtitle)));

        var activation = new TextBlock { Foreground = Brush.Parse("#087F5B"), TextWrapping = TextWrapping.Wrap };
        activation.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.ActivationStatus)));
        var projection = new TextBlock { Foreground = Brush.Parse("#52606D"), TextWrapping = TextWrapping.Wrap };
        projection.Bind(TextBlock.TextProperty, new Binding(nameof(DashboardViewModel.ProjectionSample)));

        Content = new StackPanel
        {
            Spacing = 20,
            Children =
            {
                new StackPanel { Spacing = 4, Children = { title, subtitle } },
                CreateMetrics(),
                new Border
                {
                    Background = Brush.Parse("#FFFFFF"),
                    BorderBrush = Brush.Parse("#D9E2EC"),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(18),
                    Child = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock { Text = "MVVM activation probe", FontSize = 16, FontWeight = FontWeight.SemiBold },
                            activation,
                            projection,
                        },
                    },
                },
            },
        };
    }

    private static Control CreateMetrics()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*"),
            ColumnSpacing = 12,
        };
        var values = new[]
        {
            ("Application modules", "48", "Generated dependency graph"),
            ("Business services", "110", "Singleton, scoped, transient, keyed"),
            ("Named outlets", "4", "Transactional Presentation commits"),
            ("Runtime state", "READY", "Host and Avalonia attached"),
        };

        for (var index = 0; index < values.Length; index++)
        {
            var item = values[index];
            var panel = new Border
            {
                Background = Brush.Parse("#FFFFFF"),
                BorderBrush = Brush.Parse("#D9E2EC"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(16),
                Child = new StackPanel
                {
                    Spacing = 6,
                    Children =
                    {
                        new TextBlock { Text = item.Item1, Foreground = Brush.Parse("#52606D") },
                        new TextBlock { Text = item.Item2, FontSize = 24, FontWeight = FontWeight.Bold },
                        new TextBlock { Text = item.Item3, FontSize = 12, Foreground = Brush.Parse("#7B8794"), TextWrapping = TextWrapping.Wrap },
                    },
                },
            };
            Grid.SetColumn(panel, index);
            grid.Children.Add(panel);
        }

        return grid;
    }
}
