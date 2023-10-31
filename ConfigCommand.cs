using System;
using System.Collections.Generic;
using System.IO;


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
                {"datapath", "Set path to the data directory. (For example: bbuilder config datapath G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data)"},
                {"modpath", "Set path to the mods directory. (For example: bbuilder config modpath G:/Games/BB/Mods/WIP)" },
                {"folders", "Add folders to be included in projects. Must be space. New mods added by the init command are automatically added. Pass nothing to remove folders." +
                " (For example: bbuilder config folders G:/Games/BB/Mods/WIP/mod_msu F:/MODDING/basegame/scripts)"},
                {"movezip", "Whether you want the final .zip file to be copied or moved to data. Default is 'false' (copy). Pass 'true' to move instead." }
            };
        }

        public void SetupConfig()
        {
            bool changed = false;
            while (!Directory.Exists(Utils.Data.GamePath))
            {
                Console.WriteLine("Please pass the path to the 'data' game directory. For example: G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data");
                string passedPath = Console.ReadLine();
                if (!ValidateDataPath(passedPath))
                    continue;
                Utils.Data.GamePath = passedPath;
                changed = true;
                Console.WriteLine($"datapath set to {passedPath}.");
            }
            while (!Directory.Exists(Utils.Data.ModPath))
            {
                Console.WriteLine("Please pass the path to the directory where you want new mods to be placed. For example: G:/Games/BB/Mods/WIP");
                string passedPath = Console.ReadLine();
                if (!Directory.Exists(passedPath))
                {
                    Console.WriteLine($"Directory {passedPath} does not exist!");
                     continue;
                }
                Utils.Data.ModPath = passedPath;
                changed = true;
                Console.WriteLine($"datapath set to {passedPath}.");
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
            if (!Commands.ContainsKey(_args[1]))
            {
                Console.WriteLine("Invalid subcommand of command 'config' passed. Printing help and current config:\n");
                PrintHelp();
                Console.WriteLine("");
                PrintConfig();
                return false;
            }
            string command = _args[1];
            if (command == "folders") HandleFolderCommand(_args);
            else if (command == "datapath" || command == "modpath") HandlePathCommand(_args);
            else if (command == "movezip") HandleMoveZipCommand(_args);
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

        private void HandlePathCommand(string[] _args)
        {
            if (_args.Length < 3)
            {
                Console.WriteLine("Invalid parameters passed. Printing help and current config:\n");
                PrintHelp();
                Console.WriteLine("");
                PrintConfig();
                return;
            }
            string passedPath = _args[2];
            if (!Directory.Exists(passedPath))
            {
                Console.WriteLine($"Directory '{passedPath}' does not exist!");
                return;
            }
            if (_args[1] == "datapath")
            {
                Utils.Data.GamePath = passedPath;
                Console.WriteLine($"Set datapath to {passedPath}");
            }
            else
            {
                Utils.Data.ModPath = passedPath;
                Console.WriteLine($"Set modpath to {passedPath}");
            }
        }


        private void HandleMoveZipCommand(string[] _args)
        {

            Utils.Data.MoveZip = Convert.ToBoolean(_args[2]);
        }


        private bool ValidateDataPath(string _path)
        {
            if (!Directory.Exists(_path))
            {
                Console.WriteLine($"Directory {_path} does not exist!");
                return false;
            }
            Console.WriteLine(new DirectoryInfo(_path).Name);
            if (new DirectoryInfo(_path).Name != "data")
            {
                Console.WriteLine($"Directory {_path} is not a data folder! Example path: G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data");
                return false;
            }
            return true;
        }

    }
}
