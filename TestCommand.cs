using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BBbuilder
{
    class TestCommand : Command
    {
        readonly OptionFlag Bench = new("-bench", "Bench running");
        public TestCommand()
        {
            this.Name = "test";
            this.Description = "Internal Testing";
        }

        public override bool HandleCommand(string[] _args)
        {
            this.RunBench(_args[1..]);
            return true;
        }

        private void RunBench(string[] _args)
        {
            int iterNum = 10;
            double totalTimeElapsed = 0.0;
            for (int i = 0; i < iterNum; i++)
            {
                var build = new BuildCommand();
                var watch = new System.Diagnostics.Stopwatch();
                watch.Start();
                bool restult = build.HandleCommand(_args);
                watch.Stop();
                totalTimeElapsed += watch.ElapsedMilliseconds;
                Console.WriteLine($"Iteration {i} stopped with result {restult} after {watch.ElapsedMilliseconds}");
            }
            Console.WriteLine($"Stopping bench, total time elapsed {totalTimeElapsed}, avg. = {totalTimeElapsed / iterNum} ");
        }
    }
}
