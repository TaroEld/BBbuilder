using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;

namespace BBBuilder
{
    public class InitCommand : Command
    {
        string ModName;
        string ModPath;
        string TemplatePath;
        public readonly OptionFlag Replace = new("-overwrite", "Overwrite the files in an existing folder. Keeps other files in the existing folder.");
        public readonly OptionFlag AltPath = new("-directory <path>", "Specify another folder to place the new mod. " +
            "\n    Example: `init mod_my_first_mod altpath \"C:\\BB Modding\\My_Mods\\\"` ");
        public readonly OptionFlag Template = new("-template <templatename>", " Specify which template to use. The template defines what files and folders will be created in the new mod directory. " +
            "\nThe default templates are found in the `Templates` folder within the .zip. You can customize these templates by either editing the existing ones, or adding new folders." +
            "\n    Example: 'bbbuilder init my_cool_mod -template ui");
        public InitCommand()
        {
            this.Name = "init";
            this.Description = "Create a new mod with the name `<modname>`. A folder structure is created, providing a light scaffold of folders and files. " +
                "\nThis speeds up the generation of new mods and provides consistency between your creations.  " +
                "\nBy default, the mod will be initialised into the folder specified in the `modpath` config value, with the foldername <modname>." +
                "\nThe generated folders and files depend on the template used, see the `-template` flag. ";
            this.Arguments = new string[]
            {
                "<modname>: Specify name of the new mod. The new mod will be created in your specified 'mods' directory. (Example: bbuilder init mod_test)"
            };
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
            if (Directory.Exists(this.ModPath) && !this.Replace)
            {
                Console.WriteLine($"Directory '{this.ModPath}' already exists! Use flag '-overwrite' to overwrite existing folder. Exiting to avoid mistakes...");
                return false;
            }

            if (!this.Template)
            {
                Console.WriteLine("No template specified, using 'default'.");
                this.TemplatePath = Path.Combine(Utils.EXECUTINGFOLDER, "Templates", "default");
            }
            else
            {
                this.TemplatePath = Path.Combine(Utils.EXECUTINGFOLDER, "Templates", this.Template.PositionalValue);
            }   
            if (!Directory.Exists(this.TemplatePath))
            {
                Console.WriteLine($"Template path {this.TemplatePath} does not exist! Exiting...");
                return false;
            }
            if (this.Replace && Directory.Exists(this.ModPath))
            {
                ReplaceFromTemplate();
            }
            else
            {
                CreateFromTemplate();
            }
            CreateExtraDirectories();
            WriteProjectFiles();
            if (Utils.IsGitInstalled())
            {
                if (Directory.Exists(Path.Combine(this.ModPath, ".git"))) {
                    Console.WriteLine("Git folder already exists, skipping...");
                } else { 
                    InitGitRepo(); 
                }
            }
                
            if (this.Template && this.Template.PositionalValue == "blank")
                File.Delete(Path.Combine(this.ModPath, "dummydel")); // VS doesn't copy the folder if it doesn't have a file in it...
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
                this.ModPath = Path.Combine(Utils.Norm(this.AltPath.PositionalValue), this.ModName);
            }
            else this.ModPath = Path.Combine(Utils.Data.ModPath, this.ModName);
            return true;
        }

        private void CreateExtraDirectories()
        {
            Console.WriteLine("Path of new mod: " + this.ModPath);
            Directory.CreateDirectory(Path.Combine(this.ModPath, ".vscode"));
            Directory.CreateDirectory(Path.Combine(this.ModPath, "assets"));
            Directory.CreateDirectory(Path.Combine(this.ModPath, "unpacked_brushes"));
        }

