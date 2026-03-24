using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.DTOs.Plans;
using ProjetoLP.API.Models;
using ProjetoLP.API.DTOs;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class PlansController : ControllerBase
{
    private readonly AppDbContext _db;

    public PlansController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/plans
    // Filtros opcionais: ?tipoPlano=Mensal&tipoSessao=Pilates&isActive=true
    [HttpGet]
    public async Task<IActionResult> GetPlans(
        [FromQuery] TipoPlano? tipoPlano,
        [FromQuery] TipoSessao? tipoSessao,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _db.Plans.AsQueryable();

        if (tipoPlano.HasValue)
            query = query.Where(p => p.TipoPlano == tipoPlano.Value);

        if (tipoSessao.HasValue)
            query = query.Where(p => p.TipoSessao == tipoSessao.Value);

        // Filtra por status ativo/inativo se informado.
        if (isActive.HasValue)
            query = query.Where(p => p.IsActive == isActive.Value);

        var totalCount = await query.CountAsync();
        var plans = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = plans.Select(p => new PlanResponseDto
        {
            Id         = p.Id,
            Name       = p.Name,
            Valor      = p.Valor,
            TipoPlano  = p.TipoPlano,
            TipoSessao = p.TipoSessao,
            IsActive   = p.IsActive,
            CreatedAt  = p.CreatedAt,
        });

        return Ok(new PagedResult<PlanResponseDto>
        {
            Data       = data,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        });
    }

    // GET /api/plans/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetPlan(int id)
    {
        var plan = await _db.Plans.FindAsync(id);
        if (plan == null)
            return NotFound(new { message = "Plano não encontrado." });

        return Ok(new PlanResponseDto
        {
            Id         = plan.Id,
            Name       = plan.Name,
            Valor      = plan.Valor,
            TipoPlano  = plan.TipoPlano,
            TipoSessao = plan.TipoSessao,
            IsActive   = plan.IsActive,
            CreatedAt  = plan.CreatedAt,
        });
    }

    // POST /api/plans
    [HttpPost]
    public async Task<IActionResult> CreatePlan(CreatePlanDto dto)
    {
        if (dto.Valor <= 0)
            return BadRequest(new { message = "O valor do plano deve ser maior que zero." });

        var planExists = await _db.Plans.AnyAsync(p => p.Name == dto.Name);
        if (planExists)
            return Conflict(new { message = "Já existe um plano com este nome." });

        var plan = new Plans
        {
            Name       = dto.Name,
            Valor      = dto.Valor,
            TipoPlano  = dto.TipoPlano,
            TipoSessao = dto.TipoSessao,
        };

        _db.Plans.Add(plan);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, new PlanResponseDto
        {
            Id         = plan.Id,
            Name       = plan.Name,
            Valor      = plan.Valor,
            TipoPlano  = plan.TipoPlano,
            TipoSessao = plan.TipoSessao,
            IsActive   = plan.IsActive,
            CreatedAt  = plan.CreatedAt,
        });
    }

    // PUT /api/plans/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePlan(int id, UpdatePlanDto dto)
    {
        if (dto.Valor <= 0)
            return BadRequest(new { message = "O valor do plano deve ser maior que zero." });

        var plan = await _db.Plans.FindAsync(id);
        if (plan == null)
            return NotFound(new { message = "Plano não encontrado." });

        var nameExists = await _db.Plans.AnyAsync(p => p.Name == dto.Name && p.Id != id);
        if (nameExists)
            return Conflict(new { message = "Já existe um plano com este nome." });

        plan.Name       = dto.Name;
        plan.Valor      = dto.Valor;
        plan.TipoPlano  = dto.TipoPlano;
        plan.TipoSessao = dto.TipoSessao;

        await _db.SaveChangesAsync();

        return Ok(new PlanResponseDto
        {
            Id         = plan.Id,
            Name       = plan.Name,
            Valor      = plan.Valor,
            TipoPlano  = plan.TipoPlano,
            TipoSessao = plan.TipoSessao,
            IsActive   = plan.IsActive,
            CreatedAt  = plan.CreatedAt,
        });
    }

    // PATCH /api/plans/{id}/status — alterna ativo/inativo do plano.
    [HttpPatch("{id}/status")]
    public async Task<IActionResult> ChangeStatus(int id)
    {
        var plan = await _db.Plans.FindAsync(id);
        if (plan == null)
            return NotFound(new { message = "Plano não encontrado." });

        plan.IsActive = !plan.IsActive;
        await _db.SaveChangesAsync();

        return Ok(new { id = plan.Id, isActive = plan.IsActive });
    }

    // DELETE /api/plans/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePlan(int id)
    {
        var plan = await _db.Plans.FindAsync(id);
        if (plan == null)
            return NotFound(new { message = "Plano não encontrado." });

        var hasPayments = await _db.Payments.AnyAsync(p => p.PlanId == id);
        if (hasPayments)
            return BadRequest(new { message = "Não é possível excluir um plano com pagamentos associados." });

        _db.Plans.Remove(plan);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
