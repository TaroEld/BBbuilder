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
        readonly OptionFlag Template = new("-template", "Specify the tamplate you want to use depending of your mod objectives, or technologies");
        public InitCommand()
        {
            this.Name = "init";
            this.Description = "Initialises a new mod. Pass the name of the mod to be initialised.";
            this.Arguments = new string[]
            {
                "Mandatory: Specify name of the new mod. The new mod will be created in your specified 'mods' directory. (Example: bbuilder init mod_test)"
            };
            this.Flags = new OptionFlag[] { this.Replace, this.AltPath, this.Template };
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
            if(this.Replace && Directory.Exists(this.ModPath)){
                Console.WriteLine($"Directory '{this.ModPath}' already exists! Deleting existing folder...");
                Directory.Delete(this.ModPath, true);
            }

            if(!this.Template || this.Template.args.Count == 0)
            {
                Console.WriteLine($"No template specified. Using default template.");
                this.Template.Value = true;
                this.Template.args = new List<string> { "default" };
            }

            Console.WriteLine($"Use template : '{this.Template.args[0]}'");
            Utils.Copy("./Templates/" + this.Template.args[0], this.ModPath);
            replaceNamePlaceholder();
            replaceBBbuilderPlaceholder();
            Process.Start("explorer.exe", this.ModPath);
            return true;
        }

        private bool ParseCommand(List<string>_args)
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

        private void replaceNamePlaceholder(){
            string[] files = Directory.GetFiles(this.ModPath, "*.*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                text = text.Replace("$Name", this.ModName[0].ToString().ToUpper() + this.ModName.Substring(1));
                text = text.Replace("$name", this.ModName);
                File.WriteAllText(file, text);
            }

            string[] directories = Directory.GetDirectories(this.ModPath, "*.*", SearchOption.AllDirectories);
            foreach (string directory in directories)
            {
                string newDirectory = directory.Replace("$Name", this.ModName[0].ToString().ToUpper() + this.ModName.Substring(1));
                newDirectory = directory.Replace("$name", this.ModName);
                if(directory != newDirectory) Directory.Move(directory, newDirectory);
            }

            string[] files2 = Directory.GetFiles(this.ModPath, "*.*", SearchOption.AllDirectories);
            foreach (string file in files2)
            {
                string newFile = file.Replace("$Name", this.ModName[0].ToString().ToUpper() + this.ModName.Substring(1));
                newFile = file.Replace("$name", this.ModName);
                if(file != newFile) File.Move(file, newFile);
            }
        }

        private void replaceBBbuilderPlaceholder(){
            string[] files = Directory.GetFiles(this.ModPath, "*.*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                text = text.Replace("$bbbuild_path", Utils.EXECUTINGFOLDER.Replace("\\", "/") + "BBbuilder.exe");
                File.WriteAllText(file, text);
            }
        }
    }
}
