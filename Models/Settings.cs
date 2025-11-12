namespace bingooo.Models
{
    public class SettingsModel
    {
        public int? SoundSpeed { get; set; }
        public string? VoiceType { get; set; }
        public string? GameRule { get; set; }
        public bool checkRows { get; set; }
        public bool checkColumns { get; set; }
        public bool checkDiagonals { get; set; }
        public bool checkCorners { get; set; }
        public bool checkMiddle { get; set; }
        public bool Firework { get; set; }
    }
}
