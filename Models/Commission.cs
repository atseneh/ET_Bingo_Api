namespace bingooo.Models
{
    public class Commission
    {
        public int Id { get; set; }
        public required string Description { get; set; }
        public decimal Amount { get; set; }
        public bool IsPending { get; set; }
    }
   
}