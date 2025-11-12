namespace bingooo.Models
{
    public class SalesReportDto
    {
        public string UserName { get; set; }
        public int NumberOfGames { get; set; } // Total deductions (IsTopUp = false)
        public decimal TotalCommission { get; set; } // Calculated using CalculateCommissionAndWinningAmount
        public decimal? TotalBalanceTransaction { get; set; } // Sum of all transactions (top-ups and deductions)
    }
}