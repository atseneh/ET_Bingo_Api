using Microsoft.AspNetCore.Identity;

namespace bingooo.Models
{
    public class ApplicationUser : IdentityUser
    {
        public required string FullName { get; set; }

        public string? Address { get; set; }
        public string? ShopName { get; set; }
        public bool isAdmin { get; set; } // 1 for admin, 0 for non-
        public bool isActive { get; set; } // 1 for admin, 0 for non-admin
        public int? SoundSpeed { get; set; }
        public string? VoiceType { get; set; }
        public string? GameRule { get; set; }
        public bool? checkRows { get; set; }
        public bool? checkColumns { get; set; }
        public bool? checkDiagonals { get; set; }
        public bool? checkCorners { get; set; }
        public bool? checkMiddle { get; set; }
        public bool? Firework { get; set; }
    }
}