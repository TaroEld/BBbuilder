using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using Microsoft.Data.Sqlite;

namespace BBbuilder
{
    class ConfigCommand : Command
    {
        private SqliteConnection Connection;
        public ConfigCommand()
        {
            this.Name = "config";
            this.Description = "Configure the settings that are used to create and build mods";
            this.Connection = new SqliteConnection("Data Source=./tools/settings.sqlite");
            this.Commands = new Dictionary<string, string>
            {
                {"datapath", "Set path to the data directory. (For example: bbuilder config datapath G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data)"},
                {"modpath", "Set path to the mods directory. (For example: bbuilder config modpath G:/Games/BB/Mods/WIP)" },
                {"folders", "Add folders to be included in projects. Must be space. New mods added by the init command are automatically added. Pass nothing to remove folders." +
                " (For example: bbuilder config folders G:/Games/BB/Mods/WIP/mod_msu F:/MODDING/basegame/scripts)"},
                {"movezip", "Whether you want the final .zip file to be copied or moved to data. Default is 'false' (copy). Pass 'true' to move instead." }
            };
        }

        public string getValueFromDB(string _id)
        {
            this.Connection.Open();
            var command = this.Connection.CreateCommand();
            string ret = "";
            command.CommandText =
            @"
                SELECT value
                FROM settings
                WHERE id = $id
            ";
            command.Parameters.AddWithValue("$id", _id);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    ret = reader.GetString(0);
                }
            }
            this.Connection.Close();
            return ret;
        }
        public void WriteValueToDb(string _id, string _value)
        {
            this.Connection.Open();
            var command = this.Connection.CreateCommand();
            command.CommandText =
            @"
                UPDATE settings
                SET value = $value
                WHERE id = $id
            ";
            command.Parameters.AddWithValue("$id", _id);
            command.Parameters.AddWithValue("$value", _value);
            command.ExecuteNonQuery();
            this.Connection.Close();
        }

        public void SetupConfig()
        {
            Utils.GamePath = getValueFromDB("GamePath");
            Utils.ModPath = getValueFromDB("ModPath");
            Utils.Folders = getValueFromDB("Folders");
            Utils.FoldersArray = Utils.Folders.Split(";");
            Utils.MoveZip = bool.Parse(getValueFromDB("MoveZip"));

            while (!Directory.Exists(Utils.GamePath))
            {
                Console.WriteLine("Please pass the path to the 'data' game directory. For example: G:/Games/SteamLibrary/steamapps/common/Battle Brothers/data");
                string passedPath = Console.ReadLine();
                if (!ValidateDataPath(passedPath))
                    continue;
                Utils.GamePath = passedPath;
                Console.WriteLine($"datapath set to {passedPath}.");
                WriteValueToDb("GamePath", Utils.GamePath);
            }
            while (!Directory.Exists(Utils.ModPath))
            {
                Console.WriteLine("Please pass the path to the directory where you want new mods to be placed. For example: G:/Games/BB/Mods/WIP");
                string passedPath = Console.ReadLine();
                if (!Directory.Exists(passedPath))
                {
                    Console.WriteLine($"Directory {passedPath} does not exist!");
                     continue;
                }
                Utils.ModPath = passedPath;
                Console.WriteLine($"datapath set to {passedPath}.");
                WriteValueToDb("ModPath", Utils.ModPath);
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
            Console.WriteLine($"Data Path: {Utils.GamePath}");
            Console.WriteLine($"Mods Path: {Utils.ModPath}");
            Console.WriteLine("Project Folders:");
            foreach (string line in Utils.FoldersArray)
            {
                Console.WriteLine(line);
            }
            Console.WriteLine($"Move Zip: {Utils.MoveZip}");
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
            UpdateBuildFiles();
            return true;
        }

        private void HandleFolderCommand(string[] _args)
        {
            Utils.Folders = "";
            if (_args.Length < 3)
            {
                Utils.FoldersArray = Array.Empty<string>();
                Console.WriteLine("Cleared folders.");
            }
            else
            {
                Utils.FoldersArray = _args[2..];
                foreach (string line in Utils.FoldersArray)
                {
                    if (!Directory.Exists(line))
                        Console.WriteLine($"WARNING: Passed path {line} is not an existing folder!");
                    Utils.Folders += line + ";";
                    Console.WriteLine($"Added path {line} to folders to be added to build file.");
                }
                Utils.Folders = Utils.Folders[0..^1];
            }
            WriteValueToDb("Folders", Utils.Folders);
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
                Utils.GamePath = passedPath;
                WriteValueToDb("GamePath", passedPath);
                Console.WriteLine($"Set datapath to {passedPath}");
            }
            else
            {
                Utils.ModPath = passedPath;
                WriteValueToDb("ModPath", passedPath);
                Console.WriteLine($"Set modpath to {passedPath}");
            }
        }


        private void HandleMoveZipCommand(string[] _args)
        {

            Utils.MoveZip = Convert.ToBoolean(_args[2]);
            WriteValueToDb("MoveZip", _args[2]);
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
