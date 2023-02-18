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
            config.SetupConfig();
            Commands.Add("config", config);

            string[] arguments = args;
            // Debug arguments
            // string[] arguments = { "build", "G:/Games/BB/Mods/WIP/mod_reforged" };

            if (arguments == null || arguments.Length == 0)
            {
                Console.WriteLine($"Printing possible commands.\n");
                Utils.PrintHelp(Commands);
            }
            else if (!(Commands.ContainsKey(arguments[0])))
            {
                Console.WriteLine($"Command {arguments[0]} not recognized! Printing possible commands.\n");
                Utils.PrintHelp(Commands);
            }
            else
            {                   
                if (Commands[arguments[0]].HandleCommand(arguments))
                {
                    watch.Stop();

                    Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");
                    return 0;
                }
            }
            watch.Stop();

            Console.WriteLine($"Execution Time: {watch.ElapsedMilliseconds} ms");
            return 1;
        }

    }
}
