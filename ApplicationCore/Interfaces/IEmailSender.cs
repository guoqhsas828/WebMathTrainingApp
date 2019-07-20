using System.Threading.Tasks;

namespace StoreManager.Services
{
  public interface IEmailSender
  {
    Task SendEmailAsync(string email, string subject, string message);
    Task SendSmsMessage(string msgText, string phoneNumber);
  }
}
