using System.Text.RegularExpressions;

namespace _242034Y_FreshFarmMarket.Services
{
    public interface IPasswordStrengthService
    {
        PasswordStrengthResult CheckPasswordStrength(string password);
    }

    public class PasswordStrengthService : IPasswordStrengthService
    {
        public PasswordStrengthResult CheckPasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                return new PasswordStrengthResult
                {
                    Score = 0,
                    Strength = "Not set",
                    IsValid = false,
                    Suggestions = new List<string>
                    {
                        "✗ Password cannot be empty",
                        "✗ Minimum 12 characters required",
                        "✗ Include uppercase letters",
                        "✗ Include lowercase letters",
                        "✗ Include numbers",
                        "✗ Include special characters"
                    }
                };
            }

            int rawScore = 0; // rawScore max = 85 now
            var suggestions = new List<string>();

            // Check length (REQUIREMENT: Minimum 12 characters)
            if (password.Length >= 12)
            {
                rawScore += 25;
                suggestions.Add($"✓ Length: {password.Length} characters (minimum 12)");
            }
            else
            {
                suggestions.Add($"✗ Length: {password.Length} characters (minimum 12 required)");
            }

            // Check for lowercase letters
            if (Regex.IsMatch(password, "[a-z]"))
            {
                rawScore += 15;
                suggestions.Add("✓ Contains lowercase letters");
            }
            else
            {
                suggestions.Add("✗ Add lowercase letters (a-z)");
            }

            // Check for uppercase letters
            if (Regex.IsMatch(password, "[A-Z]"))
            {
                rawScore += 15;
                suggestions.Add("✓ Contains uppercase letters");
            }
            else
            {
                suggestions.Add("✗ Add uppercase letters (A-Z)");
            }

            // Check for numbers
            if (Regex.IsMatch(password, "[0-9]"))
            {
                rawScore += 15;
                suggestions.Add("✓ Contains numbers");
            }
            else
            {
                suggestions.Add("✗ Add numbers (0-9)");
            }

            // Check for special characters
            if (Regex.IsMatch(password, "[^a-zA-Z0-9]"))
            {
                rawScore += 15;
                suggestions.Add("✓ Contains special characters");
            }
            else
            {
                suggestions.Add("✗ Add special characters (!@#$%^&*)");
            }

            // ✅ Normalize score to 0–100 for your UI meter (so it still fills nicely)
            // raw max = 85 -> 100%
            var score = (int)Math.Round((rawScore / 85.0) * 100.0);
            score = Math.Clamp(score, 0, 100);

            // Determine strength level (same labels as before)
            string strength;
            bool isValid = true;

            if (score >= 90)
            {
                strength = "Very Strong";
            }
            else if (score >= 75)
            {
                strength = "Strong";
            }
            else if (score >= 60)
            {
                strength = "Good";
                isValid = password.Length >= 12; // Must meet minimum length
            }
            else if (score >= 40)
            {
                strength = "Weak";
                isValid = false;
            }
            else
            {
                strength = "Very Weak";
                isValid = false;
            }

            // Additional validation for minimum requirements
            if (password.Length < 12 ||
                !Regex.IsMatch(password, "[a-z]") ||
                !Regex.IsMatch(password, "[A-Z]") ||
                !Regex.IsMatch(password, "[0-9]") ||
                !Regex.IsMatch(password, "[^a-zA-Z0-9]"))
            {
                isValid = false;
            }

            return new PasswordStrengthResult
            {
                Score = score,
                Strength = strength,
                IsValid = isValid,
                Suggestions = suggestions
            };
        }
    }

    public class PasswordStrengthResult
    {
        public int Score { get; set; }
        public string Strength { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public List<string> Suggestions { get; set; } = new List<string>();
    }
}
