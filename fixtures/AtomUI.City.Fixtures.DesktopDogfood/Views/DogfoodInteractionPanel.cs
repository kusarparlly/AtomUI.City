using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AtomUI.City.Fixtures.DesktopDogfood;

internal sealed class DogfoodInteractionPanel : Border
{
    private static readonly IBrush SurfaceBrush = Brush.Parse("#FFFFFF");
    private static readonly IBrush MutedTextBrush = Brush.Parse("#52606D");
    private static readonly IBrush StrokeBrush = Brush.Parse("#D9E2EC");

    public DogfoodInteractionPanel(DogfoodInteractionViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        AutomationProperties.SetAutomationId(this, DogfoodAutomationIds.InteractionPanel);
        Background = SurfaceBrush;
        BorderBrush = StrokeBrush;
        BorderThickness = new Thickness(0, 1, 0, 0);
        Padding = new Thickness(14, 10);
        DataContext = viewModel;

        var searchQuery = Identify(new TextBox
        {
            PlaceholderText = "Order ID",
            MinHeight = 32,
            TabIndex = 0,
        }, DogfoodAutomationIds.SearchQuery);
        searchQuery.Bind(
            TextBox.TextProperty,
            new Binding(nameof(DogfoodInteractionViewModel.SearchQuery))
            {
                Mode = BindingMode.TwoWay,
            });
        searchQuery.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter && viewModel.SearchCommand.CanExecute(null))
            {
                viewModel.SearchCommand.Execute(null);
                args.Handled = true;
            }
            else if (args.Key == Key.Escape)
            {
                viewModel.SearchCommand.Cancel();
                args.Handled = true;
            }
        };

        var search = Identify(new Button
        {
            Content = "Search",
            MinHeight = 32,
            TabIndex = 1,
        }, DogfoodAutomationIds.SearchSubmit);
        search.Bind(Button.CommandProperty, new Binding(nameof(DogfoodInteractionViewModel.SearchCommand)));

        var cancel = Identify(new Button
        {
            Content = "Cancel",
            MinHeight = 32,
            TabIndex = 2,
        }, DogfoodAutomationIds.SearchCancel);
        cancel.Bind(Button.CommandProperty, new Binding(nameof(DogfoodInteractionViewModel.CancelSearchCommand)));

        var searchStatus = Identify(new TextBlock
        {
            Foreground = MutedTextBrush,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        }, DogfoodAutomationIds.SearchStatus);
        searchStatus.Bind(TextBlock.TextProperty, new Binding(nameof(DogfoodInteractionViewModel.SearchStatus)));

        var cultureSelector = Identify(new ComboBox
        {
            ItemsSource = viewModel.Cultures,
            MinHeight = 30,
            TabIndex = 3,
        }, DogfoodAutomationIds.CultureSelector);
        cultureSelector.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding(nameof(DogfoodInteractionViewModel.SelectedCulture))
            {
                Mode = BindingMode.TwoWay,
            });
        var applyCulture = Identify(new Button
        {
            Content = "Apply",
            MinHeight = 30,
            TabIndex = 4,
        }, DogfoodAutomationIds.CultureApply);
        applyCulture.Bind(Button.CommandProperty, new Binding(nameof(DogfoodInteractionViewModel.ApplyCultureCommand)));
        var culturePreview = Identify(new TextBlock
        {
            Foreground = MutedTextBrush,
            FontSize = 11,
        }, DogfoodAutomationIds.CulturePreview);
        culturePreview.Bind(TextBlock.TextProperty, new Binding(nameof(DogfoodInteractionViewModel.CulturePreview)));

        var accountSelector = Identify(new ComboBox
        {
            ItemsSource = viewModel.AccountNames,
            MinHeight = 30,
            TabIndex = 5,
        }, DogfoodAutomationIds.AccountSelector);
        accountSelector.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding(nameof(DogfoodInteractionViewModel.SelectedAccount))
            {
                Mode = BindingMode.TwoWay,
            });
        var switchAccount = Identify(new Button
        {
            Content = "Switch",
            MinHeight = 30,
            TabIndex = 6,
        }, DogfoodAutomationIds.AccountSwitch);
        switchAccount.Bind(Button.CommandProperty, new Binding(nameof(DogfoodInteractionViewModel.SwitchAccountCommand)));
        var accountStatus = Identify(new TextBlock
        {
            Foreground = MutedTextBrush,
            FontSize = 11,
        }, DogfoodAutomationIds.AccountStatus);
        accountStatus.Bind(TextBlock.TextProperty, new Binding(nameof(DogfoodInteractionViewModel.AccountStatus)));

        var scenarioSelector = Identify(new ComboBox
        {
            ItemsSource = viewModel.ScenarioNames,
            MinHeight = 30,
            TabIndex = 7,
        }, DogfoodAutomationIds.ScenarioSelector);
        scenarioSelector.Bind(
            SelectingItemsControl.SelectedItemProperty,
            new Binding(nameof(DogfoodInteractionViewModel.SelectedScenario))
            {
                Mode = BindingMode.TwoWay,
            });
        var runScenario = Identify(new Button
        {
            Content = "Run",
            MinHeight = 30,
            TabIndex = 8,
        }, DogfoodAutomationIds.ScenarioRun);
        runScenario.Bind(Button.CommandProperty, new Binding(nameof(DogfoodInteractionViewModel.RunScenarioCommand)));
        var runMatrix = Identify(new Button
        {
            Content = "Run matrix",
            MinHeight = 30,
            TabIndex = 9,
        }, DogfoodAutomationIds.ScenarioRunMatrix);
        runMatrix.Bind(Button.CommandProperty, new Binding(nameof(DogfoodInteractionViewModel.RunScenarioMatrixCommand)));
        var cancelScenario = Identify(new Button
        {
            Content = "Cancel",
            MinHeight = 30,
            TabIndex = 10,
        }, DogfoodAutomationIds.ScenarioCancel);
        cancelScenario.Bind(Button.CommandProperty, new Binding(nameof(DogfoodInteractionViewModel.CancelScenarioCommand)));
        var scenarioStatus = Identify(new TextBlock
        {
            Foreground = MutedTextBrush,
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap,
        }, DogfoodAutomationIds.ScenarioStatus);
        scenarioStatus.Bind(TextBlock.TextProperty, new Binding(nameof(DogfoodInteractionViewModel.ScenarioStatus)));
        var scenarioSummary = Identify(new TextBlock
        {
            Foreground = MutedTextBrush,
            FontSize = 10,
            TextWrapping = TextWrapping.Wrap,
        }, DogfoodAutomationIds.ScenarioSummary);
        scenarioSummary.Bind(TextBlock.TextProperty, new Binding(nameof(DogfoodInteractionViewModel.ScenarioSummary)));

        var realtime = Identify(new CheckBox
        {
            Content = "Realtime updates",
            TabIndex = 11,
        }, DogfoodAutomationIds.RealtimeToggle);
        realtime.Bind(
            ToggleButton.IsCheckedProperty,
            new Binding(nameof(DogfoodInteractionViewModel.RealtimeEnabled))
            {
                Mode = BindingMode.TwoWay,
            });

        var priority = Identify(new Slider
        {
            Minimum = 1,
            Maximum = 5,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            TabIndex = 12,
        }, DogfoodAutomationIds.PrioritySlider);
        priority.Bind(
            RangeBase.ValueProperty,
            new Binding(nameof(DogfoodInteractionViewModel.Priority))
            {
                Mode = BindingMode.TwoWay,
            });

        var searchResults = Identify(new ListBox
        {
            ItemsSource = viewModel.SearchResults,
            Height = 34,
            TabIndex = 13,
        }, DogfoodAutomationIds.SearchResults);

        var history = new StackPanel { Spacing = 2 };
        for (var index = 0; index < 48; index++)
        {
            history.Children.Add(new TextBlock
            {
                Text = $"Operator timeline entry {index + 1:00}",
                FontSize = 10,
                Foreground = MutedTextBrush,
            });
        }

        var historyScroll = Identify(new ScrollViewer
        {
            Height = 34,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = history,
            TabIndex = 14,
        }, DogfoodAutomationIds.HistoryScroll);

        var clear = Identify(new Button
        {
            Content = "Clear results",
            HorizontalAlignment = HorizontalAlignment.Left,
            MinHeight = 28,
            TabIndex = 15,
        }, DogfoodAutomationIds.SearchClear);
        clear.Bind(Button.CommandProperty, new Binding(nameof(DogfoodInteractionViewModel.ClearCommand)));

        Child = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = "Operator interaction lab", FontSize = 14, FontWeight = FontWeight.SemiBold },
                    Row("Search", searchQuery, search, cancel),
                    searchStatus,
                    Row("Culture", cultureSelector, applyCulture),
                    culturePreview,
                    Row("Account", accountSelector, switchAccount),
                    accountStatus,
                    Row("Scenario", scenarioSelector, runScenario),
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("*,*"),
                        ColumnSpacing = 5,
                        Children =
                        {
                            runMatrix,
                            Place(cancelScenario, 1),
                        },
                    },
                    scenarioStatus,
                    scenarioSummary,
                    new Grid
                    {
                        ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                        ColumnSpacing = 12,
                        Children =
                        {
                            realtime,
                            Place(priority, 1),
                        },
                    },
                    searchResults,
                    historyScroll,
                    clear,
                },
            },
        };
    }

    private static Grid Row(string label, params Control[] controls)
    {
        var controlColumns = controls.Length == 1
            ? "*"
            : "*," + string.Join(',', Enumerable.Repeat("Auto", controls.Length - 1));
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"62,{controlColumns}"),
            ColumnSpacing = 5,
        };
        grid.Children.Add(new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = MutedTextBrush,
            FontSize = 11,
        });
        for (var index = 0; index < controls.Length; index++)
        {
            Grid.SetColumn(controls[index], index + 1);
            grid.Children.Add(controls[index]);
        }

        return grid;
    }

    private static T Identify<T>(T control, string automationId) where T : Control
    {
        AutomationProperties.SetAutomationId(control, automationId);
        return control;
    }

    private static T Place<T>(T control, int column) where T : Control
    {
        Grid.SetColumn(control, column);
        return control;
    }
}
