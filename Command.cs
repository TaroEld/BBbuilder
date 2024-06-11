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
            if (_args.Count > 2)
            {
                Console.WriteLine("Unknown args/flags found:");
                foreach (string arg in _args.ToArray()[2..])
                    Console.WriteLine(arg);
            }
        }

        void PrintCommands()
        {
            if (this.Arguments != null && this.Arguments.Length > 0)
            {
                Console.WriteLine("List of arguments:");
                foreach (string entry in this.Arguments)
                {
                    Console.WriteLine("*** " + entry);
                }
            }
            if (this.Flags != null && this.Flags.Length > 0)
            {
                Console.WriteLine("List of flags:");
                foreach (OptionFlag flag in this.Flags)
                {
                    Console.WriteLine($"** '{flag.Flag} {flag.Parameter}': {flag.Description}");
                }
            }
        }
        public void PrintHelp()
        {
            Console.WriteLine($"**** Printing help for command '{this.Name}'");
            Console.WriteLine($"Description: {this.Description}\n");
            PrintCommands();
        }

        virtual public void CleanUp(bool _ugly = false) { }
    }
}
