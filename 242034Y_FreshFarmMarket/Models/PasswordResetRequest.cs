using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _242034Y_FreshFarmMarket.Models
{
    public class PasswordResetRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(64)]
        public string RequestId { get; set; } = string.Empty; // GUID "N"

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [Column(TypeName = "nvarchar(max)")]
        public string Token { get; set; } = string.Empty; // Identity reset token stored server-side

        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Column(TypeName = "datetime")]
        public DateTime ExpiresAt { get; set; } = DateTime.Now.AddMinutes(15);

        public bool Used { get; set; } = false;

        [Column(TypeName = "datetime")]
        public DateTime? UsedAt { get; set; }
    }
}
