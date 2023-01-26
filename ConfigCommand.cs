using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
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
                {"datapath", "Set path to the data directory. (For example: bbuilder config datapath G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data)"},
                {"modpath", "Set path to the mods directory. (For example: bbuilder config modpath G:/Games/BB/Mods/WIP)" },
                {"folders", "Add folders to be included in projects. Must be space. New mods added by the init command are automatically added." +
                " (For example: bbuilder config folders G:/Games/BB/Mods/WIP/mod_msu F:/MODDING/basegame/scripts)"},
                {"movezip", "Whether you want the final .zip file to be copied or moved to data. Default is 'false' (copy). Pass 'true' to move instead." }
            };
        }

        public void SetupConfig()
        {
            while (!Directory.Exists(Properties.Settings.Default.GamePath))
            {
                Console.WriteLine("Please pass the path to the 'data' game directory. For example: G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data");
                string passedPath = Console.ReadLine();
                if (!ValidateDataPath(passedPath))
                    continue;
                Properties.Settings.Default.GamePath = passedPath;
                Console.WriteLine($"datapath set to {passedPath}.");
                Properties.Settings.Default.Save();
            }
            while (!Directory.Exists(Properties.Settings.Default.ModPath))
            {
                Console.WriteLine("Please pass the path to the directory where you want new mods to be placed. For example: G:/Games/BB/Mods/WIP");
                string passedPath = Console.ReadLine();
                if (!Directory.Exists(passedPath))
                {
                    Console.WriteLine($"Directory {passedPath} does not exist!");
                     continue;
                }
                Properties.Settings.Default.ModPath = passedPath;
                Console.WriteLine($"datapath set to {passedPath}.");
                Properties.Settings.Default.Save();
            }
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
                if (_destinationPath != "" && Directory.Exists(Path.Combine(_destinationPath, _fileName)))
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
            Console.WriteLine($"Data Path: {Properties.Settings.Default.GamePath}");
            Console.WriteLine($"Mods Path: {Properties.Settings.Default.ModPath}");
            if (Properties.Settings.Default.Folders == null)
            {
                Console.WriteLine("No Project Folders defined");
            }
            else
            {
                Console.WriteLine("Project Folders:");
                foreach (string line in Properties.Settings.Default.Folders)
                {
                    Console.WriteLine(line);
                }
            }
        }
        public override bool HandleCommand(string[] _args)
        {
            if (_args.Length < 3 || !Commands.ContainsKey(_args[1]))
            {
                Console.WriteLine("Invalid command passed. Printing help and current config:\n");
                PrintHelp();
                Console.WriteLine("");
                PrintConfig();
                return false;
            }
            string command = _args[1];
            Console.WriteLine($"Passed command is {command}");
            if ((command == "datapath" || command == "modpath"))
            {
                string passedPath = _args[2];
                if (!Directory.Exists(passedPath))
                {
                    Console.WriteLine($"Directory '{passedPath}' does not exist!");
                }
                if (command == "datapath")
                {
                    Properties.Settings.Default.GamePath = passedPath;
                    Console.WriteLine($"Set datapath to {passedPath}");
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
                var folderArgs = new ArraySegment<string>(_args, 2, _args.Length-2);
                // Properties.Settings.Default.Folders.Clear();
                foreach (string line in folderArgs)
                {
                    if (!Directory.Exists(line))
                        Console.WriteLine($"WARNING: Passed path {line} is not an existing folder!");
                    Properties.Settings.Default.Folders.Add(line);
                    Console.WriteLine($"Added path {line} to folders to be added to build file.");
                }
            }
            if (command == "movezip")
            {
                Properties.Settings.Default.MoveZip = Convert.ToBoolean(_args[2]);
            }
            Properties.Settings.Default.Save();
            PrintConfig();
            UpdateBuildFiles();
            return true;
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
