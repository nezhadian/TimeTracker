using CommunityToolkit.Mvvm.ComponentModel;

namespace TimeTracker.ViewModels;

public sealed partial class HistoryDayItem : ObservableObject
{
    public HistoryDayItem(DateOnly date)
    {
        Date = date;
    }

    public DateOnly Date { get; }

    [ObservableProperty]
    private string _title = "";

    [ObservableProperty]
    private string _subtitle = "";

    [ObservableProperty]
    private string _badge = "";

    public bool HasBadge => !string.IsNullOrWhiteSpace(Badge);

    partial void OnBadgeChanged(string value) => OnPropertyChanged(nameof(HasBadge));
}
