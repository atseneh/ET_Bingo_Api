namespace bingooo.Models
{
    public class SalesDto
    {
        public DateTime StartedTime { get; set; }
        public DateTime EndedTime { get; set; }
        public string ShopName { get; set; }
        public int OnCall { get; set; }
        public int NoCards { get; set; }
        public int Price { get; set; }
        public decimal Collected { get; set; }
        public decimal Comission { get; set; }
        public decimal WinningAmount { get; set; }
        
    }
}
