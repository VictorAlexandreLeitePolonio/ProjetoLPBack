namespace ProjetoLP.API.DTOs.Payment;

using ProjetoLP.API.Models;

// Dados retornados ao cliente nas respostas da API.
public class PaymentResponseDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int PatientId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string ReferenceMonth { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public decimal PlanAmount { get; set; }

    public PaymentStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
