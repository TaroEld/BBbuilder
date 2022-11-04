using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BBbuilder
{
    abstract class Command
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> Commands { get; set; }

        virtual public bool HandleCommand(string[] _args)
        {
            return true;
        }
        void PrintCommands()
        {
            Console.WriteLine("List of commands:");
            foreach (KeyValuePair<string, string> entry in this.Commands)
            {
                Console.WriteLine($"Command: {entry.Key} : {entry.Value}");
            }
        }

        public void PrintHelp()
        {
            Console.WriteLine($"Command: {this.Name}");
            Console.WriteLine($"Description: {this.Description}");
            PrintCommands();
        }
    }
}
