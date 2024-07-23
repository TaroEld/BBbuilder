using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace BBbuilder
{
    class Program
    {
        static int Main(string[] args)
        {
            var version = "1.4";
            watch.Start();

            Utils.GetJsonData();

            Dictionary<string, Command> Commands = new()
            {
                { "start", new StartCommand()},
                { "build", new BuildCommand() },
                { "init", new InitCommand() },
                { "extract", new ExtractCommand() },
                { "config", new ConfigCommand() }
            };
            var config = (ConfigCommand)Commands["config"];
            config.SetupConfig();
            // exit early instead of printing the whole config
            if (!Debugger.IsAttached) {
                if (Utils.UpdatePathVariable())
                    return 1;
            }
            bool success = false;
            string[] arguments = args;
            if (arguments == null || arguments.Length == 0)
            {
                Console.WriteLine($"No command passed, printing possible commands.\n");
                Utils.PrintHelp(Commands);
            }
            else if (arguments[0] == "version")
            {
                Console.WriteLine($"BBBuilder version: {version}");
            }
            else if (!(Commands.ContainsKey(arguments[0])))
            {
                Console.WriteLine($"Command {arguments[0]} is not recognized! Printing possible commands.\n");
                Utils.PrintHelp(Commands);
            }
            else
            {
                success = Commands[arguments[0]].HandleCommand(arguments);
                Commands[arguments[0]].CleanUp(success);
            }
            watch.Stop();
            Console.WriteLine($"Total Execution Time: {watch.ElapsedMilliseconds} ms");
            return success ? 0 : 1;
        }

    }
}
