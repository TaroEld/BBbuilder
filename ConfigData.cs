using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BBbuilder
{
    public class ConfigData
    {
        public string GamePath { get; set; } = "";
        public string ModPath { get; set; } = "";
        public string Folders { get; set; } = "";
        public bool MoveZip { get; set; } = false;
        public string[] FoldersArray { get; set; } = new string[0];
        
    }
}
