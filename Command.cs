using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BBBuilder
{
    public abstract class Command
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, string> Commands = new();
        public String[] Arguments;
        public OptionFlag[] Flags;

        protected Command()
        {
            Flags = GetOptionFlags();
        }

        private OptionFlag[] GetOptionFlags()
        {
            return this.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(f => f.FieldType == typeof(OptionFlag))
                .Select(f => (OptionFlag)f.GetValue(this))
                .ToArray();
        }

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
            foreach (OptionFlag flag in this.Flags)
            {
                flag.Validate(_args);
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
                Console.WriteLine("Arguments:");
                foreach (string entry in this.Arguments)
                {
                    Console.WriteLine("** " + entry);
                }
            }
            if (this.Flags.Length > 0)
            {
                Console.WriteLine("Flags:");
                foreach (OptionFlag flag in this.Flags)
                {
                    Console.WriteLine($"** {flag}");
                }
            }
        }
        public void PrintHelp()
        {
            Console.WriteLine($"**** Command '{this.Name}'");
            Console.WriteLine($"{this.Description}");
            PrintCommands();
        }

        virtual public void CleanUp(bool _ugly = false) { }
    }
}
