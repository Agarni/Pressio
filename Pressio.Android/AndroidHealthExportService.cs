using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using AndroidX.Health.Connect.Client;
using AndroidX.Health.Connect.Client.Records;
using AndroidX.Health.Connect.Client.Units;
using Java.Time;
using Pressio.Services;

namespace Pressio.Android;

// Exporta as medições de pressão para o Health Connect (sucessor do Google Fit), no próprio aparelho,
// sem conta. Exige o app Health Connect instalado e a permissão do usuário.
public sealed class AndroidHealthExportService : IHealthExportService
{
    private const int Unavailable = 1;

    public bool IsSupported => true;

    public async Task<bool> RequestAuthorizationAsync()
    {
        var context = Application.Context;
        if (HealthConnectClient.GetSdkStatus(context) == Unavailable)
            return false; // sem o app Health Connect (a VM avisa para instalar).

        return await MainActivity.RequestHealthPermissionAsync();
    }

    public async Task<bool> ExportAsync(IReadOnlyList<HealthReading> readings)
    {
        try
        {
            var client = HealthConnectClient.GetOrCreate(Application.Context);
            var records = new List<IRecord>();

            foreach (var r in readings)
            {
                var time = Instant.OfEpochMilli(new DateTimeOffset(r.MeasuredAt.ToUniversalTime()).ToUnixTimeMilliseconds());
                var sys = Pressure.InvokeMillimetersOfMercury(r.Systolic);
                var dia = Pressure.InvokeMillimetersOfMercury(r.Diastolic);
                // Metadata vazio (null) — o SDK aplica o padrão.
                records.Add(new BloodPressureRecord(time, null, null, sys, dia, 0, 0));
            }

            await client.InsertRecordsAsync(records);
            return true;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
}

// Ponte da API de corrotinas (Kotlin Continuation) para Task.
internal static class HealthConnectCoroutine
{
    public static Task InsertRecordsAsync(this IHealthConnectClient client, IList<IRecord> records)
    {
        var tcs = new TaskCompletionSource<bool>();
        client.InsertRecords(records, new Continuation(tcs));
        return tcs.Task;
    }

    private sealed class Continuation : Java.Lang.Object, Kotlin.Coroutines.IContinuation
    {
        private readonly TaskCompletionSource<bool> _tcs;
        public Continuation(TaskCompletionSource<bool> tcs) => _tcs = tcs;

        public Kotlin.Coroutines.ICoroutineContext Context => Kotlin.Coroutines.EmptyCoroutineContext.Instance;

        public void ResumeWith(Java.Lang.Object? result)
        {
            _tcs.TrySetResult(true);
        }
    }
}
