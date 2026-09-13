using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AtomUI.City.Presentation;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodWindowFactory
{
    private readonly DogfoodInteractionViewModel _interactionViewModel;

    public DogfoodWindowFactory(DogfoodInteractionViewModel interactionViewModel)
    {
        _interactionViewModel = interactionViewModel;
    }

    public DogfoodMainWindow CreateMainWindow() => new(_interactionViewModel);

    public DogfoodAuxiliaryWindow CreateAuxiliaryWindow(
        string windowId,
        string title,
        string description,
        IReadOnlyList<string> outletNames) =>
        new(windowId, title, description, outletNames);
}

internal sealed class DogfoodAuxiliaryWindow : Window
{
    private static readonly IBrush CanvasBrush = Brush.Parse("#F4F6F8");
    private static readonly IBrush SurfaceBrush = Brush.Parse("#FFFFFF");
    private static readonly IBrush MutedTextBrush = Brush.Parse("#52606D");
    private static readonly IBrush StrokeBrush = Brush.Parse("#D9E2EC");
    private static readonly IBrush AccentBrush = Brush.Parse("#087F5B");

    public DogfoodAuxiliaryWindow(
        string windowId,
        string title,
        string description,
        IReadOnlyList<string> outletNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(windowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentNullException.ThrowIfNull(outletNames);
        if (outletNames.Count == 0 ||
            outletNames.Any(string.IsNullOrWhiteSpace) ||
            outletNames.Distinct(StringComparer.Ordinal).Count() != outletNames.Count)
        {
            throw new ArgumentException("Auxiliary Window outlet names must be non-empty and unique.", nameof(outletNames));
        }

        WindowId = windowId;
        OutletNames = Array.AsReadOnly(outletNames.ToArray());
        Outlets = OutletNames.ToDictionary(
            static name => name,
            CreateOutlet,
            StringComparer.Ordinal);
        Title = title;
        Width = 760;
        Height = 520;
        MinWidth = 560;
        MinHeight = 380;
        Background = CanvasBrush;

        Content = new Grid
        {
            RowDefinitions = new RowDefinitions("72,*"),
            Children =
            {
                new Border
                {
                    Background = SurfaceBrush,
                    BorderBrush = StrokeBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(22, 12),
                    Child = new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                        Children =
                        {
                            new StackPanel
                            {
                                Spacing = 2,
                                Children =
                                {
                                    new TextBlock { Text = title, FontSize = 18, FontWeight = FontWeight.SemiBold },
                                    new TextBlock { Text = description, FontSize = 12, Foreground = MutedTextBrush },
                                },
                            },
                            Place(new TextBlock
                            {
                                Text = windowId.ToUpperInvariant(),
                                FontSize = 11,
                                FontWeight = FontWeight.Bold,
                                Foreground = AccentBrush,
                                VerticalAlignment = VerticalAlignment.Center,
                            }, 1),
                        },
                    },
                },
                PlaceInRow(new Border
                {
                    Padding = new Thickness(22),
                    Child = CreateOutletGrid(),
                }, 1),
            },
        };
    }

    public string WindowId { get; }

    public IReadOnlyList<string> OutletNames { get; }

    public IReadOnlyDictionary<string, ContentControl> Outlets { get; }

    public Control CreateWorkspaceContent(
        string outletName,
        string headline,
        params (string Name, string Value)[] metrics)
    {
        var panel = new StackPanel { Spacing = 14 };
        panel.Children.Add(new TextBlock
        {
            Text = headline,
            FontSize = 20,
            FontWeight = FontWeight.SemiBold,
        });
        panel.Children.Add(new TextBlock
        {
            Text = $"Managed by WindowSession '{WindowId}' and outlet '{outletName}'.",
            Foreground = MutedTextBrush,
            TextWrapping = TextWrapping.Wrap,
        });

        foreach (var (name, value) in metrics)
        {
            panel.Children.Add(new Border
            {
                Background = SurfaceBrush,
                BorderBrush = StrokeBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(14, 10),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                    Children =
                    {
                        new TextBlock { Text = name, Foreground = MutedTextBrush },
                        Place(new TextBlock { Text = value, FontWeight = FontWeight.SemiBold }, 1),
                    },
                },
            });
        }

