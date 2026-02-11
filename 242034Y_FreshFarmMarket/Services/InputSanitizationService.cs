using System.Net;

namespace _242034Y_FreshFarmMarket.Services
{
    public interface IInputSanitizationService
    {
        // Store safe (encoded) representation in DB
        string EncodeForStorage(string? input);

        // Convert DB-encoded text back to normal text for display/binding
        string DecodeFromStorage(string? encodedInput);

        // Optional: basic trimming/normalization (kept simple to avoid “unnecessary changes”)
        string Normalize(string? input);
    }

    public class InputSanitizationService : IInputSanitizationService
    {
        public string Normalize(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return input.Trim();
        }

        public string EncodeForStorage(string? input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // Normalize first to keep stored values consistent
            var normalized = Normalize(input);

            // HTML-encode for safe storage in DB (prevents stored XSS)
            return WebUtility.HtmlEncode(normalized);
        }

        public string DecodeFromStorage(string? encodedInput)
        {
            if (string.IsNullOrEmpty(encodedInput)) return string.Empty;

            // Decode back to readable text for display/binding
            // IMPORTANT: still render with Razor @... (NOT Html.Raw)
            return WebUtility.HtmlDecode(encodedInput);
        }
    }
}
