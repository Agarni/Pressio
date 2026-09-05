using System;

namespace Pressio.Models;

public enum PressureCategory
{
    Optimal,
    Normal,
    Elevated,
    Stage1,
    Stage2,
    Stage3
}

// Classificação de faixas pela 7ª Diretriz Brasileira de Hipertensão (SBC 2020).
// Obtida pela pior faixa entre pressão sistólica e diastólica.
public static class BloodPressureClassification
{
    public static PressureCategory Classify(int systolic, int diastolic)
    {
        if (systolic >= 180 || diastolic >= 110) return PressureCategory.Stage3;
        if (systolic >= 160 || diastolic >= 100) return PressureCategory.Stage2;
        if (systolic >= 140 || diastolic >= 90) return PressureCategory.Stage1;
        if (systolic >= 130 || diastolic >= 85) return PressureCategory.Elevated;
        if (systolic >= 120 || diastolic >= 80) return PressureCategory.Normal;
        return PressureCategory.Optimal;
    }

    public static string Label(PressureCategory category) => category switch
    {
        PressureCategory.Optimal => "Ótima",
        PressureCategory.Normal => "Normal",
        PressureCategory.Elevated => "Elevada",
        PressureCategory.Stage1 => "Hipertensão 1",
        PressureCategory.Stage2 => "Hipertensão 2",
        PressureCategory.Stage3 => "Hipertensão 3",
        _ => "—"
    };

    public static string Color(PressureCategory category) => category switch
    {
        PressureCategory.Optimal => "#2E9E6B",
        PressureCategory.Normal => "#3FAF83",
        PressureCategory.Elevated => "#D9A62E",
        PressureCategory.Stage1 => "#E0842E",
        PressureCategory.Stage2 => "#D64545",
        PressureCategory.Stage3 => "#A83232",
        _ => "#73799B"
    };
}