        return panel;
    }

    private Control CreateOutletGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(
                string.Join(',', Enumerable.Repeat("*", OutletNames.Count))),
        };
        for (var index = 0; index < OutletNames.Count; index++)
        {
            var border = new Border
            {
                BorderBrush = StrokeBrush,
                BorderThickness = new Thickness(index == 0 ? 0 : 1, 0, 0, 0),
                Padding = new Thickness(
                    index == 0 ? 0 : 18,
                    0,
                    index == OutletNames.Count - 1 ? 0 : 18,
                    0),
                Child = Outlets[OutletNames[index]],
            };
            Grid.SetColumn(border, index);
            grid.Children.Add(border);
        }

        return grid;
    }

    private static ContentControl CreateOutlet(string name)
    {
        var outlet = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        RouteOutletProperties.SetName(outlet, name);
        return outlet;
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private static T PlaceInRow<T>(T control, int row) where T : Control
    {
        Grid.SetRow(control, row);
        return control;
    }
}

internal sealed class DogfoodMainWindow : Window
{
    private static readonly IBrush CanvasBrush = Brush.Parse("#F4F6F8");
    private static readonly IBrush SurfaceBrush = Brush.Parse("#FFFFFF");
    private static readonly IBrush NavigationBrush = Brush.Parse("#202A33");
    private static readonly IBrush NavigationTextBrush = Brush.Parse("#F8FAFC");
    private static readonly IBrush MutedTextBrush = Brush.Parse("#52606D");
    private static readonly IBrush StrokeBrush = Brush.Parse("#D9E2EC");
    private static readonly IBrush AccentBrush = Brush.Parse("#087F5B");
    private static readonly IBrush ErrorBrush = Brush.Parse("#C92A2A");

    private readonly TextBlock _statusTitle;
    private readonly TextBlock _statusDetail;
    private readonly Dictionary<string, Button> _navigationButtons = new(StringComparer.Ordinal);

    public DogfoodMainWindow(DogfoodInteractionViewModel interactionViewModel)
    {
        ArgumentNullException.ThrowIfNull(interactionViewModel);
        AutomationProperties.SetAutomationId(this, DogfoodAutomationIds.MainWindow);
        Title = "AtomUI.City Operations Workbench";
        Width = 1440;
        Height = 900;
        MinWidth = 1080;
        MinHeight = 700;
        Background = CanvasBrush;

        NavigationOutlet = CreateOutlet("navigation");
        PrimaryOutlet = CreateOutlet("primary");
        InspectorOutlet = CreateOutlet("inspector");
        ActivityOutlet = CreateOutlet("activity");

        _statusTitle = new TextBlock
        {
            Text = "Starting",
            FontWeight = FontWeight.SemiBold,
            Foreground = AccentBrush,
        };
        AutomationProperties.SetAutomationId(_statusTitle, DogfoodAutomationIds.StatusTitle);
        _statusDetail = new TextBlock
        {
            Text = "Attaching City Presentation runtime",
            Foreground = MutedTextBrush,
            TextWrapping = TextWrapping.Wrap,
        };
        AutomationProperties.SetAutomationId(_statusDetail, DogfoodAutomationIds.StatusDetail);

        InteractionPanel = new DogfoodInteractionPanel(interactionViewModel);
        interactionViewModel.AttachNavigationProjection(ApplyLocalizedNavigation);

        Content = CreateLayout();
    }

    public ContentControl NavigationOutlet { get; }

    public ContentControl PrimaryOutlet { get; }

    public ContentControl InspectorOutlet { get; }

    public ContentControl ActivityOutlet { get; }

    public DogfoodInteractionPanel InteractionPanel { get; }

    public void WireNavigation(Func<string, Task> navigateAsync)
    {
        ArgumentNullException.ThrowIfNull(navigateAsync);
        foreach (var (section, button) in _navigationButtons)
        {
            button.Click += async (_, _) => await navigateAsync(section).ConfigureAwait(true);
        }
    }

    public void ApplyLocalizedNavigation(IReadOnlyDictionary<string, string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        foreach (var (section, button) in _navigationButtons)
        {
            if (labels.TryGetValue(section, out var label))
            {
                button.Content = label;
            }
        }
    }

    public void SetStatus(string title, string detail, bool isError = false)
    {
        _statusTitle.Text = title;
        _statusTitle.Foreground = isError ? ErrorBrush : AccentBrush;
        _statusDetail.Text = detail;
    }

