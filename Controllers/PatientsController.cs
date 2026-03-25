using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.DTOs.Patient;
using ProjetoLP.API.DTOs;
using Microsoft.AspNetCore.Authorization;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class PatientsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PatientsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/patients
    // Filtros opcionais: ?name=Maria&isActive=true&appointmentStatus=Scheduled&paymentStatus=Pending
    [HttpGet]
    public async Task<IActionResult> GetPatients(
        [FromQuery] string? name,
        [FromQuery] bool? isActive,
        [FromQuery] AppointmentStatus? appointmentStatus,
        [FromQuery] PaymentStatus? paymentStatus,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _db.Patients
            .Include(u => u.Appointments)
            .Include(u => u.Payments)
            .Include(u => u.MedicalRecords)
            .AsQueryable();

        if (!string.IsNullOrEmpty(name))
            query = query.Where(u => u.Name.Contains(name));

        // Filtra por status ativo/inativo se informado.
        if (isActive.HasValue)
            query = query.Where(u => u.IsActive == isActive.Value);

        if (appointmentStatus.HasValue)
            query = query.Where(u => u.Appointments.Any(a => a.Status == appointmentStatus.Value));

        if (paymentStatus.HasValue)
            query = query.Where(u => u.Payments.Any(p => p.Status == paymentStatus.Value));

        var totalCount = await query.CountAsync();

        var patients = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = patients.Select(u => new PatientResponseDto
        {
            Id                = u.Id,
            Name              = u.Name,
            Email             = u.Email,
            CPF               = u.CPF,
            Rg                = u.Rg,
            Rua               = u.Rua,
            Numero            = u.Numero,
            Bairro            = u.Bairro,
            Cidade            = u.Cidade,
            Estado            = u.Estado,
            Cep               = u.Cep,
            Phone             = u.Phone,
            IsActive          = u.IsActive,
            appointmentStatus = u.Appointments.OrderByDescending(a => a.AppointmentDate).FirstOrDefault()?.Status ?? AppointmentStatus.Scheduled,
            paymentStatus     = u.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault()?.Status ?? PaymentStatus.Pending,
            CreatedAt         = u.CreatedAt,
        });

        return Ok(new PagedResult<PatientResponseDto>
        {
            Data       = data,
            Page       = page,
            PageSize   = pageSize,
            TotalCount = totalCount,
        });
    }

    // GET /api/patients/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPatient(int id)
    {
        var patient = await _db.Patients
            .Include(p => p.Appointments)
            .Include(p => p.Payments)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (patient == null)
            return NotFound(new { message = "Paciente não encontrado." });

        return Ok(new PatientResponseDto
        {
            Id                = patient.Id,
            Name              = patient.Name,
            Email             = patient.Email,
            CPF               = patient.CPF,
            Rg                = patient.Rg,
            Rua               = patient.Rua,
            Numero            = patient.Numero,
            Bairro            = patient.Bairro,
            Cidade            = patient.Cidade,
            Estado            = patient.Estado,
            Cep               = patient.Cep,
            Phone             = patient.Phone,
            CreatedAt         = patient.CreatedAt,
            IsActive          = patient.IsActive,
            appointmentStatus = patient.Appointments.OrderByDescending(a => a.AppointmentDate).FirstOrDefault()?.Status ?? AppointmentStatus.Scheduled,
            paymentStatus     = patient.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault()?.Status ?? PaymentStatus.Pending,
        });
    }

    // GET /api/patients/{id}/profile
    // Retorna dados completos do paciente + histórico de consultas, prontuários e pagamentos.
    [HttpGet("{id}/profile")]
    public async Task<IActionResult> GetPatientProfile(int id)
    {
        var patient = await _db.Patients
            .Include(p => p.Appointments).ThenInclude(a => a.User)
            .Include(p => p.MedicalRecords).ThenInclude(m => m.User)
            .Include(p => p.Payments).ThenInclude(p => p.Plan)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (patient == null)
            return NotFound(new { message = "Paciente não encontrado." });

        var profile = new PatientProfileDto
        {
            Id        = patient.Id,
            Name      = patient.Name,
            Email     = patient.Email,
            CPF       = patient.CPF,
            Rg        = patient.Rg,
            Phone     = patient.Phone,
            Rua       = patient.Rua,
            Numero    = patient.Numero,
            Bairro    = patient.Bairro,
            Cidade    = patient.Cidade,
            Estado    = patient.Estado,
            Cep       = patient.Cep,
            IsActive  = patient.IsActive,
            CreatedAt = patient.CreatedAt,

            Appointments = patient.Appointments
                .OrderByDescending(a => a.AppointmentDate)
                .Select(a => new AppointmentSummary
                {
                    Id              = a.Id,
                    AppointmentDate = a.AppointmentDate,
                    Status          = a.Status,
                    UserName        = a.User.Name,
                    CreatedAt       = a.CreatedAt,
                }).ToList(),

            MedicalRecords = patient.MedicalRecords
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => new MedicalRecordSummary
                {
                    Id        = m.Id,
                    Titulo    = m.Titulo,
                    Sessao    = m.Sessao,
                    Patologia = m.Patologia,
                    UserName  = m.User.Name,
                    CreatedAt = m.CreatedAt,
                }).ToList(),

            Payments = patient.Payments
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PaymentSummary
                {
                    Id                  = p.Id,
                    ReferenceMonth      = p.ReferenceMonth,
                    PlanName            = p.Plan.Name,
                    Amount              = p.Amount,
                    PaymentMethod       = p.PaymentMethod,
                    Status              = p.Status,
                    PaymentDate         = p.PaymentDate,
                    PaidAt              = p.PaidAt,
                    PaymentReminderSent = p.PaymentReminderSent,
                    CreatedAt           = p.CreatedAt,
                }).ToList(),
        };

        return Ok(profile);
    }

    // POST /api/patients
    [HttpPost]
    public async Task<IActionResult> CreatePatient(CreatePatientDto dto)
    {
        var emailExists = await _db.Patients.AnyAsync(p => p.Email == dto.Email);
        if (emailExists)
            return Conflict(new { message = "Email já cadastrado por outro paciente." });

        var cpfExists = await _db.Patients.AnyAsync(p => p.CPF == dto.CPF);
        if (cpfExists)
            return Conflict(new { message = "CPF já cadastrado por outro paciente." });

        var patient = new Patient
        {
            Name   = dto.Name,
            Email  = dto.Email,
            CPF    = dto.CPF,
            Rg     = dto.Rg,
            Rua    = dto.Rua,
            Numero = dto.Numero,
            Bairro = dto.Bairro,
            Cidade = dto.Cidade,
            Estado = dto.Estado,
            Cep    = dto.Cep,
            Phone  = dto.Phone,
        };

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPatient), new { id = patient.Id }, patient);
    }

    // PUT /api/patients/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePatient(int id, UpdatePatientDto dto)
    {
        var emailExists = await _db.Patients.AnyAsync(p => p.Email == dto.Email && p.Id != id);
        if (emailExists)
            return Conflict(new { message = "Email já cadastrado por outro paciente." });

        var cpfExists = await _db.Patients.AnyAsync(p => p.CPF == dto.CPF && p.Id != id);
        if (cpfExists)
            return Conflict(new { message = "CPF já cadastrado por outro paciente." });

        var patient = await _db.Patients.FindAsync(id);
        if (patient == null)
            return NotFound(new { message = "Paciente não encontrado." });

        patient.Name   = dto.Name;
        patient.Email  = dto.Email;
        patient.CPF    = dto.CPF;
        patient.Rg     = dto.Rg;
        patient.Rua    = dto.Rua;
        patient.Numero = dto.Numero;
        patient.Bairro = dto.Bairro;
        patient.Cidade = dto.Cidade;
        patient.Estado = dto.Estado;
        patient.Cep    = dto.Cep;
        patient.Phone  = dto.Phone;

        await _db.SaveChangesAsync();
        return Ok(new { message = "Paciente atualizado com sucesso." });
    }

    // PATCH /api/patients/{id}/status
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> ChangeStatus(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null)
            return NotFound(new { message = "Paciente não encontrado." });

        patient.IsActive = !patient.IsActive;
        await _db.SaveChangesAsync();
        return Ok(new { id = patient.Id, isActive = patient.IsActive });
    }

    // DELETE /api/patients/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePatient(int id)
    {
        var patient = await _db.Patients.FindAsync(id);
        if (patient == null)
            return NotFound(new { message = "Paciente não encontrado." });

        var hasRecords = await _db.Appointments.AnyAsync(a => a.PatientId == id)
                      || await _db.Payments.AnyAsync(p => p.PatientId == id);
        if (hasRecords)
            return Conflict(new { message = "Não é possível excluir paciente com agendamentos ou pagamentos associados." });

        _db.Patients.Remove(patient);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
