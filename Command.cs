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
        public Dictionary<string, string> Commands = new();
        public String[] Arguments;
        virtual public bool HandleCommand(string[] _args)
        {
            if (_args.Length == 1)
            {
                Console.WriteLine($"No argument passed. Printing help of {this.Name}:");
                this.PrintHelp();
                return false;
            }
            return true;
        }
        void PrintCommands()
        {
            if (this.Commands.Count > 0)
            {
                Console.WriteLine("List of commands:");
                foreach (KeyValuePair<string, string> entry in this.Commands)
                {
                    Console.WriteLine($"{entry.Key} : {entry.Value}");
                }
            }
            if (this.Arguments != null && this.Arguments.Length > 0)
            {
                Console.WriteLine("List of arguments:");
                foreach (string entry in this.Arguments)
                {
                    Console.WriteLine(entry);
                }
            }
        }
        public void PrintHelp()
        {
            Console.WriteLine($"Printing help for command {this.Name}:");
            Console.WriteLine($"Description: {this.Description}");
            PrintCommands();
        }
    }
}
