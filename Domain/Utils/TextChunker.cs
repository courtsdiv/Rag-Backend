using System.Text.RegularExpressions;

namespace RagBackend.Domain.Utils
{
    public static class TextChunker
    {
        public static List<string> ChunkText(string text, int maxChunkSize = 200)
        {
            var chunks = new List<string>();

            if (string.IsNullOrWhiteSpace(text))
                return chunks;

            // Split into sentences by punctuation
            var sentences = Regex.Split(text, @"(?<=[\.!\?])\s+");

            var current = "";
            foreach (var s in sentences)
            {
                var sentence = s.Trim();
                if (sentence.Length == 0)
                    continue;

                if ((current + " " + sentence).Length > maxChunkSize)
                {
                    chunks.Add(current.Trim());
                    current = sentence;
                }
                else
                {
                    current += " " + sentence;
                }
            }

            if (!string.IsNullOrWhiteSpace(current))
                chunks.Add(current.Trim());

            return chunks;
        }
    }
}