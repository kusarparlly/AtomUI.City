namespace AtomUI.City.Fixtures.DesktopDogfood;

internal static class DogfoodAutomationIds
{
    public const string MainWindow = "dogfood-main-window";
    public const string StatusTitle = "dogfood-status-title";
    public const string StatusDetail = "dogfood-status-detail";
    public const string InteractionPanel = "dogfood-interaction-panel";
    public const string SearchQuery = "dogfood-search-query";
    public const string SearchSubmit = "dogfood-search-submit";
    public const string SearchCancel = "dogfood-search-cancel";
    public const string SearchClear = "dogfood-search-clear";
    public const string SearchStatus = "dogfood-search-status";
    public const string SearchResults = "dogfood-search-results";
    public const string CultureSelector = "dogfood-culture-selector";
    public const string CultureApply = "dogfood-culture-apply";
    public const string CulturePreview = "dogfood-culture-preview";
    public const string AccountSelector = "dogfood-account-selector";
    public const string AccountSwitch = "dogfood-account-switch";
    public const string AccountStatus = "dogfood-account-status";
    public const string ScenarioSelector = "dogfood-scenario-selector";
    public const string ScenarioRun = "dogfood-scenario-run";
    public const string ScenarioRunMatrix = "dogfood-scenario-run-matrix";
    public const string ScenarioCancel = "dogfood-scenario-cancel";
    public const string ScenarioStatus = "dogfood-scenario-status";
    public const string ScenarioSummary = "dogfood-scenario-summary";
    public const string RealtimeToggle = "dogfood-realtime-toggle";
    public const string PrioritySlider = "dogfood-priority-slider";
    public const string HistoryScroll = "dogfood-history-scroll";

    public static string Navigation(string section) => $"dogfood-navigation-{section.ToLowerInvariant()}";
}
