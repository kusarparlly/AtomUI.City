using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using AtomUI.City.Core.DependencyInjection;
using CityLearning.Workbench.Models;
using CityLearning.Workbench.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CityLearning.Workbench.ViewModels;

[Service(ServiceLifetime.Singleton)]
public sealed class WorkbenchViewModel(WorkItemService workItemService) : INotifyPropertyChanged
{
    private WorkItem? _selectedItem;
    private string _status = "正在读取本地任务…";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WorkItem> Items { get; } = [];

    public WorkItem? SelectedItem
    {
        get => _selectedItem;
        set => SetField(ref _selectedItem, value);
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var items = await workItemService.GetAllAsync(cancellationToken);
        ReplaceItems(items);
        Status = BuildStatus();
    }

    public async Task AddAsync(string title, CancellationToken cancellationToken = default)
    {
        var item = await workItemService.AddAsync(title, cancellationToken);
        Items.Insert(0, item);
        SelectedItem = item;
        Status = BuildStatus();
    }

    public async Task CompleteSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedItem is null)
        {
            Status = "请先选择一个尚未完成的任务。";
            return;
        }

        if (!await workItemService.CompleteAsync(SelectedItem.Id, cancellationToken))
        {
            Status = "该任务已经完成，或已被删除。";
            return;
        }

        await LoadAsync(cancellationToken);
    }

    public void ReportError(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Status = $"操作失败：{exception.Message}";
    }

    private void ReplaceItems(IEnumerable<WorkItem> items)
    {
        var selectedId = SelectedItem?.Id;
        Items.Clear();
        foreach (var item in items.OrderBy(item => item.IsCompleted).ThenByDescending(item => item.CreatedAt))
        {
            Items.Add(item);
        }

        SelectedItem = Items.FirstOrDefault(item => item.Id == selectedId);
    }

    private string BuildStatus()
    {
        var completed = Items.Count(item => item.IsCompleted);
        return $"共 {Items.Count} 项 · 待办 {Items.Count - completed} 项 · 已完成 {completed} 项";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
