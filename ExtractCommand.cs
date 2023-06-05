using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BBbuilder
{
    class ExtractCommand : Command
    {
        string ModPath;
        string ModName;
        string ZipPath;
        readonly OptionFlag Replace = new("-replace", "Replace the files in an existing folder.");
        readonly OptionFlag Rename = new("-rename", "Renames the extracted mod.", true);
        readonly OptionFlag AltPath = new("-alt", "Specify alternative path to extract the mod to.", true);
        List<string> InitCommandArray;
        public ExtractCommand()
        {
            this.Name = "extract";
            this.Description = "Extract an existing mod to a new or specified directory";
            this.Arguments = new String[]
            {
                "Mandatory: Specify path of mod to extract. The file will be put in your specified 'mods' directory. (Example: bbuilder extract C:/Users/user/Desktop/mod_test.zip)"
            };
            this.InitCommandArray = new List<string>();
            this.Flags = new OptionFlag[] { this.Replace, this.Rename, this.AltPath }; 
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

            // Create new folder with the initcommand, where the files will be extracted to
            bool directoryExisted = Directory.Exists(this.ModPath);
            InitCommand initCommand = new();
            if (!initCommand.HandleCommand(this.InitCommandArray.ToArray()))
            {
                Console.WriteLine("Error while creating new folder! Exiting...");
                return false;
            }

            // Set replace value to true if this is a newly created directory, to overwrite things like scripts/ or .gitignore
            if (!directoryExisted)
            {
                this.Replace.Value = true;
            }

            if (!ExtractZip())
            {
                Console.WriteLine("Error while extracting zip! Exiting...");
                return false;
            }

            DecompileFiles();
            ExtractBrushes();

            return true;
        }

        private bool ParseCommand(List<string> _args)
        {
            this.ParseFlags(_args);
            if (!File.Exists(_args[1]))
            {
                Console.WriteLine($"Passed path to extract: {_args[1]} but this file does not exist!");
                return false;
            }
            this.ZipPath = _args[1];
            Console.WriteLine($"Extracting zip {this.ZipPath}");
            this.InitCommandArray.Add("init");

            this.ModName = this.Rename ? this.Rename.PositionalValue : Path.GetFileNameWithoutExtension(this.ZipPath);
            this.ModPath = Path.Combine(Properties.Settings.Default.ModPath, this.ModName);
            this.InitCommandArray.Add(this.ModName);
            if (this.AltPath)
            {
                if (!Directory.Exists(this.AltPath.PositionalValue))
                {
                    Console.WriteLine($"Passed alternative path {this.AltPath.PositionalValue} but this folder does not exist!");
                    return false;
                }
                this.ModPath = Path.Combine(this.AltPath.PositionalValue, this.ModName);
                this.InitCommandArray.Add(this.AltPath.Flag);
                this.InitCommandArray.Add(this.AltPath.PositionalValue);
            }
            if (this.Replace)
                this.InitCommandArray.Add("-replace");
            return true;
        }

        private bool ExtractZip()
        {
            try
            {
                ZipFile.ExtractToDirectory(this.ZipPath, this.ModPath, this.Replace);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                string ending = this.ZipPath.Substring(this.ZipPath.Length - 4);
                if (ending != ".zip")
                {
                    Console.WriteLine($"This does not look like a zip file! Ending characters: '{ending}'. Make sure you pass a .zip!");
                }
                return false;
            }
            return true;
        }

        private bool DecompileFiles()
        {
            string[] allCnutFilesAsPath = Directory.GetFiles(Path.Combine(this.ModPath, "scripts"), "*.cnut", SearchOption.AllDirectories);
            string decompileOutput = "";
            if (allCnutFilesAsPath.Length == 0)
            {
                Console.WriteLine("No files to decompile.");
                return true;
            }
            Console.WriteLine("Starting to decompile files.");
            Parallel.For(0, allCnutFilesAsPath.Length, (i, state) =>
            {
                string cnutFilePath = allCnutFilesAsPath[i];
                string nutFilePath = Path.ChangeExtension(cnutFilePath, ".nut");
                string bbsqCommand = String.Format("-d \"{0}\"", cnutFilePath);
                string nutcrackerCommand = $"\"{cnutFilePath}\"";

                using (Process decrypting = new Process())
                {
                    decrypting.StartInfo.UseShellExecute = false;
                    decrypting.StartInfo.RedirectStandardOutput = true;
                    decrypting.StartInfo.FileName = Utils.BBSQPATH;
                    decrypting.StartInfo.Arguments = bbsqCommand;
                    decrypting.Start();
                    decrypting.WaitForExit();
                    if (decrypting.ExitCode == -2)
                    {
                        Console.WriteLine($"Error decrypting file {cnutFilePath}!");
                        StreamReader myStreamReader = decrypting.StandardOutput;
                        Console.WriteLine(myStreamReader.ReadLine());
                        return;
                    }
                }

                using (Process decompiling = new Process())
                {
                    decompiling.StartInfo.UseShellExecute = false;
                    decompiling.StartInfo.RedirectStandardOutput = true;
                    decompiling.StartInfo.FileName = Utils.NUTCRACKERPATH;
                    decompiling.StartInfo.Arguments = nutcrackerCommand;
                    decompiling.Start();

                    // Adapted from https://stackoverflow.com/a/16256623
                    using (StreamWriter writer = File.CreateText(nutFilePath))
                    using (StreamReader reader = decompiling.StandardOutput)
                    {
                        writer.AutoFlush = true;

                        for (; ; )
                        {
                            string textLine = reader.ReadLine();

                            if (textLine == null)
                                break;

                            writer.WriteLine(textLine);
                        }
                    }

                    decompiling.WaitForExit();
                    if (decompiling.ExitCode == -2)
                    {
                        Console.WriteLine($"Error decompiling file {cnutFilePath}!");
                        return;
                    }
                }
                decompileOutput += $"Decompiled file {nutFilePath}\n";

                File.Delete(cnutFilePath);
            });
            Console.WriteLine(decompileOutput); 
            Console.WriteLine("Finished decompiling files.");
            return true;
        }

        private bool ExtractBrushes()
        {
            string brushesPath = Path.Combine(this.ModPath, "brushes");
            if (!Directory.Exists(brushesPath))
            {
                return true;
            }
            string[] brushFiles = Directory.GetFiles(brushesPath);
            if (brushFiles.Length == 0)
            {
                return true;
            }
            Directory.CreateDirectory(Path.Combine(this.ModPath, "unpacked_brushes"));

            foreach (string brushPath in brushFiles)
            {
                Console.WriteLine($"Extracting brush file {brushPath}");
                using (Process unpackBrush = new Process())
                {
                    unpackBrush.StartInfo.UseShellExecute = false;
                    unpackBrush.StartInfo.RedirectStandardOutput = true;
                    unpackBrush.StartInfo.FileName = Utils.BBRUSHERPATH;
                    unpackBrush.StartInfo.Arguments = $"unpack {brushPath}";
                    unpackBrush.StartInfo.WorkingDirectory = Path.Combine(this.ModPath, "unpacked_brushes");
                    unpackBrush.Start();
                    string output = unpackBrush.StandardOutput.ReadToEnd();
                    unpackBrush.WaitForExit();
                    if (unpackBrush.ExitCode == -2)
                    {
                        StreamReader myStreamReader = unpackBrush.StandardOutput;
                        Console.WriteLine(myStreamReader.ReadLine());
                        Console.WriteLine($"Error unpacking brush file {brushPath}!");

                    }
                }
            }


            return true;
        }

    }
}
