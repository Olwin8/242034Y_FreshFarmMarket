using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _242034Y_FreshFarmMarket.Models
{
    public class UserSession
    {
        [Key]
        [MaxLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [MaxLength(256)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(100)]
        public string FullName { get; set; } = string.Empty;

        [MaxLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        [MaxLength(500)]
        public string UserAgent { get; set; } = string.Empty;

        [MaxLength(100)]
        public string DeviceInfo { get; set; } = string.Empty;

        [Column(TypeName = "datetime")]
        public DateTime CreatedAt { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime LastActivity { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime ExpiresAt { get; set; }

        [Column(TypeName = "datetime")]
        public DateTime? TerminatedAt { get; set; }

        [MaxLength(100)]
        public string? TerminationReason { get; set; }

        public bool IsActive { get; set; }

        public bool IsMainDevice { get; set; }

        // Navigation property
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
}