using System;
using System.Collections.Generic;


namespace BBbuilder
{
    // A simple class that allows for parsing of the passed arguments
    // Checks for the presence of a flag, sets a bool and removes the flag from the List
    class OptionFlag
    {
        bool Value;
        string Flag;
        string Description;
        public OptionFlag(string _flag, string _description)
        {
            this.Flag = _flag;
            this.Description = _description;
            this.Value = false;
        }
        public void Validate(List<string> _args)
        {
            int idx = _args.IndexOf(this.Flag);
            if (idx > 0)
            {
                _args.RemoveAt(idx);
                this.Value = true;
            }
            else
            {
                this.Value = false;
            }
        }
        public void PrintDescription()
        {
            Console.WriteLine($"{this.Flag} : {this.Description}");
        }

        // Allows instances to be used as simple bools.
        public static implicit operator bool(OptionFlag flag)
        {
            return flag.Value;
        }
    }
}
