using System.Text.RegularExpressions;

namespace RagBackend.Domain.Utils
{
    // Utility for splitting large text into smaller chunks for embedding and search
    public static class TextChunker
    {
        // Splits text into sentence-based chunks under a maximum character size
        public static List<string> ChunkText(string text, int maxChunkSize = 200)
        {
            var chunks = new List<string>();

            // Guard: return empty list if input text is empty
            if (string.IsNullOrWhiteSpace(text))
                return chunks;

            // Split text into sentences using punctuation as boundaries
            var sentences = Regex.Split(text, @"(?<=[\.!\?])\s+");

            var currentChunk = string.Empty;

            foreach (var sentenceRaw in sentences)
            {
                var sentence = sentenceRaw.Trim();
                if (sentence.Length == 0)
                    continue;

                // If adding this sentence would exceed the max size,
                // store the current chunk and start a new one
                if ((currentChunk + " " + sentence).Length > maxChunkSize)
                {
                    chunks.Add(currentChunk.Trim());
                    currentChunk = sentence;
                }
                else
                {
                    // Otherwise, keep adding sentences to the current chunk
                    currentChunk += " " + sentence;
                }
            }

            // Add the final chunk if one exists
            if (!string.IsNullOrWhiteSpace(currentChunk))
                chunks.Add(currentChunk.Trim());

            return chunks;
        }
    }
}