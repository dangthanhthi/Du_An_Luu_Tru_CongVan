using System.Threading.Tasks;

namespace EmailWorkerService.Services
{
    public interface IEmailProcessor
    {
        Task ProcessIncomingEmailsAsync();
    }
}