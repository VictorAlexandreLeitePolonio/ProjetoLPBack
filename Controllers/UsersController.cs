using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.DTOs.User;
using Microsoft.AspNetCore.Authorization;

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;

    public UsersController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/users
    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users.ToListAsync();

        var response = users.Select(u => new UserResponseDto
        {
            Id        = u.Id,
            Name      = u.Name,
            Email     = u.Email,
            Role      = u.Role,
            CreatedAt = u.CreatedAt,
        });

        return Ok(response);
    }

    // GET /api/users/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "Usuário não encontrado." });

        return Ok(new UserResponseDto
        {
            Id        = user.Id,
            Name      = user.Name,
            Email     = user.Email,
            Role      = user.Role,
            CreatedAt = user.CreatedAt,
        });
    }

    // POST /api/users
    [HttpPost]
    public async Task<IActionResult> CreateUser(CreateUserDto dto)
    {
        // Senha deve ter no mínimo 6 caracteres.
        if (dto.Password.Length < 6)
            return BadRequest(new { message = "A senha deve ter no mínimo 6 caracteres." });

        // Email deve ser único.
        var emailExists = await _db.Users.AnyAsync(u => u.Email == dto.Email);
        if (emailExists)
            return Conflict(new { message = "Email já cadastrado por outro usuário." });

        var user = new User
        {
            Name         = dto.Name,
            Email        = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            Role         = dto.Role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new UserResponseDto
        {
            Id        = user.Id,
            Name      = user.Name,
            Email     = user.Email,
            Role      = user.Role,
            CreatedAt = user.CreatedAt,
        });
    }

    // PUT /api/users/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(int id, UpdateUserDto dto)
    {
        // Email deve ser único entre outros usuários.
        var emailExists = await _db.Users.AnyAsync(u => u.Email == dto.Email && u.Id != id);
        if (emailExists)
            return Conflict(new { message = "Email já cadastrado por outro usuário." });

        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "Usuário não encontrado." });

        user.Name      = dto.Name;
        user.Email     = dto.Email;
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        return Ok(new UserResponseDto
        {
            Id        = user.Id,
            Name      = user.Name,
            Email     = user.Email,
            Role      = user.Role,
            CreatedAt = user.CreatedAt,
        });
    }

    // DELETE /api/users/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(int id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
            return NotFound(new { message = "Usuário não encontrado." });

        // Impede deletar o último Admin — o sistema ficaria sem acesso.
        if (user.Role == UserRole.Admin)
        {
            var adminCount = await _db.Users.CountAsync(u => u.Role == UserRole.Admin);
            if (adminCount <= 1)
                return BadRequest(new { message = "Não é possível excluir o único administrador do sistema." });
        }

        // Impede exclusão se houver registros filhos.
        var hasRecords = await _db.Appointments.AnyAsync(a => a.UserId == id)
                      || await _db.MedicalRecords.AnyAsync(m => m.UserId == id)
                      || await _db.Payments.AnyAsync(p => p.UserId == id);
        if (hasRecords)
            return Conflict(new { message = "Não é possível excluir usuário com agendamentos ou prontuários associados." });

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
