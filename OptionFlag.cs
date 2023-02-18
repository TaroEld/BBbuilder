using System;
using System.Collections.Generic;


namespace BBbuilder
{
    // A simple class that allows for parsing of the passed arguments
    // Checks for the presence of a flag, sets a bool and removes the flag from the List
    class OptionFlag
    {
        bool Value;
        public string Flag;
        string Description;
        bool Positional;
        public string PositionalValue;
        public OptionFlag(string _flag, string _description, bool _positional = false)
        {
            this.Flag = _flag;
            this.Description = _description;
            this.Value = false;
            this.Positional = _positional;
        }
        public void Validate(List<string> _args)
        {
            int idx = _args.IndexOf(this.Flag);
            if (idx > 0)
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
        public void PrintDescription()
        {
            string positional = this.Positional ? "(positional)" : "";
            Console.WriteLine($"{this.Flag} {positional}: {this.Description}");
        }

        // Allows instances to be used as simple bools.
        public static implicit operator bool(OptionFlag flag)
        {
            return flag.Value;
        }
    }
}
