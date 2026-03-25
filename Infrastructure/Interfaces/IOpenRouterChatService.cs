using System.Threading.Tasks;
using RagBackend.Infrastructure.Interfaces;

namespace RagBackend.Infrastructure.Interfaces
{
    public interface IOpenRouterChatService
    {
        Task<string> GetAnswerAsync(string prompt);
    }
}