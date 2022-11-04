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
        static void LoadConfig()
        {
            while (!Directory.Exists(Properties.Settings.Default.GamePath))
            {
                Console.WriteLine("Please pass game path:");
                Properties.Settings.Default.GamePath = Console.ReadLine();
            }
            while (!Directory.Exists(Properties.Settings.Default.ModPath))
            {
                Console.WriteLine("Please pass mods path:");
                Properties.Settings.Default.ModPath = Console.ReadLine();
            }
        }

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
            LoadConfig();
            Dictionary<string, Command> Commands = new Dictionary<string, Command>();
            Commands["config"] = new ConfigCommand();
            Commands["build"] = new BuildCommand();
            Commands["init"] = new InitCommand();
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
                    return 1;
                }
            }
            return 0;
        }

    }
}
