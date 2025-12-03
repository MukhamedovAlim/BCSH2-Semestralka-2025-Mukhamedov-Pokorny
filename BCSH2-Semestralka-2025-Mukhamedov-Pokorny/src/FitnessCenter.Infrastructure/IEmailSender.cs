using System.Threading.Tasks;

namespace FitnessCenter.Infrastructure
{
    public interface IEmailSender
    {
        Task SendEmailAsync(string to, string subject, string body, bool isHtml = true);
    }
}
