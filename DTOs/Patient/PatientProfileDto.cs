namespace ProjetoLP.API.DTOs.Patient;

using ProjetoLP.API.Models;

// Retorno da rota GET /api/patients/{id}/profile
// Agrega dados do paciente + histórico de consultas, prontuários e pagamentos em uma única chamada.
public class PatientProfileDto
{
    // ── Dados cadastrais ────────────────────────────────────────────────────
    public int    Id       { get; set; }
    public string Name     { get; set; } = string.Empty;
    public string Email    { get; set; } = string.Empty;
    public string CPF      { get; set; } = string.Empty;
    public string Rg       { get; set; } = string.Empty;
    public string Phone    { get; set; } = string.Empty;
    public string Rua      { get; set; } = string.Empty;
    public string Numero   { get; set; } = string.Empty;
    public string Bairro   { get; set; } = string.Empty;
    public string Cidade   { get; set; } = string.Empty;
    public string Estado   { get; set; } = string.Empty;
    public string Cep      { get; set; } = string.Empty;
    public bool   IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // ── Históricos ──────────────────────────────────────────────────────────
    public List<AppointmentSummary>   Appointments   { get; set; } = [];
    public List<MedicalRecordSummary> MedicalRecords { get; set; } = [];
    public List<PaymentSummary>       Payments       { get; set; } = [];
}

public class AppointmentSummary
{
    public int               Id              { get; set; }
    public DateTime          AppointmentDate { get; set; }
    public AppointmentStatus Status          { get; set; }
    public string            UserName        { get; set; } = string.Empty;
    public DateTime          CreatedAt       { get; set; }
}

public class MedicalRecordSummary
{
    public int      Id        { get; set; }
    public string   Titulo    { get; set; } = string.Empty;
    public string   Sessao    { get; set; } = string.Empty;
    public string   Patologia { get; set; } = string.Empty;
    public string   UserName  { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class PaymentSummary
{
    public int           Id                  { get; set; }
    public string        ReferenceMonth      { get; set; } = string.Empty;
    public string        PlanName            { get; set; } = string.Empty;
    public decimal       Amount              { get; set; }
    public string        PaymentMethod       { get; set; } = string.Empty;
    public PaymentStatus Status              { get; set; }
    public DateTime?     PaymentDate         { get; set; }
    public DateTime?     PaidAt              { get; set; }
    public bool          PaymentReminderSent { get; set; }
    public DateTime      CreatedAt           { get; set; }
}
