using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using AndroidX.Health.Connect.Client;
using Pressio.Services;

namespace Pressio.Android;

// Scaffold da integração com o Health Connect (sucessor do Google Fit, sem conta).
// Estado atual: dependência + permissões + launcher prontos; a gravação de registros precisa do
// controle de corrotinas (IContinuation) do binding health-connect 1.1.0.4 e de teste em device.
public sealed class AndroidHealthExportService : IHealthExportService
{
    private const int Unavailable = 1;

    // Desligado por enquanto (o fluxo real exige Health Connect instalado + o binding de escrita).
    public bool IsSupported => false;

    public async Task<bool> RequestAuthorizationAsync()
    {
        var context = Application.Context;
        if (HealthConnectClient.GetSdkStatus(context) == Unavailable)
            return false; // sem o app Health Connect.
        return await MainActivity.RequestHealthPermissionAsync();
    }

    public Task<bool> ExportAsync(IReadOnlyList<HealthReading> readings) => Task.FromResult(false);
}
