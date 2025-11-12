namespace bingooo.Models
{
    public class AdminSalesDto
    {
        public string? UserId { get; set; }
        public string? UserName { get; set; }
        public string? FullName { get; set; }
        public string? ShopName { get; set; }
        public DateTime Date { get; set; }
        public decimal TotalCommission { get; set; } // Calculated using CalculateCommissionAndWinningAmount
        public int NumberOfGames { get; set; } // Total deductions (IsTopUp = false)
    }
}