using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BBbuilder
{
    class BuildScriptsCommand : Command
    {
        public BuildScriptsCommand()
        {
            this.Name = "buildscripts";
            this.Description = "Build without including asset files. An alias for 'build <path> true false true'. The finished .zip will have a '_scripts' suffix.";
        }

        public override bool HandleCommand(string[] _args)
        {
            if (!base.HandleCommand(_args))
            {
                return false;
            }
            List<string> buildCommandArray = new List<string> { "build", _args[1], "true", "false", "true" };
            BuildCommand buildCommand = new();
            return buildCommand.HandleCommand(buildCommandArray.ToArray());
        }
    }
}
