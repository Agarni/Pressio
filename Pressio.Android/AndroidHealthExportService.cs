using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using AndroidX.Health.Connect.Client;
using Pressio.Services;

namespace Pressio.Android;

// Integração com o Health Connect deixada de lado (a gravação via corrotinas do binding 1.1.0.4
// crasha em device). Mantida como stub (IsSupported=false) — botão oculto.
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
