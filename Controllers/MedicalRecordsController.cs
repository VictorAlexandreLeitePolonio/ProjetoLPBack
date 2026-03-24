using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjetoLP.API.Data;
using ProjetoLP.API.Models;
using ProjetoLP.API.DTOs.MedicalRecord;
using Microsoft.AspNetCore.Authorization;
using ProjetoLP.API.DTOs;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MedicalRecordsController : ControllerBase
{
    private readonly AppDbContext _db;

    public MedicalRecordsController(AppDbContext db)
    {
        _db = db;
    }

    // GET /api/medicalrecords
    [HttpGet]
    public async Task<IActionResult> GetMedicalRecords(
        [FromQuery] int? patientId,
        [FromQuery] string? patientName,
        [FromQuery] int? userId,
        [FromQuery] DateOnly? createdAt,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _db.MedicalRecords
            .Include(m => m.User)
            .Include(m => m.Patient)
            .AsQueryable();

        if (patientId.HasValue)
            query = query.Where(m => m.PatientId == patientId.Value);

        if (!string.IsNullOrEmpty(patientName))
            query = query.Where(m => m.Patient.Name.Contains(patientName));

        if (userId.HasValue)
            query = query.Where(m => m.UserId == userId.Value);

        if (createdAt.HasValue)
            query = query.Where(m => DateOnly.FromDateTime(m.CreatedAt) == createdAt.Value);

        var totalCount = await query.CountAsync();
        var records = await query
            .OrderBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var data = records.Select(m => new MedicalRecordResponseDto
        {
            Id                   = m.Id,
            UserId               = m.UserId,
            UserName             = m.User.Name,
            PatientId            = m.PatientId,
            PatientName          = m.Patient.Name,
            Patologia            = m.Patologia,
            QueixaPrincipal      = m.QueixaPrincipal,
            ExamesImagem         = m.ExamesImagem,
            DoencaAntiga         = m.DoencaAntiga,
            DoencaAtual          = m.DoencaAtual,
            Habitos              = m.Habitos,
            ExamesFisicos        = m.ExamesFisicos,
            SinaisVitais         = m.SinaisVitais,
            Medicamentos         = m.Medicamentos,
            Cirurgias            = m.Cirurgias,
            OutrasDoencas        = m.OutrasDoencas,
            Sessao               = m.Sessao,
            Titulo               = m.Titulo,
            Contrato             = m.Contrato,
            OrientacaoDomiciliar = m.OrientacaoDomiciliar,
            CreatedAt            = m.CreatedAt,
        });

        return Ok(new PagedResult<MedicalRecordResponseDto>
        {
            Data       = data,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = pageSize
        });
    }

    // GET /api/medicalrecords/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetMedicalRecord(int id)
    {
        var m = await _db.MedicalRecords
            .Include(m => m.User)
            .Include(m => m.Patient)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (m == null)
            return NotFound(new { message = "Prontuário não encontrado." });

        return Ok(new MedicalRecordResponseDto
        {
            Id                   = m.Id,
            UserId               = m.UserId,
            UserName             = m.User.Name,
            PatientId            = m.PatientId,
            PatientName          = m.Patient.Name,
            Patologia            = m.Patologia,
            QueixaPrincipal      = m.QueixaPrincipal,
            ExamesImagem         = m.ExamesImagem,
            DoencaAntiga         = m.DoencaAntiga,
            DoencaAtual          = m.DoencaAtual,
            Habitos              = m.Habitos,
            ExamesFisicos        = m.ExamesFisicos,
            SinaisVitais         = m.SinaisVitais,
            Medicamentos         = m.Medicamentos,
            Cirurgias            = m.Cirurgias,
            OutrasDoencas        = m.OutrasDoencas,
            Sessao               = m.Sessao,
            Titulo               = m.Titulo,
            Contrato             = m.Contrato,
            OrientacaoDomiciliar = m.OrientacaoDomiciliar,
            CreatedAt            = m.CreatedAt,
        });
    }

    // POST /api/medicalrecords
    [HttpPost]
    public async Task<IActionResult> CreateMedicalRecord(CreateMedicalRecordDto dto)
    {
        var userExists = await _db.Users.AnyAsync(u => u.Id == dto.UserId);
        if (!userExists)
            return NotFound(new { message = "Fisioterapeuta não encontrado." });

        var patientExists = await _db.Patients.AnyAsync(p => p.Id == dto.PatientId);
        if (!patientExists)
            return NotFound(new { message = "Paciente não encontrado." });

        var medicalRecord = new MedicalRecord
        {
            UserId               = dto.UserId,
            PatientId            = dto.PatientId,
            Patologia            = dto.Patologia,
            QueixaPrincipal      = dto.QueixaPrincipal,
            DoencaAntiga         = dto.DoencaAntiga,
            DoencaAtual          = dto.DoencaAtual,
            Habitos              = dto.Habitos,
            ExamesFisicos        = dto.ExamesFisicos,
            SinaisVitais         = dto.SinaisVitais,
            Medicamentos         = dto.Medicamentos,
            Cirurgias            = dto.Cirurgias,
            OutrasDoencas        = dto.OutrasDoencas,
            Sessao               = dto.Sessao,
            Titulo               = dto.Titulo,
            OrientacaoDomiciliar = dto.OrientacaoDomiciliar,
        };

        _db.MedicalRecords.Add(medicalRecord);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetMedicalRecord), new { id = medicalRecord.Id }, medicalRecord);
    }

    // POST /api/medicalrecords/{id}/contrato
    [HttpPost("{id}/contrato")]
    public async Task<IActionResult> UploadContrato(int id, IFormFile file)
    {
        var record = await _db.MedicalRecords.FindAsync(id);
        if (record == null)
            return NotFound(new { message = "Prontuário não encontrado." });

        if (file.ContentType != "application/pdf")
            return BadRequest(new { message = "Apenas arquivos PDF são aceitos." });

        // Limite de 10MB para contratos.
        if (file.Length > 10 * 1024 * 1024)
            return BadRequest(new { message = "O arquivo não pode ser maior que 10MB." });

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var folder   = Path.Combine("wwwroot", "uploads", "contratos");
        var filePath = Path.Combine(folder, fileName);

        using (var stream = System.IO.File.Create(filePath))
            await file.CopyToAsync(stream);

        record.Contrato = $"/uploads/contratos/{fileName}";
        await _db.SaveChangesAsync();

        return Ok(new { url = record.Contrato });
    }

    // POST /api/medicalrecords/{id}/exames
    [HttpPost("{id}/exames")]
    public async Task<IActionResult> UploadExame(int id, IFormFile file)
    {
        var record = await _db.MedicalRecords.FindAsync(id);
        if (record == null)
            return NotFound(new { message = "Prontuário não encontrado." });

        var allowedTypes = new[] { "image/jpeg", "image/png" };
        if (!allowedTypes.Contains(file.ContentType))
            return BadRequest(new { message = "Apenas imagens JPG ou PNG são aceitas." });

        // Limite de 5MB para imagens de exames.
        if (file.Length > 5 * 1024 * 1024)
            return BadRequest(new { message = "A imagem não pode ser maior que 5MB." });

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var folder   = Path.Combine("wwwroot", "uploads", "exames");
        var filePath = Path.Combine(folder, fileName);

        using (var stream = System.IO.File.Create(filePath))
            await file.CopyToAsync(stream);

        record.ExamesImagem = $"/uploads/exames/{fileName}";
        await _db.SaveChangesAsync();

        return Ok(new { url = record.ExamesImagem });
    }

    // PUT /api/medicalrecords/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateMedicalRecord(int id, UpdateMedicalRecordDto dto)
    {
        var m = await _db.MedicalRecords.FindAsync(id);
        if (m == null)
            return NotFound(new { message = "Prontuário não encontrado." });

        m.Patologia            = dto.Patologia;
        m.QueixaPrincipal      = dto.QueixaPrincipal;
        m.DoencaAntiga         = dto.DoencaAntiga;
        m.DoencaAtual          = dto.DoencaAtual;
        m.Habitos              = dto.Habitos;
        m.ExamesFisicos        = dto.ExamesFisicos;
        m.SinaisVitais         = dto.SinaisVitais;
        m.Medicamentos         = dto.Medicamentos;
        m.Cirurgias            = dto.Cirurgias;
        m.OutrasDoencas        = dto.OutrasDoencas;
        m.Sessao               = dto.Sessao;
        m.Titulo               = dto.Titulo;
        m.OrientacaoDomiciliar = dto.OrientacaoDomiciliar;

        await _db.SaveChangesAsync();
        return Ok(m);
    }

    // DELETE /api/medicalrecords/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMedicalRecord(int id)
    {
        var m = await _db.MedicalRecords.FindAsync(id);
        if (m == null)
            return NotFound(new { message = "Prontuário não encontrado." });

        _db.MedicalRecords.Remove(m);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
