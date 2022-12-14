using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Diagnostics;

namespace BBbuilder
{
    class InitCommand : Command
    {
        string ModName;
        string ModPath;
        public InitCommand()
        {
            this.Name = "init";
            this.Description = "Initialises a new mod. Pass the name of the mod to be initialised.";
            this.Arguments = new string[]
            {
                "Mandatory: Specify name of the new mod. The new mod will be created in your specified 'mods' directory. (Example: bbuilder init mod_test)",
                "Optional: Specify alternative path where the new mod will be created. (Example: bbuilder init mod_test C:/Users/user/Desktop/test/)",
            };
        }
        public override bool HandleCommand(string[] _args) 
        {
            if (!base.HandleCommand(_args))
            {
                return false;
            }
            if (!ParseCommand(_args))
            {
                return false;
            }
            if (Directory.Exists(this.ModPath))
            {
                Console.WriteLine($"Directory '{this.ModPath}' already exists! Exiting to avoid mistakes...");
                return false;
            }
            CreateDirectories();
            CreateTemplateFile();
            WriteProjectFiles();
            Process.Start("explorer.exe", this.ModPath);
            return true;
        }

        private bool ParseCommand(string[] _args)
        {
            if (_args[1].IndexOf(" ") != -1)
            {
                Console.WriteLine($"Found Space character in mod name {_args[1]}! Please don't do that. Exiting...");
                return false;
            }
            this.ModName = _args[1];
            if (_args.Length > 2)
            {
                if (!Directory.Exists(_args[2]))
                {
                    Console.WriteLine($"Passed alternative path {_args[2]} but this folder does not exist!");
                    return false;
                }
                this.ModPath = Path.Combine(_args[2], this.ModName);
            }
            else this.ModPath = Path.Combine(Properties.Settings.Default.ModPath, this.ModName);
            return true;
        }

        private void CreateDirectories()
        {
            Console.WriteLine("Path of new mod: " + this.ModPath);
            Directory.CreateDirectory(this.ModPath);
            Directory.CreateDirectory(Path.Combine(this.ModPath, ".vscode"));
            Directory.CreateDirectory(Path.Combine(this.ModPath, "assets"));
            Directory.CreateDirectory(Path.Combine(this.ModPath, "unpacked_brushes"));
            Directory.CreateDirectory(Path.Combine(this.ModPath, "scripts", "!mods_preload"));
        }

        private bool CreateTemplateFile()
        {
            string nutTemplate = Utils.ReadFile("BBbuilder.template_preload.nut");
            nutTemplate = nutTemplate.Replace("$name", this.ModName);
            string[] pathArray = new string[] { this.ModPath, "scripts", "!mods_preload", this.ModName + ".nut" };
            File.WriteAllText(Path.Combine(pathArray), nutTemplate);
            string gitignore = Utils.ReadFile("BBbuilder.gitignore_template");
            File.WriteAllText(Path.Combine(this.ModPath, ".gitignore"), gitignore);
            return true;
        }

        private bool WriteProjectFiles()
        {
            var sublimeProjectObject = new SublimeProject
            {
                build_systems = Array.Empty<string>(),
                folders = new List<Folder>()
            };
            var vsCodeProjectObject = new VSCodeProject
            {
                settings = Array.Empty<string>(),
                folders = new List<Folder>()
            };
            if (Properties.Settings.Default.Folders != null)
            {
                foreach (string line in Properties.Settings.Default.Folders)
                {
                    sublimeProjectObject.folders.Add(new Folder { path = line });
                    vsCodeProjectObject.folders.Add(new Folder { path = line });
                }
            }
            // Add mod folder too
            sublimeProjectObject.folders.Add(new Folder { path = "." });
            vsCodeProjectObject.folders.Add(new Folder { path = ".." });

            var options = new JsonSerializerOptions { WriteIndented = true };
            string sublimeJsonString = JsonSerializer.Serialize(sublimeProjectObject, options);
            string vscodeJsonString = JsonSerializer.Serialize(vsCodeProjectObject, options);
            File.WriteAllText(Path.Combine(this.ModPath, this.ModName + ".sublime-project"), sublimeJsonString);
            File.WriteAllText(Path.Combine(this.ModPath, ".vscode", this.ModName + ".code-workspace"), vscodeJsonString);
            return true;
        }
    }
    public class SublimeProject
    {
        public String[] build_systems { get; set; }

        public List<Folder> folders { get; set; }
    }

    public class VSCodeProject
    {
        public String[] settings { get; set; }

        public List<Folder> folders { get; set; }
    }

    public class Folder
    {
        public String path { get; set; }
    }
}
