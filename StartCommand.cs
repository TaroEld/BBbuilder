using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BBBuilder
{
    public class StartCommand : Command
    {
        public StartCommand()
        {
            this.Name = "start";
            this.Description = "Restarts the game without doing anything else.";
        }

        public override bool HandleCommand(string[] _args)
        {
            return Utils.KillAndStartBB();
        }
    }
};