        public bool InitGitRepo()
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "init",
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    WorkingDirectory = this.ModPath
                };

                using (var process = Process.Start(processStartInfo))
                {
                    process.WaitForExit();
                }

                processStartInfo.Arguments = "add .";
                using (var process = Process.Start(processStartInfo))
                {
                    process.WaitForExit();
                }

                processStartInfo.Arguments = "commit -m \"initial commit\"";
                using (var process = Process.Start(processStartInfo))
                {
                    process.WaitForExit();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        private string GetNameSpaceName(string _name)
        {
            _name = _name[0].ToString().ToUpper() + _name[1..];
            while (_name.Contains("_"))
            {
                int idx = _name.IndexOf("_");
                if (idx + 1 == _name.Length)
                    return _name;
                else
                    _name = _name[0..idx] + _name[idx + 1].ToString().ToUpper() + _name[(idx + 2)..];
            }
            return _name;
        }
        private void CreateFromTemplate()
        {
            Utils.Copy(this.TemplatePath, this.ModPath);
            InitTemplateFiles(this.ModPath);
        }

        private void ReplaceFromTemplate()
        {
            string tempPath = this.ModPath + "_bbb_temp";
            Utils.Copy(this.TemplatePath, tempPath);
            InitTemplateFiles(tempPath);
            Utils.Copy(tempPath, this.ModPath);
            Directory.Delete(tempPath, true);
        }

        private void InitTemplateFiles(string _path)
        {
            string upperCaseName = this.ModName[0].ToString().ToUpper() + this.ModName[1..];
            string nameSpaceName = GetNameSpaceName(upperCaseName);
            string replaceNames(string input) => input
                .Replace("$namespace", nameSpaceName)
                .Replace("$modname", ModName)
                .Replace("$uppercase", upperCaseName);

            List<string> toRemove = new();

            foreach (string directory in Directory.GetDirectories(_path, "*.*", SearchOption.AllDirectories).OrderBy(d => d))
            {
                if (!Directory.Exists(directory)) continue;  // already renamed it previously
                string newDirectory = replaceNames(directory);
                if (directory != newDirectory && !Directory.Exists(newDirectory))
                {
                    Directory.CreateDirectory(newDirectory);
                    toRemove.Add(directory);    
                }
            }

            foreach (string fileName in Directory.GetFiles(_path, "*", SearchOption.AllDirectories).OrderByDescending(f => f))
            {
                File.WriteAllText(fileName, replaceNames(File.ReadAllText(fileName)));
                File.Move(fileName, replaceNames(fileName), true);
            }

            foreach (string dir in toRemove) {
                if (!Directory.Exists(dir)) continue;
                Directory.Delete(dir, true);
            }
        }

        private bool WriteProjectFiles()
        {
            bool hasSublime = Directory.GetFiles(this.ModPath, "*.sublime-project", SearchOption.AllDirectories).Length != 0;
            bool hasVS = Directory.GetFiles(this.ModPath, "*.code-workspace", SearchOption.AllDirectories).Length != 0;
            if (hasSublime && hasVS)
                return true;
            var foldersList = (Utils.Data.FoldersArray?.Select(line => new Folder { path = line }) ?? Enumerable.Empty<Folder>()).ToList();
            var options = new JsonSerializerOptions { WriteIndented = true };
            if (!hasSublime)
            {
                var sublimeProjectObject = new SublimeProject
                {
                    build_systems = Array.Empty<string>(),
                    folders = new List<Folder>(foldersList) { new Folder { path = "." } }
                };
                File.WriteAllText(Path.Combine(this.ModPath, this.ModName + ".sublime-project"), JsonSerializer.Serialize(sublimeProjectObject, options));
            } 
            else
            {
                Console.WriteLine("Found sublime-project file in template, skipping creation.");
            }

            if (!hasVS)
            {
                var vsCodeProjectObject = new VSCodeProject
                {
                    settings = Array.Empty<string>(),
                    folders = new List<Folder> { new Folder { path = ".." } }
                };
                // For vscode, the mod folder must come first
                vsCodeProjectObject.folders.AddRange(foldersList);

                File.WriteAllText(Path.Combine(this.ModPath, ".vscode", this.ModName + ".code-workspace"), JsonSerializer.Serialize(vsCodeProjectObject, options));
            }
            else
            {
                Console.WriteLine("Found sublime-project file in template, skipping creation.");
            }

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
