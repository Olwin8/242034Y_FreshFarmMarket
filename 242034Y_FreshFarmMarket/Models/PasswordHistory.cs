using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _242034Y_FreshFarmMarket.Models
{
    public class PasswordHistory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        // Store HASH only
        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Column(TypeName = "datetime")]
        public DateTime ChangedAt { get; set; } = DateTime.Now;

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
}
