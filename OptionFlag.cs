using System;
using System.Collections.Generic;


namespace BBbuilder
{
    // A simple class that allows for parsing of the passed arguments
    // Checks for the presence of a flag, sets a bool and removes the flag from the List
    class OptionFlag
    {
        public bool Value;
        public string Flag;
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
            this.Description = _description;
            this.Value = false;
            
        }
        public void Validate(List<string> _args)
        {
            int idx = _args.IndexOf(this.Flag);
            if (idx > -1)
            {
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
                }
            }
            else
            {
                this.Value = false;
            }
        }

        // Allows instances to be used as simple bools.
        public static implicit operator bool(OptionFlag flag)
        {
            return flag.Value;
        }
    }
}
