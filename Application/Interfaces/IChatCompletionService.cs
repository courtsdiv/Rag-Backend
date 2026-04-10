namespace RagBackend.Application.Interfaces
{
    // Generates a text response from a prompt using an LLM
    public interface IChatCompletionService
    {
        Task<string> GetAnswerAsync(string prompt);
    }
}