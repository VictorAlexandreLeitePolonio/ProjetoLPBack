using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.DTOs;
using ProjetoLP.API.DTOs.Financial;
using ProjetoLP.API.Models;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class FinancialController : ControllerBase
{
    private readonly AppDbContext _db;

    public FinancialController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/financial/expenses
    [HttpGet("expenses")]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] string? month,
        [FromQuery] string? title,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        // Valida formato do mês se informado.
        if (!string.IsNullOrEmpty(month) &&
            !System.Text.RegularExpressions.Regex.IsMatch(month, @"^\d{4}-\d{2}$"))
            return BadRequest(new { message = "O formato do mês deve ser 'YYYY-MM'." });

        var query = _db.Expenses.AsQueryable();

        if (!string.IsNullOrEmpty(month))
            query = query.Where(e => e.ReferenceMonth == month);

        if (!string.IsNullOrEmpty(title))
            query = query.Where(e => e.Title.Contains(title));

        var totalCount = await query.CountAsync();
        var expenses = await query
            .OrderByDescending(e => e.PaymentDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = expenses.Select(e => new ExpenseResponseDto
        {
            Id             = e.Id,
            Title          = e.Title,
            Value          = e.Value,
            PaymentDate    = e.PaymentDate,
            Description    = e.Description,
            ReferenceMonth = e.ReferenceMonth,
            CreatedAt      = e.CreatedAt,
        });

        return Ok(new PagedResult<ExpenseResponseDto>
        {
            Data       = data,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        });
    }

    // GET /api/financial/expenses/{id}
    [HttpGet("expenses/{id}")]
    public async Task<IActionResult> GetExpense(int id)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense == null)
            return NotFound(new { message = "Gasto não encontrado." });

        return Ok(new ExpenseResponseDto
        {
            Id             = expense.Id,
            Title          = expense.Title,
            Value          = expense.Value,
            PaymentDate    = expense.PaymentDate,
            Description    = expense.Description,
            ReferenceMonth = expense.ReferenceMonth,
            CreatedAt      = expense.CreatedAt,
        });
    }

    // POST /api/financial/expenses
    [HttpPost("expenses")]
    public async Task<IActionResult> CreateExpense(CreateExpenseDto dto)
    {
        // Valor do gasto deve ser positivo.
        if (dto.Value <= 0)
            return BadRequest(new { message = "O valor do gasto deve ser maior que zero." });

        // Valida formato do mês de referência.
        if (!System.Text.RegularExpressions.Regex.IsMatch(dto.ReferenceMonth, @"^\d{4}-\d{2}$"))
            return BadRequest(new { message = "O formato do mês de referência deve ser 'YYYY-MM'." });

        // Título não pode estar vazio.
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "O título do gasto é obrigatório." });

        var expense = new Expense
        {
            Title          = dto.Title,
            Value          = dto.Value,
            PaymentDate    = dto.PaymentDate,
            Description    = dto.Description,
            ReferenceMonth = dto.ReferenceMonth,
        };

        _db.Expenses.Add(expense);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetExpense), new { id = expense.Id }, new ExpenseResponseDto
        {
            Id             = expense.Id,
            Title          = expense.Title,
            Value          = expense.Value,
            PaymentDate    = expense.PaymentDate,
            Description    = expense.Description,
            ReferenceMonth = expense.ReferenceMonth,
            CreatedAt      = expense.CreatedAt,
        });
    }

    // PUT /api/financial/expenses/{id}
    [HttpPut("expenses/{id}")]
    public async Task<IActionResult> UpdateExpense(int id, UpdateExpenseDto dto)
    {
        // Valor do gasto deve ser positivo.
        if (dto.Value <= 0)
            return BadRequest(new { message = "O valor do gasto deve ser maior que zero." });

        // Valida formato do mês de referência.
        if (!System.Text.RegularExpressions.Regex.IsMatch(dto.ReferenceMonth, @"^\d{4}-\d{2}$"))
            return BadRequest(new { message = "O formato do mês de referência deve ser 'YYYY-MM'." });

        // Título não pode estar vazio.
        if (string.IsNullOrWhiteSpace(dto.Title))
            return BadRequest(new { message = "O título do gasto é obrigatório." });

        var expense = await _db.Expenses.FindAsync(id);
        if (expense == null)
            return NotFound(new { message = "Gasto não encontrado." });

        expense.Title          = dto.Title;
        expense.Value          = dto.Value;
        expense.PaymentDate    = dto.PaymentDate;
        expense.Description    = dto.Description;
        expense.ReferenceMonth = dto.ReferenceMonth;

        await _db.SaveChangesAsync();

        return Ok(new ExpenseResponseDto
        {
            Id             = expense.Id,
            Title          = expense.Title,
            Value          = expense.Value,
            PaymentDate    = expense.PaymentDate,
            Description    = expense.Description,
            ReferenceMonth = expense.ReferenceMonth,
            CreatedAt      = expense.CreatedAt,
        });
    }

    // DELETE /api/financial/expenses/{id}
    [HttpDelete("expenses/{id}")]
    public async Task<IActionResult> DeleteExpense(int id)
    {
        var expense = await _db.Expenses.FindAsync(id);
        if (expense == null)
            return NotFound(new { message = "Gasto não encontrado." });

        _db.Expenses.Remove(expense);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    // GET /api/financial/balance/history?months=6
    // Deve vir ANTES de balance/{month} para evitar conflito de rota — "history" seria interpretado como {month}.
    [HttpGet("balance/history")]
    public async Task<IActionResult> GetBalanceHistory([FromQuery] int months = 6)
    {
        // Limita o período máximo a 24 meses para evitar queries excessivas.
        if (months < 1 || months > 24)
            return BadRequest(new { message = "O período deve ser entre 1 e 24 meses." });

        var result = new List<FinancialBalanceDto>();
        var now    = DateTime.UtcNow;

        for (int i = months - 1; i >= 0; i--)
        {
            var date  = now.AddMonths(-i);
            var month = date.ToString("yyyy-MM");

            var totalExpenses = await _db.Expenses
                .Where(e => e.ReferenceMonth == month)
                .SumAsync(e => (decimal?)e.Value) ?? 0m;

            var totalIncome = await _db.Payments
                .Where(p => p.ReferenceMonth == month && p.Status == PaymentStatus.Paid)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            result.Add(new FinancialBalanceDto
            {
                ReferenceMonth = month,
                TotalExpenses  = totalExpenses,
                TotalIncome    = totalIncome,
                NetBalance     = totalIncome - totalExpenses,
            });
        }

        return Ok(result);
    }

    // GET /api/financial/balance/{month}
    [HttpGet("balance/{month}")]
    public async Task<IActionResult> GetBalance(string month)
    {
        // Valida formato do mês.
        if (!System.Text.RegularExpressions.Regex.IsMatch(month, @"^\d{4}-\d{2}$"))
            return BadRequest(new { message = "O formato do mês deve ser 'YYYY-MM'. Exemplo: 2026-03" });

        var totalExpenses = await _db.Expenses
            .Where(e => e.ReferenceMonth == month)
            .SumAsync(e => (decimal?)e.Value) ?? 0m;

        var totalIncome = await _db.Payments
            .Where(p => p.ReferenceMonth == month && p.Status == PaymentStatus.Paid)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        return Ok(new FinancialBalanceDto
        {
            ReferenceMonth = month,
            TotalExpenses  = totalExpenses,
            TotalIncome    = totalIncome,
            NetBalance     = totalIncome - totalExpenses,
        });
    }
}
