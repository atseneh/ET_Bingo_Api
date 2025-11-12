using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using bingooo.Models;

namespace YourNamespace.Models
{
    public class Balance
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public required string UserId { get; set; } // Foreign key referencing AspNetUsers.Id
        public decimal BalanceAmount { get; set; } // Balance amount
        public DateTime Date { get; set; } // Date of the transaction
        public bool IsTopUp { get; set; } // Indicates whether this is a top-up
                                          // Navigation property to access the related User
        public ApplicationUser User { get; set; }
        public DateTime? StartedTime { get; set; }
        public DateTime? EndedTime { get; set; }
        public string? ShopName { get; set; }
        public int? OnCall { get; set; }
        public int? NoCards { get; set; }
        public decimal? Price { get; set; }
        public decimal? Collected { get; set; }
        public decimal? Comission { get; set; }
        public decimal? WinningAmount { get; set; }

    }
}