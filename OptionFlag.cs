using System;
using System.Collections.Generic;
using System.Xml.Linq;


namespace BBbuilder
{
    // A simple class that allows for parsing of the passed arguments
    // Checks for the presence of a flag, sets a bool and removes the flag from the List
    class OptionFlag
    {
        public bool Value;
        public string Flag;
        public string FlagAlias;
        public readonly string Parameter;
        public readonly string Description;
        readonly bool Positional;
        public string PositionalValue;
        public OptionFlag(string _flag, string _description)
        {
            if (_flag.Split(" ").Length > 1)
            {
                this.Flag = _flag.Split(" ")[0];
                this.Parameter = _flag.Split(" ")[1];
                this.Positional = true;
            }
            else
            {
                this.Flag = _flag;
                this.Positional = false;
            }
            this.FlagAlias = this.Flag.Substring(0, 2);
            this.Description = _description;
            this.Value = false;
        }
        public void Validate(List<string> _args)
        {
            int idx = _args.IndexOf(this.Flag);
            if (idx == -1)
            {
                idx = _args.IndexOf(this.FlagAlias);
            }
            if (idx == -1)
            {
                this.Value = false;
                return;
            }
            _args.RemoveAt(idx);
            this.Value = true;
            if (this.Positional)
            {
                if (_args.Count < idx)
                {
                    Console.WriteLine($"Passed positional flag {this.Flag} but no fitting argument found!");
                    throw new Exception();
                }
                this.PositionalValue = _args[idx];
                _args.RemoveAt(idx);
                Console.WriteLine($"Value of {this.Flag}: {this.PositionalValue}");
            }
            else
            {
                Console.WriteLine($"Value of {this.Flag}: {this.Value}");
            }
        }

        public override string ToString()
        {
            return $"'{Flag} (alias: {FlagAlias}) {Parameter}': {Description}";
        }

        // Allows instances to be used as simple bools.
        public static implicit operator bool(OptionFlag flag)
        {
            return flag.Value;
        }
    }
}
