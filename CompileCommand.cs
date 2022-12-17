using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BBbuilder
{
    class CompileCommand : Command
    {
        public CompileCommand()
        {
            this.Name = "compile";
            this.Description = "Compile all .nut files to test for syntax errors.";
            this.Arguments = new string[]
            {
                "Mandatory: Specify the path of the mod to be compiled. (Example: bbuilder compile G:/Games/BB/Mods/WIP/mod_msu)",
            };
        }

        public override bool HandleCommand(string[] _args)
        {
            if (!base.HandleCommand(_args))
            {
                return false;
            }
            List<string> buildCommandArray = new List<string> { "build", _args[1], "false", "true" };
            BuildCommand buildCommand = new();
            return buildCommand.HandleCommand(buildCommandArray.ToArray());
        }
    }
}
