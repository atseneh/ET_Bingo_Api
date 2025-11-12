namespace bingooo.Models
{
    public class CommissionRangeDto
    {
        public int MinCount { get; set; }
        public int MaxCount { get; set; }
        public decimal Multiplier { get; set; }
    }

    public class CommissionRangeRequest
    {
        public int MinCount { get; set; }
        public int MaxCount { get; set; }
        public decimal Multiplier { get; set; }
    }
}
