
namespace bingooo.Models
{
    public class SalesActionDto
    {
        public string UserId { get; set; }
        public string UserName { get; set; }
        public decimal WinningAmount { get; set; }
        public decimal Amount { get; set; }
        public DateTime DateTime { get; set; }
        public DateTime? StartedTime { get; set; }
        public DateTime? EndedTime { get; set; }
        public string ShopName { get; set; }

        public int OnCall { get; set; }
        public int NoCards { get; set; }
        public decimal Price { get; set; }

        public decimal Collected { get; set; }
        public decimal Commission { get; set; }
        public decimal TotalCommission { get; set; } // Calculated using CalculateCommissionAndWinningAmount

    }
}
