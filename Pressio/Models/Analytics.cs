using Avalonia;

namespace Pressio.Models;

public sealed record TimeSlotInfo(string Label, int Count);

public sealed record ContextCountInfo(string Label, int Count);

public sealed record ChartPointLabel(string Text, int X, int Y)
{
    public Thickness Offset => new(X, Y, 0, 0);
}

public sealed record ExportFileRequest(string FileName, string Extension, string Kind, string? StartDirectory);
