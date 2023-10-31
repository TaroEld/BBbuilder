﻿using System;
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
            var watch = new System.Diagnostics.Stopwatch();
            watch.Start();

            Utils.GetJsonData();

            Dictionary<string, Command> Commands = new();
            
            Commands.Add("build", new BuildCommand());
            Commands.Add("init", new InitCommand());
            Commands.Add("extract", new ExtractCommand());
            var config = new ConfigCommand();
            config.SetupConfig();
            Commands.Add("config", config);

            string[] arguments = args;
            for (int x = 0; x < arguments.Length; x++)
            {
                if (arguments[x][0].ToString() == "-")
                {
                    Console.WriteLine($"Replaced argument '{arguments[x]}' with '{arguments[x][1..]}'.");
                    arguments[x] = arguments[x][1..];
                }
            }
            if (arguments == null || arguments.Length == 0)
            {
                Console.WriteLine($"No command passed, printing possible commands.\n");
                Utils.PrintHelp(Commands);
            }
            else if (!(Commands.ContainsKey(arguments[0])))
            {
                Console.WriteLine($"Command {arguments[0]} is not recognized! Printing possible commands.\n");
                Utils.PrintHelp(Commands);
            }
            else
            {
                Commands[arguments[0]].HandleCommand(arguments);
                Commands[arguments[0]].CleanUp();
                watch.Stop();
                Console.WriteLine($"Total Execution Time: {watch.ElapsedMilliseconds} ms");
                return 0;
            }
            watch.Stop();

            Console.WriteLine($"Total Execution Time: {watch.ElapsedMilliseconds} ms");
            return 1;
        }

    }
}
