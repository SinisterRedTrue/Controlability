using CommunityToolkit.Mvvm.ComponentModel;
using Controlability.App.Resources;
using Controlability.Core.Models;
using Controlability.Engine;

namespace Controlability.App.ViewModels;

/// <summary>One row of the "Detected devices" list.</summary>
public sealed partial class DeviceItemViewModel : ObservableObject
{
    private readonly IEngine _engine;

    [ObservableProperty]
    private string _assignmentText = "";

    [ObservableProperty]
    private string _detailText = "";

    public DeviceItemViewModel(DeviceInfo info, IEngine engine)
    {
        _engine = engine;
        Id = info.Id;
        Name = info.Name;
        Type = info.Type;
        AssignOptions =
        [
            .. Enumerable.Range(0, 4).Select(slot => new MenuOption(
                Strings.Format(Strings.AssignToPlayer, slot + 1),
                () => _engine.Assign(Id, slot))),
        ];
        Update(info, null);
    }

    public DeviceId Id { get; }

    public string Name { get; }

    public ControllerType Type { get; }

    public List<MenuOption> AssignOptions { get; }

    /// <summary>Text for screen readers.</summary>
    public string AutomationName => $"{Name}, {DetailText}, {AssignmentText}";

    public void Update(DeviceInfo info, int? slot)
    {
        var parts = new[] { PlayerCardViewModel.TypeName(info.Type), PlayerCardViewModel.ConnectionName(info) };
        DetailText = string.Join(" - ", parts.Where(part => part.Length > 0));
        AssignmentText = slot is { } s ? Strings.Format(Strings.DeviceAssigned, s + 1) : Strings.DeviceUnassigned;
        OnPropertyChanged(nameof(AutomationName));
    }
}
