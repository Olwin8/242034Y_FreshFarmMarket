using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _242034Y_FreshFarmMarket.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        // ✅ FIX: allow NULL so failed login logs don't violate FK
        public string? UserId { get; set; }

        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Action { get; set; } = string.Empty; // Login, Logout, Register, ProfileUpdate

        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        [MaxLength(500)]
        public string UserAgent { get; set; } = string.Empty;

        [Column(TypeName = "datetime")]
        public DateTime Timestamp { get; set; }

        public bool Success { get; set; }

        [MaxLength(500)]
        public string? AdditionalInfo { get; set; }

        // Navigation property
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
}
