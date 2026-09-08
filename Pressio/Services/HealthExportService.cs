using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Pressio.Services;

public sealed record HealthReading(int Systolic, int Diastolic, DateTime MeasuredAt, int? HeartRate = null);

public interface IHealthExportService
{
    // true quando a plataforma tem HealthKit (iOS). Android (Health Connect) virá depois.
    bool IsSupported { get; }

    // Pede permissão ao usuário para escrever pressão arterial (HealthKit).
    Task<bool> RequestAuthorizationAsync();

    // Exporta as leituras para o app Saúde. Retorna true se todas foram enviadas.
    Task<bool> ExportAsync(IReadOnlyList<HealthReading> readings);
}

public static class HealthExport
{
    // Definido no host iOS (HealthKit). O padrão não suporta (desktop/Android).
    public static IHealthExportService Service { get; set; } = new EmptyHealthExportService();

    private sealed class EmptyHealthExportService : IHealthExportService
    {
        public bool IsSupported => false;
        public Task<bool> RequestAuthorizationAsync() => Task.FromResult(false);
        public Task<bool> ExportAsync(IReadOnlyList<HealthReading> readings) => Task.FromResult(false);
    }
}
