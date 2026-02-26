using Microsoft.AspNetCore.Mvc;
using RagBackend.Services;

namespace RagBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    /// <summary>
    /// Simple controller for RAG 
    /// </summary>
    /// <remarks>
    /// Has three endpoints: index (save text), search (find similar texts), and answer (use context to answer a question).
    /// </remarks>
    public class RagController : ControllerBase
    {
        // Service that turns text into embedding vectors.
        private readonly OpenRouterEmbeddingService _embeddingService;

        // Service that stores and finds vectors (Qdrant client).
        private readonly QdrantService _qdrantService;

        // Service that calls a chat model to produce answers.
        private readonly OpenRouterChatService _chatService;

        // The size of embeddings produced by the embedding model.
        private const int EmbeddingSize = 1536;

        private readonly int _topK;

        /// <summary>
        /// Save the injected services for use in the endpoints.
        /// </summary>
        public RagController(
            OpenRouterEmbeddingService embeddingService,
            QdrantService qdrantService,
            OpenRouterChatService chatService,
            IConfiguration config)
        {
            _embeddingService = embeddingService;
            _qdrantService = qdrantService;
            _chatService = chatService;

            _topK = config.GetValue<int>("Retrieval:TopK", 3); // Default to 3 if missing
        }

        /// <summary>
        /// Index a piece of text: get its vector and store it with the text.
        /// </summary>
        [HttpPost("index")]
        public async Task<IActionResult> IndexText([FromBody] string text)
        {
            // Make sure the collection exists and is configured for our vector size.
            await _qdrantService.EnsureCollectionAsync(EmbeddingSize);

            // Convert the text to an embedding vector.
            var vector = await _embeddingService.GetEmbeddingAsync(text);

            // Store the vector and text together in Qdrant.
            await _qdrantService.UpsertAsync(vector, text);

            // Return a simple success response.
            return Ok(new { message = "Text indexed successfully." });
        }

        /// <summary>
        /// Search for texts similar to the given query.
        /// </summary>
        [HttpPost("search")]
        public async Task<IActionResult> Search([FromBody] string query)
        {
            // Ensure collection exists before searching.
            await _qdrantService.EnsureCollectionAsync(EmbeddingSize);

            // Turn the query into a vector.
            var queryVector = await _embeddingService.GetEmbeddingAsync(query);

            // Get the top 3 matching texts from Qdrant.
            var results = await _qdrantService.SearchAsync(queryVector, limit: _topK);

            // Return the matching texts.
            return Ok(results);
        }

        /// <summary>
        /// Answer a question using retrieved text as context.
        /// </summary>
        [HttpPost("answer")]
        public async Task<IActionResult> GetAnswer([FromBody] string question)
        {
            // Ensure the collection exists.
            await _qdrantService.EnsureCollectionAsync(EmbeddingSize);

            // Get the embedding for the question.
            var queryEmbedding = await _embeddingService.GetEmbeddingAsync(question);

            Console.WriteLine($"[RAG] Question: \" {question}\"");
            Console.WriteLine($"[RAG] Using TopK = 3");

            // Find the top 3 relevant text chunks.
            var topChunks = await _qdrantService.SearchAsync(queryEmbedding, limit: _topK);

            // Combine the chunks into a short context string.
            var context = string.Join("\n\n", topChunks).Trim();
            

            // Create a prompt that tells the chat model to only use the context.
            var prompt = $@"
            ## CONTEXT
            ---------------------
            {context}
            ---------------------

            ## QUESTION
            {question}

            ## INSTRUCTIONS
            - Use ONLY the context above when answering.
            - If the answer is not in the context, say:
            'The provided documents do not contain this information.'
            - Keep your answer short and factual.
            ".Trim();


            Console.WriteLine("----- FINAL PROMPT -----");
            Console.WriteLine(prompt);
            Console.WriteLine("------------------------");


            // Ask the chat service to answer using the prompt.
            var answer = await _chatService.GetAnswerAsync(prompt);

            // Return the question, the context chunks, and the model answer.
            return Ok(new
            {
                question,
                context = topChunks,
                answer
            });
        }
    }
}