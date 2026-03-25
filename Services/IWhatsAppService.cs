namespace ProjetoLP.API.Services;

public interface IWhatsAppService
{
    Task SendTextAsync(string phone, string message);
}
