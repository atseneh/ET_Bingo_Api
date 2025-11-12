using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace bingooo.Models
{
    public class UserCommission
    {
        // Primary Key
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Foreign Key referencing AspNetUsers table
        [Required]
        [StringLength(450)]
        public string? UserId { get; set; } // Maps to AspNetUsers.Id

        // Range Fields
        [Required]
        public int MinCount { get; set; }

        [Required]
        public int MaxCount { get; set; }

        // Multiplier Field
        [Required]
        [Column(TypeName = "decimal(5, 2)")]
        public decimal Multiplier { get; set; }

        // Optional Index Value
        public int? Index_value { get; set; }

        // Navigation Property for the related User (AspNetUsers)
        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }
    }
}
