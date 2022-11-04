using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BBbuilder
{
    class InitCommand : Command
    {
        string ModName;
        string ModPath;
        public InitCommand()
        {
            this.Name = "init";
            this.Description = "init";
            this.Commands = new Dictionary<string, string>
            {
                {"path", "Specify alternative path to put mod" }
            };
        }
        public override bool HandleCommand(string[] _args) 
        {
            if (_args.Length < 2)
            {
                Console.WriteLine("Missing name of new mod!");
                return false;
            }
            string modName = _args[1];

            if(!ValidateModname())
            {
                return false;
            }
            this.ModName = modName;
            this.ModPath = $"{Properties.Settings.Default.ModPath}/{modName}";
            Console.WriteLine("ModPath: " + this.ModPath);
            if (!CreateDirectories())
            {
                return false;
            }
            CreateTemplateFile();
            WriteProjectFiles();
            return true;
        }

        private bool CreateDirectories()
        {
            if (Directory.Exists(this.ModPath))
            {
                Console.WriteLine($"Directory '{this.ModPath}' already exists!");
                return false;
            }
            Directory.CreateDirectory(this.ModPath);
            Console.WriteLine($"Created folder {this.ModPath}");
            Directory.CreateDirectory($"{this.ModPath}/.vscode");
            Directory.CreateDirectory($"{this.ModPath}/assets");
            Directory.CreateDirectory($"{this.ModPath}/scripts/mods_preload/");
            return true;
        }

        private bool CreateTemplateFile()
        {
            string nutTemplate = ReadFile("BBbuilder.template_preload.nut");
            nutTemplate = nutTemplate.Replace("$name", this.ModName);
            File.WriteAllText($"{this.ModPath}/scripts/mods_preload/{this.ModName}.nut", nutTemplate);
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
            foreach (string line in Properties.Settings.Default.Folders)
            {
                Console.WriteLine("Line?" + line);
                Folder folder = new Folder
                {
                    path = line
                };
                sublimeProjectObject.folders.Add(folder);
                vsCodeProjectObject.folders.Add(folder);
            }
            var options = new JsonSerializerOptions { WriteIndented = true };
            string sublimeJsonString = JsonSerializer.Serialize(sublimeProjectObject, options);
            string vscodeJsonString = JsonSerializer.Serialize(vsCodeProjectObject, options);
            Console.WriteLine(vscodeJsonString);
            File.WriteAllText($"{this.ModPath}/{this.ModName}.sublime-project", sublimeJsonString);
            File.WriteAllText($"{this.ModPath}/.vscode/{this.ModName}.code-workspace", vscodeJsonString);
            return true;
        }

        private bool ValidateModname()
        {
            return true;
        }

        private string ReadFile(string _path)
        {
            string fileAsString;
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(_path))
            using (StreamReader reader = new StreamReader(stream))
            {
                fileAsString = reader.ReadToEnd();
            }
            return fileAsString;
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
