namespace bingooo.Models
{
    public class UserCommissionsDto
    {
        public string UserId { get; set; }
        public List<CommissionRangeDto> Ranges { get; set; } = new List<CommissionRangeDto>();
    }
    
}
