using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.DTOs.Appointment;
using ProjetoLP.API.Models;
using ProjetoLP.API.DTOs;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AppointmentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public AppointmentsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/appointments
    [HttpGet]
    public async Task<IActionResult> GetAppointments(
        [FromQuery] AppointmentStatus? status,
        [FromQuery] DateOnly? date,
        [FromQuery] string? PatientName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.User)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(a => a.Status == status.Value);

        if (date.HasValue)
            query = query.Where(a => DateOnly.FromDateTime(a.AppointmentDate) == date.Value);

        if (!string.IsNullOrEmpty(PatientName))
            query = query.Where(a => a.Patient.Name.Contains(PatientName));

        var totalCount = await query.CountAsync();
        var appointments = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = appointments.Select(a => new AppointmentResponseDto
        {
            Id              = a.Id,
            UserId          = a.UserId,
            UserName        = a.User.Name,
            PatientId       = a.PatientId,
            PatientName     = a.Patient.Name,
            AppointmentDate = a.AppointmentDate,
            Status          = a.Status,
            CreatedAt       = a.CreatedAt,
        });

        return Ok(new PagedResult<AppointmentResponseDto>
        {
            Data = data,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    // GET /api/appointments/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetAppointment(int id)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (appointment == null)
            return NotFound(new { message = "Consulta não encontrada." });

        return Ok(new AppointmentResponseDto
        {
            Id              = appointment.Id,
            UserId          = appointment.UserId,
            UserName        = appointment.User.Name,
            AppointmentDate = appointment.AppointmentDate,
            Status          = appointment.Status,
            PatientId       = appointment.PatientId,
            PatientName     = appointment.Patient.Name,
            CreatedAt       = appointment.CreatedAt,
        });
    }

    // POST /api/appointments
    [HttpPost]
    public async Task<IActionResult> CreateAppointment(CreateAppointmentDto dto)
    {
        var user    = await _db.Users.FindAsync(dto.UserId);
        var patient = await _db.Patients.FindAsync(dto.PatientId);

        if (user == null)    return NotFound(new { message = "Usuário não encontrado." });
        if (patient == null) return NotFound(new { message = "Paciente não encontrado." });

        // Impede agendamento para paciente inativo.
        if (!patient.IsActive)
            return BadRequest(new { message = "Não é possível agendar consulta para um paciente inativo." });

        // Impede agendamento no passado.
        if (dto.AppointmentDate < DateTime.UtcNow)
            return BadRequest(new { message = "A data da consulta deve ser futura." });

        var appointment = new Appointment
        {
            UserId          = dto.UserId,
            PatientId       = dto.PatientId,
            AppointmentDate = dto.AppointmentDate,
        };

        _db.Appointments.Add(appointment);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetAppointment), new { id = appointment.Id }, appointment);
    }

    // PUT /api/appointments/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateAppointment(int id, UpdateAppointmentDto dto)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null)
            return NotFound(new { message = "Consulta não encontrada." });

        // Consulta concluída não pode voltar para Scheduled.
        if (appointment.Status == AppointmentStatus.Completed && dto.Status == AppointmentStatus.Scheduled)
            return BadRequest(new { message = "Não é possível reabrir uma consulta já concluída." });

        // Consulta cancelada não pode mudar de status.
        if (appointment.Status == AppointmentStatus.Cancelled)
            return BadRequest(new { message = "Não é possível alterar uma consulta cancelada." });

        appointment.AppointmentDate = dto.AppointmentDate;
        appointment.Status          = dto.Status;

        await _db.SaveChangesAsync();
        return Ok(appointment);
    }

    // DELETE /api/appointments/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteAppointment(int id)
    {
        var appointment = await _db.Appointments.FindAsync(id);
        if (appointment == null)
            return NotFound(new { message = "Consulta não encontrada." });

        // Impede deleção de consultas concluídas — protege o histórico clínico.
        if (appointment.Status == AppointmentStatus.Completed)
            return BadRequest(new { message = "Não é possível excluir uma consulta já concluída." });

        _db.Appointments.Remove(appointment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
