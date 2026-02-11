using System.Text.Json;

namespace _242034Y_FreshFarmMarket.Services
{
    public interface IRecaptchaService
    {
        Task<(bool Success, double Score, string? Error)> VerifyAsync(string token, string? remoteIp, string expectedAction);
    }

    public class RecaptchaService : IRecaptchaService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<RecaptchaService> _logger;

        public RecaptchaService(HttpClient httpClient, IConfiguration configuration, ILogger<RecaptchaService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<(bool Success, double Score, string? Error)> VerifyAsync(string token, string? remoteIp, string expectedAction)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return (false, 0.0, "Missing reCAPTCHA token.");
            }

            var secret = _configuration["Recaptcha:SecretKey"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                return (false, 0.0, "reCAPTCHA SecretKey not configured.");
            }

            try
            {
                var form = new Dictionary<string, string>
                {
                    ["secret"] = secret,
                    ["response"] = token
                };

                if (!string.IsNullOrWhiteSpace(remoteIp))
                {
                    form["remoteip"] = remoteIp;
                }

                using var content = new FormUrlEncodedContent(form);
                using var resp = await _httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);
                var json = await resp.Content.ReadAsStringAsync();

                var result = JsonSerializer.Deserialize<RecaptchaVerifyResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (result == null)
                {
                    return (false, 0.0, "Invalid reCAPTCHA response.");
                }

                if (!result.Success)
                {
                    return (false, result.Score, "reCAPTCHA verification failed.");
                }

                // v3 fields
                if (!string.IsNullOrWhiteSpace(result.Action) &&
                    !string.Equals(result.Action, expectedAction, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, result.Score, "reCAPTCHA action mismatch.");
                }

                // Score threshold (reasonable default)
                if (result.Score < 0.5)
                {
                    return (false, result.Score, "reCAPTCHA score too low.");
                }

                return (true, result.Score, null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "reCAPTCHA verification error.");
                return (false, 0.0, "reCAPTCHA service error.");
            }
        }

        private class RecaptchaVerifyResponse
        {
            public bool Success { get; set; }
            public double Score { get; set; }
            public string? Action { get; set; }
            public string? Hostname { get; set; }
            public string? Challenge_ts { get; set; }
            public string[]? ErrorCodes { get; set; }
        }
    }
}
