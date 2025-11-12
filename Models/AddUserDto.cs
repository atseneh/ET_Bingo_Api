using System.ComponentModel.DataAnnotations;

namespace bingooo.Models
{
    public class AddUserDto
    {
        [Required(ErrorMessage = "Username is required.")]
        public string UserName { get; set; }

        [Required(ErrorMessage = "Full Name is required.")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Phone Number is required.")]
        public string PhoneNumber { get; set; }

        [Required(ErrorMessage = "Address is required.")]
        public string Address { get; set; }

        [Required(ErrorMessage = "Shop Name is required.")]
        public string ShopName { get; set; }
    }
}
