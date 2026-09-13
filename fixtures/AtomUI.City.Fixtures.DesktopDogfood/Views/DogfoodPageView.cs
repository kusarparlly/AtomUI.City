using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using AtomUI.City.Presentation;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodPageView : UserControl, IViewDataContextAware
{
    public DogfoodPageView()
    {
        var title = new TextBlock { FontSize = 28, FontWeight = FontWeight.SemiBold };
        title.Bind(TextBlock.TextProperty, new Binding(nameof(DogfoodPageViewModel.PageTitle)));
        var activity = new TextBlock { Foreground = Brush.Parse("#52606D") };
        activity.Bind(TextBlock.TextProperty, new Binding(nameof(DogfoodPageViewModel.Activity)));
        var primary = new Button
        {
            Content = "Run primary command",
            HorizontalAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(12, 7),
        };
        primary.Bind(Button.CommandProperty, new Binding(nameof(DogfoodPageViewModel.PrimaryCommand)));

        Content = new StackPanel
        {
            Spacing = 14,
            Children =
            {
                title,
                new TextBlock
                {
                    Text = "This view was resolved from a generated Router descriptor and committed by Presentation.",
                    Foreground = Brush.Parse("#52606D"),
                    TextWrapping = TextWrapping.Wrap,
                },
                activity,
                primary,
            },
        };
    }
}
