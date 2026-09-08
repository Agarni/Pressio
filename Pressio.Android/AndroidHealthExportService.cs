using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using AndroidX.Health.Connect.Client;
using Pressio.Services;

namespace Pressio.Android;

// Scaffold da integração com o Health Connect (sucessor do Google Fit, sem conta).
// O fluxo real (gravação) exige o app Health Connect instalado + a API de corrotinas do binding; ver TODO.
public sealed class AndroidHealthExportService : IHealthExportService
{
    private const int Unavailable = 1;

    // Desligado por enquanto (a gravação depende do app Health Connect + bridge de corrotinas).
    public bool IsSupported => false;

    public async Task<bool> RequestAuthorizationAsync()
    {
        var context = Application.Context;
        if (HealthConnectClient.GetSdkStatus(context) == Unavailable)
            return false;
        return await MainActivity.RequestHealthPermissionAsync();
    }

    public Task<bool> ExportAsync(IReadOnlyList<HealthReading> readings) => Task.FromResult(false);
}
