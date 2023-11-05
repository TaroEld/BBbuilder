using System;
using System.Collections.Generic;
using System.IO;


namespace BBbuilder
{
    class ConfigCommand : Command
    {
        readonly OptionFlag Clear = new("-clear", "Clears all settings.");
        public ConfigCommand()
        {
            this.Name = "config";
            this.Description = "Configure the settings that are used to create and build mods";
            this.Commands = new Dictionary<string, string>
            {
                {"-datapath <path>", "Set path to the directory of the game to copy the .zip of the mod to and optionally (re)start the game." +
                    "\n    Example: 'bbuilder config -datapath \"G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data\"'"},
                {"-modpath <path>", "Set the path to the directory of your mods folder, where newly initialised or extracted mods will be placed by default."+
                    "\n    Example: 'bbuilder config -modpath \"C:/BB Modding/My Mods\"'" },
                {"-folders <folderpath_1 folderpath_2 ...>", "Space-separated list of folders to be included in the editor config files (for example, adding the vanilla game files folder)." + 
                    "\n    Replaces the current list of folders.nothing to remove folders." +
                    "\n    A new mod created through init or extract is automatically added to its own workspace, so no need to specify it." +
                    "\n    Example: 'bbuilder config -folders \"C:/BB Modding/Other Mods/mod_msu\" \"C:/BB Modding/basegame/scripts\""},
                {"-movezip <true|false>", "Whether you'd like to delete the zip after building the mod and copying it to `datapath`." +
                    "\n    Example: 'bbbuilder config -movezip true'"}
            };
            this.Flags = new OptionFlag[]
            {
               this.Clear
            };
        }

        public void SetupConfig()
        {
            bool changed = false;
            while (!Directory.Exists(Utils.Data.GamePath))
            {
                Console.WriteLine("Please pass the path to the 'data' game directory. For example: G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data");
                string passedPath = Console.ReadLine();
                string[] args = { "", "-datapath", passedPath};
                changed = HandlePathCommand(args);
                if (changed)
                    Utils.Data.GamePath = passedPath;
            }
            while (!Directory.Exists(Utils.Data.ModPath))
            {
                Console.WriteLine("Please pass the path to the directory where you want new mods to be placed. For example: G:/Games/BB/Mods/WIP");
                string passedPath = Console.ReadLine();
                string[] args = { "", "-datapath", passedPath };
                changed = HandlePathCommand(args);
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
            Console.WriteLine($"Move Zip: {Utils.Data.MoveZip}");
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
            if (this.Clear)
            {
                this.HandleClearCommand(_args);
                Console.WriteLine("Cleared all config values.");
                return true;
            }
            string command = _args[1];
            if (!Commands.ContainsKey(command))
            {
                Console.WriteLine("Invalid subcommand of command 'config' passed. Printing help and current config:\n");
                PrintHelp();
                Console.WriteLine("");
                PrintConfig();
                return false;
            }
            if (command == "-folders") HandleFolderCommand(_args);
            else if (command == "-datapath" || command == "-modpath") HandlePathCommand(_args);
            else if (command == "-movezip") HandleMoveZipCommand(_args);
            PrintConfig();
            Utils.WriteJSON(Utils.Data);
            UpdateBuildFiles();
            return true;
        }

        private void HandleFolderCommand(string[] _args)
        {
            Utils.Data.Folders = "";
            if (_args.Length < 3)
            {
                Utils.Data.FoldersArray = Array.Empty<string>();
                Console.WriteLine("Cleared folders.");
            }
            else
            {
                Utils.Data.FoldersArray = _args[2..];
                foreach (string line in Utils.Data.FoldersArray)
                {
                    if (!Directory.Exists(line))
                        Console.WriteLine($"WARNING: Passed path {line} is not an existing folder!");
                    Utils.Data.Folders += line + ";";
                    Console.WriteLine($"Added path {line} to folders to be added to build file.");
                }
                Utils.Data.Folders = Utils.Data.Folders[0..^1];
            }
        }

        private bool HandlePathCommand(string[] _args)
        {
            string passedPath = _args[2];
            if (!Directory.Exists(passedPath))
            {
                Console.WriteLine($"Directory '{passedPath}' does not exist!");
                return false;
            }
            if (_args[1] == "-datapath")
            {
                if (new DirectoryInfo(passedPath).Name != "data")
                {
                    Console.WriteLine($"Directory {passedPath} is not a data folder! Example path: G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data");
                    return false;
                }
                Utils.Data.GamePath = passedPath;
                Console.WriteLine($"Set datapath to {passedPath}");
            }
            else
            {
                Utils.Data.ModPath = passedPath;
                Console.WriteLine($"Set modpath to {passedPath}");
            }
            return true;
        }


        private void HandleMoveZipCommand(string[] _args)
        {

            Utils.Data.MoveZip = Convert.ToBoolean(_args[2]);
        }

        private void HandleClearCommand(string[] _args)
        {
            Utils.CreateJSON();
        }

        private bool ValidateDataPath(string _path)
        {
            if (!Directory.Exists(_path))
            {
                Console.WriteLine($"Directory {_path} does not exist!");
                return false;
            }
            return true;
        }

    }
}
