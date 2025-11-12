//namespace bingooo.Models
//{
//    public class CommissionsDto
//    {
//        public string Id { get; set; }
//        public string UserName { get; set; }
//        public string FullName { get; set; }
//        public string PhoneNumber { get; set; }
//        public decimal CalculatedBalance { get; set; } // TopUp - Deductions
//    }
//}


namespace bingooo.Models
{
    public class CommissionsDto
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string PhoneNumber { get; set; }
        public string Address { get; set; } // New: User's address
        public decimal Credit { get; set; } // New: Latest top-up amount
        public decimal CalculatedBalance { get; set; } // TopUp - Deductions
        public double Status { get; set; } // Balance as a percentage of credit
    }
}