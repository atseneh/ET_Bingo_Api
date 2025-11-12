using System.Collections.Generic;

namespace bingooo.Models
{
    public class Game
    {
        public int Id { get; set; }
        public List<int> CalledNumbers { get; set; } = new List<int>();
        public List<int> SelectedCartelas { get; set; } = new List<int>();
    }
}