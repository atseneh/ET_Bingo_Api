namespace bingooo.Models;
public class Users
{
    public  string Id { get; set; }
    public  string FullName { get; set; }
    public  string UserName { get; set; }
    public  string PhoneNumber { get; set; } // Use this or the built-in PhoneNumber property from IdentityUser
    public  string Address { get; set; }
    public  string ShopName { get; set; }
    public bool isActive { get; set; } // 1 for admin, 0 for non-admin
    public bool isAdmin { get; set; } // 1 for admin, 0 for non-

}

