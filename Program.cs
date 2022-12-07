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
        static int Main(string[] args)
        {
            // Properties.Settings.Default.Reset();
            var watch = new System.Diagnostics.Stopwatch();

            watch.Start();

            Dictionary<string, Command> Commands = new();
            
            Commands.Add("build", new BuildCommand());
            Commands.Add("init", new InitCommand());
            Commands.Add("extract", new ExtractCommand());
            var config = new ConfigCommand();
            Commands.Add("config", config);
            config.SetupConfig();
            if (args == null || args.Length == 0)
            {
                Console.WriteLine($"Printing possible commands.\n");
                Utils.PrintHelp(Commands);
            }
            else if (!(Commands.ContainsKey(args[0])))
            {
                Console.WriteLine($"Command {args[0]} not recognized! Printing possible commands.\n");
                Utils.PrintHelp(Commands);
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
            watch.Stop();

            Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");
            return 0;
        }

    }
}
