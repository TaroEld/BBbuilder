using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Configuration;
using System.Collections.Specialized;
using System.Collections.Generic;

namespace BBbuilder
{
    class Program
    {
        static void Help(Dictionary<string, Command> _commands)
        {
            foreach (KeyValuePair<string, Command> entry in _commands)
            {
                entry.Value.PrintHelp();
            }
            return;
        }

        static int Main(string[] args)
        {
            var watch = new System.Diagnostics.Stopwatch();

            watch.Start();

            Dictionary<string, Command> Commands = new Dictionary<string, Command>();
            Commands["config"] = new ConfigCommand();
            Commands["build"] = new BuildCommand();
            Commands["init"] = new InitCommand();
            ConfigCommand.SetupConfig();
            if (!(args.Length == 0 || Commands.ContainsKey(args[0])))
            {
                Console.WriteLine("Command " + args[0] + " not recognized!");
                Help(Commands);
            }
            else if (args == null || args.Length == 0)
            {
                Help(Commands);
            }
            else
            {
                if (Commands[args[0]].HandleCommand(args))
                {
                    watch.Stop();

                    Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");
                    return 1;
                }
            }
            return 0;
        }

    }
}
