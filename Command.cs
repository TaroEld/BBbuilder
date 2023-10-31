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
        public OptionFlag[] Flags;
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

        public void ParseFlags(List<string> _args)
        {
            if (this.Flags != null && this.Flags.Length > 0)
            {
                foreach (OptionFlag flag in this.Flags)
                {
                    flag.Validate(_args);
                }
            }
        }

        void PrintCommands()
        {
            if (this.Commands.Count > 0)
            {
                Console.WriteLine("List of subcommands:");
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
            if (this.Flags != null && this.Flags.Length > 0)
            {
                Console.WriteLine("List of flags:");
                foreach (OptionFlag flag in this.Flags)
                {
                    flag.PrintDescription();
                }
            }
        }
        public void PrintHelp()
        {
            Console.WriteLine($"Printing help for command {this.Name}:");
            Console.WriteLine($"Description: {this.Description}");
            PrintCommands();
        }

        public void CleanUp() { }
    }
}
