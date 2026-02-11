using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace _242034Y_FreshFarmMarket.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Required]
        [MaxLength(100)]
        [PersonalData]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(50)]
        [PersonalData]
        public string? Gender { get; set; }

        [Required]
        [MaxLength(13)] // +65 prefix + 8 digits = 13 characters
        [PersonalData]
        [RegularExpression(@"^\+65[689]\d{7}$", ErrorMessage = "Singapore mobile number must start with +65 followed by 8 digits starting with 6, 8 or 9")]
        public string MobileNo { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        [PersonalData]
        public string DeliveryAddress { get; set; } = string.Empty;

        [Column(TypeName = "nvarchar(max)")]
        [ProtectedPersonalData]
        public string? CreditCardNoEncrypted { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? PhotoPath { get; set; }

        [Column(TypeName = "nvarchar(max)")]
        public string? AboutMe { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? CreatedDate { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? LastUpdated { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? LastLoginDate { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? LastPasswordChangeDate { get; set; }

        public int FailedLoginAttempts { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? LockoutEnd { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? LastFailedLoginAttempt { get; set; }

        [MaxLength(100)]
        public string? TwoFactorMethod { get; set; }

        // Session tracking
        [MaxLength(100)]
        public string? CurrentSessionId { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? LastSessionActivity { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? LastPasswordResetRequest { get; set; }

        [NotMapped]
        public string DisplayMobileNo => MobileNo.Length > 3 ? MobileNo[3..] : MobileNo;

        public void SetMobileNo(string digits)
        {
            if (digits.Length == 8 && Regex.IsMatch(digits, @"^[689]"))
            {
                MobileNo = $"+65{digits}";
            }
        }

        [NotMapped]
        public bool IsPasswordExpired
        {
            get
            {
                if (!LastPasswordChangeDate.HasValue)
                    return false;

                var maxPasswordAge = TimeSpan.FromMinutes(2); // ✅ 2 minutes
                return (DateTime.Now - LastPasswordChangeDate.Value) > maxPasswordAge;
            }
        }

        [NotMapped]
        public bool IsAccountLocked
        {
            get
            {
                return LockoutEnd.HasValue && LockoutEnd.Value > DateTime.Now;
            }
        }

        [NotMapped]
        public bool ShouldChangePassword
        {
            get
            {
                return IsPasswordExpired ||
                       (CreatedDate.HasValue && (DateTime.Now - CreatedDate.Value).TotalDays > 90);
            }
        }
    }
}
