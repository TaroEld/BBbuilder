using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BBbuilder
{
    class ConfigCommand : Command
    {
        readonly OptionFlag DataPath = new("-datapath <path>", "Set path to the directory of the game to copy the .zip of the mod to and optionally (re)start the game." +
                    "\n    Example: 'bbuilder config -datapath \"G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data\"'");
        readonly OptionFlag ModsPath = new("-modpath <path>", "Set the path to the directory of your mods folder, where newly initialised or extracted mods will be placed by default." +
                    "\n    Example: 'bbuilder config -modpath \"C:/BB Modding/My Mods\"'");
        readonly OptionFlag Folders = new("-folders <folderpath_1,folderpath_2 ...>", "Comma-separated list of folders to be included in the editor config files (for example, adding the vanilla game files folder)." +
                    "\n    Replaces the current list of folders.nothing to remove folders." +
                    "\n    A new mod created through init or extract is automatically added to its own workspace, so no need to specify it.");
        readonly OptionFlag MoveZip = new("-movezip <true|false>", "Whether you'd like to delete the zip after building the mod and copying it to `datapath`." +
                    "\n    Example: 'bbbuilder config -movezip true'");
        readonly OptionFlag UseSteam = new("-usesteam <true|false>", "Whether you'd like to start the game via Steam instead of the exe in the datapath. Needed for a patched .exe usinfg the MSU launcher. Requires Windows for now." +
            "\n    Example: 'bbbuilder config -usesteam true'");
        readonly OptionFlag Verbose = new("-verbose <true|false>", "Whether you'd like to print extra information" +
            "\n    Example: 'bbbuilder config -verbose true'");
        readonly OptionFlag LogTime = new("-logtime <true|false>", "Whether you'd like to print the time it takes the program to execute its different parts." +
            "\n    Example: 'bbbuilder config -logtime true'");

        readonly OptionFlag Clear = new("-clear", "Clears all settings.");
        public ConfigCommand()
        {
            this.Name = "config";
            this.Description = "Configure the settings that are used to create and build mods";
        }

        public void SetupConfig()
        {
            bool changed = false;
            while (!Directory.Exists(Utils.Data.GamePath))
            {
                Console.WriteLine("Please pass the path to the 'data' game directory. For example: G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data");
                string passedPath = Console.ReadLine();
                OptionFlag flag = new(this.DataPath.Flag + " " + this.DataPath.Parameter, this.DataPath.Description);
                flag.Validate(new List<string> { "-datapath", passedPath });
                changed = HandlePathCommand(flag);
                if (changed)
                    Utils.Data.GamePath = passedPath;
            }
            while (!Directory.Exists(Utils.Data.ModPath))
            {
                Console.WriteLine("Please pass the path to the directory where you want new mods to be placed. For example: G:/Games/BB/Mods/WIP");
                string passedPath = Console.ReadLine();
                OptionFlag flag = new(this.ModsPath.Flag + " " + this.ModsPath.Parameter, this.ModsPath.Description);
                flag.Validate(new List<string> { "-modpath", passedPath });
                changed = HandlePathCommand(flag);
                if (changed)
                    Utils.Data.ModPath = passedPath;
            }
            if (changed) Utils.WriteJSON(Utils.Data);
            UpdateBuildFiles();
        }


        private void UpdateBuildFiles()
        {
            string exeFile = Utils.EXEPATH.Replace("\\", "\\\\");
            string sqFile = Utils.SQPATH.Replace("\\", "\\\\");
            void setupFile(string _localPath, string _fileName, string _destinationPath)
            {
                string template = Utils.ReadFile("BBbuilder." + _localPath);
                template = template.Replace("$bbbuild_path", exeFile);
                template = template.Replace("$sq_path", sqFile);
                File.WriteAllText(Path.Combine(Utils.EXECUTINGFOLDER, "tools", _fileName), template);
                if (_destinationPath != "" && Directory.Exists(_destinationPath))
                {
                    File.Copy(Path.Combine(Utils.EXECUTINGFOLDER, "tools", _fileName), Path.Combine(_destinationPath, _fileName), true);
                }
            }
            setupFile("build_template_sublime", "bb_build.sublime-build", Path.Combine(new string[] { Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Sublime Text", "Packages", "User" }));
            setupFile("build_template_vs", "tasks.json", "");
        }

        public static void PrintConfig()
        {
            Console.WriteLine("Current config:");
            Console.WriteLine($"Data Path: {Utils.Data.GamePath}");
            Console.WriteLine($"Mods Path: {Utils.Data.ModPath}");
            Console.WriteLine("Project Folders:");
            foreach (string line in Utils.Data.FoldersArray)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine($"Use Steam: {Utils.Data.UseSteam}");
            Console.WriteLine($"Move Zip: {Utils.Data.MoveZip}");
            Console.WriteLine($"Verbose: {Utils.Data.Verbose}");
            Console.WriteLine($"Log Time: {Utils.Data.LogTime}");
        }

        public override bool HandleCommand(string[] _args)
        {
            if (_args.Length == 1)
            {
                PrintHelp();
                PrintConfig();
                return false;
            }
            this.ParseFlags(new List<string>(_args));
            if (!this.Flags.Where(c => c).Any())
            {
                Console.WriteLine("No valid flags passed!\n");
                PrintHelp();
                Console.WriteLine("");
                PrintConfig();
                return false;
            }
            if (this.Clear)
            {
                this.HandleClearCommand();
                return true;
            }
            if (this.DataPath)
            {
                HandlePathCommand(this.DataPath);
            }
            if (this.ModsPath)
            {
                HandlePathCommand(this.ModsPath);
            }
            if (this.Folders)
            {
                HandleFolderCommand(this.Folders);
            }
            if (this.MoveZip)
            {
                HandleBooleanCommand(this.MoveZip, value => Utils.Data.MoveZip = value);
            }
            if (this.UseSteam)
            {
                HandleBooleanCommand(this.UseSteam, value => Utils.Data.UseSteam = value);
            }
            if (this.Verbose)
            {
                HandleBooleanCommand(this.Verbose, value => Utils.Data.Verbose = value);
            }
            if (this.LogTime)
            {
                HandleBooleanCommand(this.LogTime, value => Utils.Data.LogTime = value);
            }
            PrintConfig();
            Utils.WriteJSON(Utils.Data);
            UpdateBuildFiles();
            return true;
        }

        private void HandleFolderCommand(OptionFlag flag)
        {
            string[] folders = flag.PositionalValue.Split(',').Select(f => Utils.Norm(f)).ToArray();
            if (folders.Length == 0)
            {
                Utils.Data.FoldersArray = Array.Empty<string>();
                Console.WriteLine("Cleared folders.");
            }
            else
            {
                Utils.Data.FoldersArray = folders;
                foreach (string line in Utils.Data.FoldersArray)
                {
                    if (!Directory.Exists(line))
                        Console.WriteLine($"WARNING: Passed path {line} is not an existing folder!");
                    Console.WriteLine($"Added path {line} to folders to be added to build file.");
                }
            }
        }

        private bool HandlePathCommand(OptionFlag _flag)
        {
            if (!Directory.Exists(_flag.PositionalValue))
            {
                Console.WriteLine($"Directory '{_flag.PositionalValue}' does not exist!");
                return false;
            }
            if (_flag.Flag == "-datapath")
            {
                if (new DirectoryInfo(_flag.PositionalValue).Name != "data")
                {
                    Console.WriteLine($"Directory {_flag.PositionalValue} is not a data folder! Example path: G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data");
                    return false;
                }
                Utils.Data.GamePath = Utils.Norm(_flag.PositionalValue);
                Console.WriteLine($"Set datapath to {Utils.Data.GamePath}");
            }
            else
            {
                Utils.Data.ModPath = Utils.Norm(_flag.PositionalValue);
                Console.WriteLine($"Set modpath to {Utils.Data.ModPath}");
            }
            return true;
        }

        private void HandleBooleanCommand(OptionFlag flag, Action<bool> setter)
        {
            if (flag.PositionalValue != "true" && flag.PositionalValue != "false")
            {
                Console.WriteLine($"You need to pass either 'true' or 'false' to {flag.Flag} !");
                return;
            }

            bool value = Convert.ToBoolean(flag.PositionalValue);
            setter(value);
            Console.WriteLine($"Set {flag.Flag} to {value}.");
        }

        private void HandleClearCommand()
        {
            Utils.CreateJSON();
            Console.WriteLine("Cleared all config values.");
        }
    }
}
