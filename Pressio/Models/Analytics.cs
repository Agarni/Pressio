namespace Pressio.Models;

public sealed record TimeSlotInfo(string Label, int Count);

public sealed record ContextCountInfo(string Label, int Count);

public sealed record ChartPointLabel(double X, double Y, string Text);
