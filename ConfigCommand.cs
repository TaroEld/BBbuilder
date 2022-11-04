using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BBbuilder
{
    class ConfigCommand : Command
    {
        public ConfigCommand()
        {
            this.Name = "config";
            this.Description = "Configure the settings that are used to create and build mods";
            this.Commands = new Dictionary<string, string>
            {
                {"gamepath", "Change path to the game directory" },
                {"modpath", "Change path to the mods directory" },
                {"folders", "todo" }
            };
        }
        public override bool HandleCommand(string[] _args)
        {
            if(_args.Length < 3)
            {
                PrintConfig();
                return false;
            }
            if (_args.Length < 3 || !Commands.ContainsKey(_args[1]))
            {
                PrintHelp();
                return false;
            }
            string command = _args[1];
            Console.WriteLine($"Passed command is {command}");
            if ((command == "gamepath" || command == "modpath"))
            {
                string passedPath = _args[2];
                if (!Directory.Exists(passedPath))
                {
                    Console.WriteLine($"Directory '{passedPath}' does not exist!");
                }
                if (command == "gamepath")
                {
                    Properties.Settings.Default.GamePath = passedPath;
                    Console.WriteLine($"Set gamepath to {passedPath}");
                }
                else
                {
                    Properties.Settings.Default.ModPath = passedPath;
                    Console.WriteLine($"Set modpath to {passedPath}");
                }
            }
            if (command == "folders")
            {
                Properties.Settings.Default.Folders = new StringCollection();
                var folderArgs = new ArraySegment<string>(_args, 2, _args.Length - 2);
                // Properties.Settings.Default.Folders.Clear();
                foreach (string line in folderArgs)
                {
                    Properties.Settings.Default.Folders.Add(line);
                    Console.WriteLine($"Added path {line} to folders to be added to build file.");
                }
            }
            Properties.Settings.Default.Save();
            //Console.WriteLine(Properties.Settings.Default.GamePath);
            //Console.WriteLine(Properties.Settings.Default.ModPath);
            foreach (string line in Properties.Settings.Default.Folders)
            {
                Console.WriteLine(line);
            }
            return true;
        }
        private void PrintConfig()
        {
            Console.WriteLine($"GamePath: {Properties.Settings.Default.GamePath}");
            Console.WriteLine($"Mods Path: {Properties.Settings.Default.ModPath}");
            Console.WriteLine("Project Folders:");
            foreach (string line in Properties.Settings.Default.Folders)
            {
                Console.WriteLine(line);
            }
        }
    }
}
