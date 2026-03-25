using System.Threading.Tasks;
using RagBackend.Infrastructure.Interfaces;

namespace RagBackend.Infrastructure.Interfaces
{
    public interface IOpenRouterEmbeddingService
    {
        Task<float[]> GetEmbeddingAsync(string text);
    }
}