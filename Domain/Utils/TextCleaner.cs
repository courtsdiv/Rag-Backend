using System.Text.RegularExpressions;

namespace RagBackend.Domain.Utils
{
    public static class TextCleaner
    {
        public static string Clean(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            // Normalise line endings
            text = text.Replace("\r\n", "\n");

            // 1) Fix headings: line ends with a letter, next line starts with Capital
            // "Why Supply Chains Matter\nCompetitive Advantage:" 
            // -> "Why Supply Chains Matter. Competitive Advantage:"
            // "Key Aspects of a Supply Chain\nFlows:" 
            // -> "Key Aspects of a Supply Chain. Flows:"
            text = Regex.Replace(text, "([a-zA-Z])\\s*\\n\\s*([A-Z])", "$1. $2");

            // 2) Fix broken words split mid-word (lowercase after newline):
            // "custo\nmer" -> "customer"
            text = Regex.Replace(text, "([a-zA-Z])\\s*\\n\\s*([a-z])", "$1$2");

            // 3) Any remaining newlines -> space
            text = Regex.Replace(text, "\\n+", " ");

            // 4) Collapse multiple spaces
            text = Regex.Replace(text, "\\s{2,}", " ");

            return text.Trim();
        }
    }
}
