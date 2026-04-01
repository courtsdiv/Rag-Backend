using System.Threading.Tasks;

namespace RagBackend.Infrastructure.Interfaces
{
    public interface IOpenRouterEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text);
    }
}