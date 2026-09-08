using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using AndroidX.Health.Connect.Client;
using Pressio.Services;

namespace Pressio.Android;

// Scaffold da integração com o Health Connect. A gravação usa corrotinas (IContinuation) + construção
// de Metadata do binding 1.1.0.4, que precisa ser validada no device. Mantido como stub (desligado)
// para não exibir um botão quebrado.
public sealed class AndroidHealthExportService : IHealthExportService
{
    private const int Unavailable = 1;

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
