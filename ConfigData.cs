namespace BBbuilder
{
    public class ConfigData
    {
        public string GamePath { get; set; } = "";
        public string ModPath { get; set; } = "";
        public bool MoveZip { get; set; } = false;
        public bool UseSteam { get; set; } = false;
        public string[] FoldersArray { get; set; } = new string[0];
        
    }
}
