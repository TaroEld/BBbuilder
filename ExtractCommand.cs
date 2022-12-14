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
        string AlternativePath;
        List<string> InitCommandArray;
        public ExtractCommand()
        {
            this.Name = "extract";
            this.Description = "Extract an existing mod to a new or specified directory";
            this.Arguments = new String[]
            {
                "Mandatory: Specify path of mod to extract. The file will be put in your specified 'mods' directory. (Example: bbuilder extract C:/Users/user/Desktop/mod_test.zip)",
                "Optional: Specify alternative path to extract the mod to. (Example: bbuilder extract C:/Users/user/Desktop/mod_test.zip C:/Users/user/Desktop/test/)",
            };
            this.InitCommandArray = new List<string>();
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

            // Create new folder with the initcommand, where the files will be extracted to
            InitCommand initCommand = new();
            if (!initCommand.HandleCommand(this.InitCommandArray.ToArray()))
            {
                Console.WriteLine("Error while creating new folder! Exiting...");
                return false;
            }

            Directory.Delete(Path.Combine(this.ModPath, "scripts"), true);
            if (!ExtractZip())
            {
                Console.WriteLine("Error while extracting zip! Exiting...");
                return false;
            }

            DecompileFiles();
            ExtractBrushes();

            return true;
        }

        private bool ParseCommand(string[] _args)
        {
            if (!File.Exists(_args[1]))
            {
                Console.WriteLine($"Passed path to extract: {_args[1]} but this file does not exist!");
                return false;
            }
            this.ZipPath = _args[1];
            Console.WriteLine($"Extracting zip {this.ZipPath}");
            this.InitCommandArray.Add("init");

            this.ModName = Path.GetFileNameWithoutExtension(this.ZipPath);
            this.ModPath = Path.Combine(Properties.Settings.Default.ModPath, this.ModName);
            this.InitCommandArray.Add(this.ModName);
            List<string> initCommandArray = new List<string> { "init", this.ModName };
            if (_args.Length > 2)
            {
                string alternativePath = _args[2];
                if (!Directory.Exists(_args[2]))
                {
                    Console.WriteLine($"Passed alternative path {_args[2]} but this folder does not exist!");
                    return false;
                }
                this.AlternativePath = _args[2];
                this.ModPath = Path.Combine(this.AlternativePath, this.ModName);
                this.InitCommandArray.Add(this.AlternativePath);
            }
            return true;
        }

        private bool ExtractZip()
        {
            try
            {
                ZipFile.ExtractToDirectory(this.ZipPath, this.ModPath);
            }
            catch
            {
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
            foreach (string cnutFilePath in allCnutFilesAsPath)
            {
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
                        continue;
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
                        continue;
                    }
                }
                decompileOutput += $"Decompiled file {nutFilePath}\n";

                File.Delete(cnutFilePath);
            }
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
