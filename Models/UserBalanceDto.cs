namespace bingooo.Models
{
    public class UserBalanceDto
    {
        public string UserId { get; set; } // User ID
        public decimal CalculatedBalance { get; set; } // TopUp - Deductions
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string ShopName { get; set; }
        public string Address { get; set; }
        public decimal Credit { get; set; } // Latest top-up amount
        public decimal Balance { get; set; } // Current balance
        public double Status { get; set; } // Balance as a percentage of credit
        public int Percentage { get; set; } // Fixed percentage (15, 20, or 25)
        public DateTime? LastTopUpDate { get; set; } // Last top-up date and time
    }
}