    public Control CreateNavigationContent()
    {
        var panel = new StackPanel
        {
            Spacing = 6,
            Margin = new Thickness(16, 18),
        };
        panel.Children.Add(new TextBlock
        {
            Text = "WORKSPACES",
            Foreground = Brush.Parse("#9FB3C8"),
            FontSize = 12,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(8, 0, 8, 8),
        });

        foreach (var section in new[] { "Dashboard", "Commerce", "Fulfillment", "Customers", "Operations", "Administration" })
        {
            var button = new Button
            {
                Content = section,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                MinHeight = 40,
                Padding = new Thickness(12, 8),
                Background = Brushes.Transparent,
                Foreground = NavigationTextBrush,
                BorderThickness = new Thickness(0),
            };
            AutomationProperties.SetAutomationId(button, DogfoodAutomationIds.Navigation(section));
            _navigationButtons.Add(section, button);
            panel.Children.Add(button);
        }

        return panel;
    }

    public static Control CreateInspectorContent() => new StackPanel
    {
        Spacing = 12,
        Margin = new Thickness(18),
        Children =
        {
            new TextBlock { Text = "Runtime inspector", FontSize = 16, FontWeight = FontWeight.SemiBold },
            Metric("Host", "Running"),
            Metric("Presentation", "Ready"),
            Metric("Window session", "main"),
            Metric("Registered modules", "48"),
            Metric("Resolved services", "110"),
        },
    };

    public static Control CreateActivityContent() => new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
        Children =
        {
            Cell("LIVE", 0, FontWeight.Bold, AccentBrush),
            Cell("Startup invariants and Presentation commit completed", 1, FontWeight.Normal, MutedTextBrush),
            Cell(DateTimeOffset.Now.ToString("HH:mm:ss"), 2, FontWeight.Normal, MutedTextBrush),
        },
    };

    private Control CreateLayout()
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("64,*,80"),
            Background = CanvasBrush,
        };

        var header = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = StrokeBrush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(24, 0),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Children =
                {
                    new StackPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Spacing = 2,
                        Children =
                        {
                            new TextBlock { Text = "City Operations Workbench", FontSize = 20, FontWeight = FontWeight.SemiBold },
                            new TextBlock { Text = "Desktop framework integration dogfood", FontSize = 12, Foreground = MutedTextBrush },
                        },
                    },
                    Place(new Border
                    {
                        Background = Brush.Parse("#E6FCF5"),
                        BorderBrush = Brush.Parse("#96F2D7"),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(4),
                        Padding = new Thickness(10, 6),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock { Text = "DOGFOOD", FontSize = 12, FontWeight = FontWeight.Bold, Foreground = AccentBrush },
                    }, column: 1),
                },
            },
        };
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var body = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("224,*,300"),
        };
        Grid.SetRow(body, 1);
        body.Children.Add(new Border { Background = NavigationBrush, Child = NavigationOutlet });
        body.Children.Add(Place(new Border
        {
            Padding = new Thickness(24),
            Child = PrimaryOutlet,
        }, column: 1));
        var inspectorColumn = new Grid
        {
            RowDefinitions = new RowDefinitions("*,500"),
            Children =
            {
                new Border
                {
                    Background = SurfaceBrush,
                    BorderBrush = StrokeBrush,
                    BorderThickness = new Thickness(1, 0, 0, 0),
                    Child = InspectorOutlet,
                },
                PlaceInRow(InteractionPanel, 1),
            },
        };
        body.Children.Add(Place(inspectorColumn, column: 2));
        root.Children.Add(body);

        var footer = new Border
        {
            Background = SurfaceBrush,
            BorderBrush = StrokeBrush,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 10),
            Child = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("280,*"),
                Children =
                {
                    new StackPanel { Spacing = 2, Children = { _statusTitle, _statusDetail } },
                    Place(ActivityOutlet, column: 1),
                },
            },
        };
        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        return root;
    }

    private static ContentControl CreateOutlet(string name)
    {
        var outlet = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
        };
        RouteOutletProperties.SetName(outlet, name);
        return outlet;
    }

    private static Control Metric(string name, string value) => new Grid
    {
        ColumnDefinitions = new ColumnDefinitions("*,Auto"),
        Children =
        {
            new TextBlock { Text = name, Foreground = MutedTextBrush },
            Place(new TextBlock { Text = value, FontWeight = FontWeight.SemiBold }, column: 1),
        },
    };

    private static TextBlock Cell(string text, int column, FontWeight weight, IBrush foreground)
    {
        var cell = new TextBlock
        {
            Text = text,
            FontSize = 12,
            FontWeight = weight,
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(column == 0 ? 0 : 14, 0),
            TextWrapping = TextWrapping.Wrap,
        };
        Grid.SetColumn(cell, column);
        return cell;
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }

    private static T PlaceInRow<T>(T control, int row) where T : Control
    {
        Grid.SetRow(control, row);
        return control;
    }
}
