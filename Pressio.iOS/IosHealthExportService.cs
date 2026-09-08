using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Foundation;
using HealthKit;
using Pressio.Services;

namespace Pressio.iOS;

// Exporta as medições de pressão para o app Saúde (HealthKit), sem conta de terceiros.
public sealed class IosHealthExportService : IHealthExportService
{
    private readonly HKHealthStore _store = new();
    private static readonly HKUnit MmHg = HKUnit.FromString("mmHg");
    private static readonly HKUnit Bpm = HKUnit.FromString("count/min");

    public bool IsSupported => true;

    private HKQuantityType Systolic => HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureSystolic);
    private HKQuantityType Diastolic => HKQuantityType.Create(HKQuantityTypeIdentifier.BloodPressureDiastolic);
    private HKQuantityType HeartRate => HKQuantityType.Create(HKQuantityTypeIdentifier.HeartRate);

    public Task<bool> RequestAuthorizationAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        var share = new NSSet(new NSObject[] { Systolic, Diastolic, HeartRate });
        _store.RequestAuthorizationToShare(share, null, (ok, _) => tcs.TrySetResult(ok));
        return tcs.Task;
    }

    public Task<bool> ExportAsync(IReadOnlyList<HealthReading> readings)
    {
        var tcs = new TaskCompletionSource<bool>();
        var objects = new List<HKObject>();

        foreach (var r in readings)
        {
            var start = (NSDate)r.MeasuredAt;
            var end = start;

            var sys = HKQuantity.FromQuantity(MmHg, r.Systolic);
            var dia = HKQuantity.FromQuantity(MmHg, r.Diastolic);
            var sysSample = HKQuantitySample.FromType(Systolic, sys, start, end);
            var diaSample = HKQuantitySample.FromType(Diastolic, dia, start, end);
            var corrType = HKCorrelationType.Create(HKCorrelationTypeIdentifier.BloodPressure);
            var correlation = HKCorrelation.Create(corrType, start, end, new NSSet(new NSObject[] { sysSample, diaSample }));
            objects.Add(correlation);

            if (r.HeartRate is { } hr)
                objects.Add(HKQuantitySample.FromType(HeartRate, HKQuantity.FromQuantity(Bpm, hr), start, end));
        }

        _store.SaveObjects(objects.ToArray(), (ok, _) => tcs.TrySetResult(ok));
        return tcs.Task;
    }
}
