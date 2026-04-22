namespace RagBackend.Application.Interfaces
{
    /// <summary>
    /// Interface for a service that generates text responses
    /// based on an input prompt.
    /// 
    /// In this project, the implementation of this interface
    /// sends prompts to an AI text generation service
    /// (such as OpenRouter) and returns the generated answer.
    /// 
    /// Using an interface here allows the application to stay
    /// independent of any specific AI provider.
    /// </summary>
    public interface IChatCompletionService
    {
        /// <summary>
        /// Sends a prompt to the text generation service and
        /// returns the generated response.
        /// </summary>
        /// <param name="prompt">
        /// The full text prompt, including context and instructions.
        /// </param>
        /// <returns>
        /// The generated response text.
        /// </returns>
        Task<string> GetAnswerAsync(string prompt);
    }
}