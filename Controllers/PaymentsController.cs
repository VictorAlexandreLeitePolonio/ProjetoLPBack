using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.DTOs.Payment;
using Microsoft.AspNetCore.Authorization;
using ProjetoLP.API.DTOs;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;

    public PaymentsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/payments — retorna todos os pagamentos paginados.
    [HttpGet]
    public async Task<IActionResult> GetPayments(
        [FromQuery] int? patientId,
        [FromQuery] PaymentStatus? status,
        [FromQuery] string? referenceMonth,
        [FromQuery] string? PaidAt,
        [FromQuery] string? PatientName,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Plan)
            .AsQueryable();

        if (!string.IsNullOrEmpty(PatientName))
            query = query.Where(p => p.Patient.Name.Contains(PatientName));

        if (patientId.HasValue)
            query = query.Where(p => p.PatientId == patientId.Value);

        if (status.HasValue)
            query = query.Where(p => p.Status == status.Value);

        if (!string.IsNullOrEmpty(referenceMonth))
            query = query.Where(p => p.ReferenceMonth == referenceMonth);

        if (!string.IsNullOrEmpty(PaidAt) && DateTime.TryParse(PaidAt, out var paidAtDate))
            query = query.Where(p => p.PaidAt.HasValue && p.PaidAt.Value.Date == paidAtDate.Date);

        var totalCount = await query.CountAsync();
        var payments = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = payments.Select(p => new PaymentResponseDto
        {
            Id                  = p.Id,
            UserId              = p.UserId,
            PatientId           = p.PatientId,
            PatientName         = p.Patient.Name,
            PlanAmount          = p.Plan.Valor,
            PlanName            = p.Plan.Name,
            ReferenceMonth      = p.ReferenceMonth,
            PaymentMethod       = p.PaymentMethod,
            Status              = p.Status,
            PaidAt              = p.PaidAt,
            PaymentDate         = p.PaymentDate,
            PaymentReminderSent = p.PaymentReminderSent,
            CreatedAt           = p.CreatedAt
        });

        return Ok(new PagedResult<PaymentResponseDto>
        {
            Data = data,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        });
    }

    // GET /api/payments/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPayment(int id)
    {
        var payment = await _db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Plan)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment == null)
            return NotFound(new { message = "Pagamento não encontrado." });

        return Ok(new PaymentResponseDto
        {
            Id                  = payment.Id,
            UserId              = payment.UserId,
            PatientId           = payment.PatientId,
            PatientName         = payment.Patient.Name,
            PlanName            = payment.Plan.Name,
            PlanAmount          = payment.Plan.Valor,
            ReferenceMonth      = payment.ReferenceMonth,
            PaymentMethod       = payment.PaymentMethod,
            Status              = payment.Status,
            PaidAt              = payment.PaidAt,
            PaymentDate         = payment.PaymentDate,
            PaymentReminderSent = payment.PaymentReminderSent,
            CreatedAt           = payment.CreatedAt
        });
    }

    // POST /api/payments
    [HttpPost]
    public async Task<IActionResult> CreatePayment(CreatePaymentDto dto)
    {
        // Valida formato do mês de referência.
        if (!System.Text.RegularExpressions.Regex.IsMatch(dto.ReferenceMonth, @"^\d{4}-\d{2}$"))
            return BadRequest(new { message = "O formato do mês de referência deve ser 'YYYY-MM'." });

        // Valida que o método de pagamento não está vazio.
        if (string.IsNullOrWhiteSpace(dto.PaymentMethod))
            return BadRequest(new { message = "O método de pagamento é obrigatório." });

        var user    = await _db.Users.FindAsync(dto.UserId);
        var patient = await _db.Patients.FindAsync(dto.PatientId);
        var plan    = await _db.Plans.FindAsync(dto.PlanId);

        if (user == null)    return NotFound(new { message = "Usuário não encontrado." });
        if (patient == null) return NotFound(new { message = "Paciente não encontrado." });
        if (plan == null)    return NotFound(new { message = "Plano não encontrado." });

        // Impede cadastrar pagamento para paciente inativo.
        if (!patient.IsActive)
            return BadRequest(new { message = "Não é possível registrar pagamento para um paciente inativo." });

        // Impede duplicidade — cada paciente tem apenas um pagamento por mês.
        var monthExists = await _db.Payments.AnyAsync(p =>
            p.PatientId == dto.PatientId && p.ReferenceMonth == dto.ReferenceMonth);
        if (monthExists)
            return Conflict(new { message = "Já existe um pagamento para este paciente neste mês." });

        var payment = new Payment
        {
            UserId         = dto.UserId,
            PatientId      = dto.PatientId,
            PlanId         = dto.PlanId,
            Amount         = plan.Valor, // Valor sempre vem do plano.
            ReferenceMonth = dto.ReferenceMonth,
            PaymentMethod  = dto.PaymentMethod,
            PaymentDate    = dto.PaymentDate,
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id }, payment);
    }

    // PUT /api/payments/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePayment(int id, UpdatePaymentDto dto)
    {
        // Valida formato do mês de referência.
        if (!System.Text.RegularExpressions.Regex.IsMatch(dto.ReferenceMonth, @"^\d{4}-\d{2}$"))
            return BadRequest(new { message = "O formato do mês de referência deve ser 'YYYY-MM'." });

        // Valida que o método de pagamento não está vazio.
        if (string.IsNullOrWhiteSpace(dto.PaymentMethod))
            return BadRequest(new { message = "O método de pagamento é obrigatório." });

        var payment = await _db.Payments.FindAsync(id);
        if (payment == null)
            return NotFound(new { message = "Pagamento não encontrado." });

        // Se o plano mudou, valida existência e atualiza o Amount.
        if (dto.PlanId != payment.PlanId)
        {
            var plan = await _db.Plans.FindAsync(dto.PlanId);
            if (plan == null)
                return NotFound(new { message = "Plano não encontrado." });
            payment.PlanId = dto.PlanId;
            payment.Amount = plan.Valor;
        }

        payment.ReferenceMonth = dto.ReferenceMonth;
        payment.PaymentMethod  = dto.PaymentMethod;
        payment.Status         = dto.Status;
        payment.PaymentDate    = dto.PaymentDate;

        // Gerencia PaidAt automaticamente: preenche ao confirmar, limpa ao cancelar/pendente.
        payment.PaidAt = dto.Status == PaymentStatus.Paid
            ? (dto.PaidAt ?? DateTime.UtcNow)
            : null;

        // Se a data de vencimento mudou, reseta o flag de lembrete para permitir novo envio.
        if (dto.PaymentDate != payment.PaymentDate)
            payment.PaymentReminderSent = false;

        await _db.SaveChangesAsync();

        var updated = await _db.Payments
            .Include(p => p.Patient)
            .Include(p => p.Plan)
            .FirstOrDefaultAsync(p => p.Id == id);

        return Ok(new PaymentResponseDto
        {
            Id                  = updated!.Id,
            UserId              = updated.UserId,
            PatientId           = updated.PatientId,
            PatientName         = updated.Patient.Name,
            PlanName            = updated.Plan.Name,
            PlanAmount          = updated.Plan.Valor,
            ReferenceMonth      = updated.ReferenceMonth,
            PaymentMethod       = updated.PaymentMethod,
            Status              = updated.Status,
            PaidAt              = updated.PaidAt,
            PaymentDate         = updated.PaymentDate,
            PaymentReminderSent = updated.PaymentReminderSent,
            CreatedAt           = updated.CreatedAt
        });
    }

    // DELETE /api/payments/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePayment(int id)
    {
        var payment = await _db.Payments.FindAsync(id);
        if (payment == null)
            return NotFound(new { message = "Pagamento não encontrado." });

        // Impede deleção de pagamentos já confirmados — protege o histórico financeiro.
        if (payment.Status == PaymentStatus.Paid)
            return BadRequest(new { message = "Não é possível excluir um pagamento já confirmado. Cancele-o antes de excluir." });

        _db.Payments.Remove(payment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
