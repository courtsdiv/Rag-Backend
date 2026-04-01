using System.Threading.Tasks;

namespace RagBackend.Infrastructure.Interfaces
{
    public interface IOpenRouterChatService
    {
        Task<string> GetAnswerAsync(string prompt);
    }
}