using System;

namespace Pressio.Models;

public sealed record Patient(long Id, string Name, DateTime? BirthDate = null, string? Notes = null, string? FullName = null, string? Medications = null, string? HealthDetails = null, string? DoctorName = null)
{
    public string DisplayName => Name;
    public string DisplayDetails => BirthDate is null ? "Sem data de nascimento" : $"Nasc.: {BirthDate:dd/MM/yyyy}";
}
