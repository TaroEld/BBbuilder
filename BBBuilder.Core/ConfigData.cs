namespace BBBuilder
{
    public class ConfigData
    {
        public string GamePath { get; set; } = "";
        public string ModPath { get; set; } = "";
        public bool MoveZip { get; set; } = false;
        public bool UseSteam { get; set; } = false;
        public bool Verbose { get; set; } = false;
        public bool LogTime { get; set; } = false;
        public bool SetPath { get; set; } = true;
        public string[] FoldersArray { get; set; } = new string[0];
        
    }
}
