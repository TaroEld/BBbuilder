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
        readonly OptionFlag Replace = new("-replace", "Replace the files in an existing folder.");
        readonly OptionFlag AltPath = new("-alt", "Specify alternative path to extract the mod to.", true);
        public InitCommand()
        {
            this.Name = "init";
            this.Description = "Initialises a new mod. Pass the name of the mod to be initialised.";
            this.Arguments = new string[]
            {
                "Mandatory: Specify name of the new mod. The new mod will be created in your specified 'mods' directory. (Example: bbuilder init mod_test)"
            };
            this.Flags = new OptionFlag[] { this.Replace, this.AltPath };
        }
        public override bool HandleCommand(string[] _args)
        {
            if (!base.HandleCommand(_args))
            {
                return false;
            }
            if (!ParseCommand(_args.ToList()))
            {
                return false;
            }
            if (!this.Replace && Directory.Exists(this.ModPath))
            {
                Console.WriteLine($"Directory '{this.ModPath}' already exists! Use flag '-replace' to overwrite existing folder. Exiting to avoid mistakes...");
                return false;
            }
            CreateDirectories();
            CreateTemplateFile();
            WriteProjectFiles();
            Process.Start("explorer.exe", this.ModPath);
            return true;
        }

        private bool ParseCommand(List<string> _args)
        {
            this.ParseFlags(_args);
            if (_args[1].IndexOf(" ") != -1)
            {
                Console.WriteLine($"Found Space character in mod name {_args[1]}! Please don't do that. Exiting...");
                return false;
            }
            this.ModName = _args[1];
            if (this.AltPath)
            {
                if (!Directory.Exists(this.AltPath.PositionalValue))
                {
                    Console.WriteLine($"Passed alternative path {this.AltPath.PositionalValue} but this folder does not exist!");
                    return false;
                }
                this.ModPath = Path.Combine(this.AltPath.PositionalValue, this.ModName);
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
            Directory.CreateDirectory(Path.Combine(this.ModPath, "ui", "mods", this.ModName));
        }

        private bool CreateTemplateFile()
        {
            string nutTemplate = Utils.ReadFile("BBbuilder.template_preload.nut");
            nutTemplate = nutTemplate.Replace("$name", this.ModName);
            string[] pathArrayNut = new string[] { this.ModPath, "scripts", "!mods_preload", this.ModName + ".nut" };
            File.WriteAllText(Path.Combine(pathArrayNut), nutTemplate);

            string gitignore = Utils.ReadFile("BBbuilder.gitignore_template");
            File.WriteAllText(Path.Combine(this.ModPath, ".gitignore"), gitignore);

            string jsTemplate = Utils.ReadFile("BBbuilder.template_index.js");
            jsTemplate = jsTemplate.Replace("$name", this.ModName);
            jsTemplate = jsTemplate.Replace("$Name", this.ModName.First().ToString().ToUpper() + this.ModName.Substring(1));
            string[] pathArrayJs = new string[] { this.ModPath, "ui", "mods", this.ModName, "index.js"};
            File.WriteAllText(Path.Combine(pathArrayJs), jsTemplate);

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
            // For vscode, the mod folder must come first
            vsCodeProjectObject.folders.Add(new Folder { path = ".." });
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
