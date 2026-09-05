using Avalonia;

namespace Pressio.Models;

public sealed record TimeSlotInfo(string Label, int Count, string AverageText);

public sealed record ContextCountInfo(string Label, int Count);

// Diferença da média de pressão (sistólica/diastólica) nos registros com um fator vs sem ele.
public sealed record CorrelationInfo(string Label, string Delta, string Detail, bool Raises, int DeltaSys, int DeltaDia);

public sealed record ChartPointLabel(string Text, int X, int Y)
{
    public Thickness Offset => new(X, Y, 0, 0);
}

public sealed record ExportFileRequest(string FileName, string Extension, string Kind, string? StartDirectory);
