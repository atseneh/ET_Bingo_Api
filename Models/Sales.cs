using System;

namespace bingooo.Models
{
    public class Sales
    {
        public int Id { get; set; } // Primary key
        public string UserId { get; set; } // Foreign key referencing AspNetUsers.Id
        public string ShopName { get; set; } //shopname for current user
        public decimal TransactionAmount { get; set; } // The transaction amount
        public string TransactionType { get; set; } // Type of transaction (e.g., "TopUp", "Deduction", "CalculatedBalance")
        public DateTime Date { get; set; } // Timestamp of the transaction
        public ApplicationUser User { get; set; }

    }
}