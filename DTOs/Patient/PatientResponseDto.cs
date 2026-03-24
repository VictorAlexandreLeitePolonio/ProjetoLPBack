using ProjetoLP.API.Models;

namespace ProjetoLP.API.DTOs.Patient
{
    public class PatientResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string CPF { get; set; } = string.Empty;
        public string Rg { get; set; } = string.Empty;
        public string Rua { get; set; } = string.Empty;
        public string Numero { get; set; } = string.Empty;
        public string Bairro { get; set; } = string.Empty;
        public string Cidade { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public string Cep { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public AppointmentStatus appointmentStatus { get; set; }
        public PaymentStatus paymentStatus { get; set; }
        public DateTime CreatedAt { get; set; }
       
    }
}