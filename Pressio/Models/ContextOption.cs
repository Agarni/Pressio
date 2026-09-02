using ReactiveUI;

namespace Pressio.Models;

public sealed class ContextOption : ReactiveObject
{
    public ContextOption(MeasurementContext context, string label)
    {
        Context = context;
        Label = label;
    }

    public MeasurementContext Context { get; }
    public string Label { get; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }
}
