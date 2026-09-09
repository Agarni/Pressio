using Avalonia;

namespace Pressio.Models;

public sealed record TimeSlotInfo(string Label, int Count, string AverageText);

public sealed record ContextCountInfo(string Label, int Count);

// Diferença da média de pressão (sistólica/diastólica) nas horas seguintes a um fator,
// em relação a leituras sem o fator recente. IsTrend indica amostragem pequena (apenas um indício).
public sealed record CorrelationInfo(string Label, string Delta, string Detail, bool Raises, int DeltaSys, int DeltaDia, bool IsTrend);

public sealed record ChartPointLabel(string Text, int X, int Y)
{
    public Thickness Offset => new(X, Y, 0, 0);
}

// Ponto do gráfico colorido pela classificação (faixa) da leitura.
public sealed record ChartPointMarker(int X, int Y, PressureCategory Category)
{
    public Thickness Offset => new(X - 4, Y - 4, 0, 0);
}

// Linha-guia tracejada ligando a etiqueta de valor ao ponto correspondente (mesma aferição).
public sealed record ChartLeaderLine(Point Start, Point End);

public sealed record ExportFileRequest(string FileName, string Extension, string Kind, string? StartDirectory);
