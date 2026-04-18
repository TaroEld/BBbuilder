using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace BBBuilder
{
    public class ExtractBasegameCommand : Command
    {
        string ModPath;
        string ModName;
        string TempZipPath;
        public readonly OptionFlag Replace = new("-overwrite", "Replace the files in an existing folder.");
        public readonly OptionFlag Rename = new("-name <newname>", "Renames the extracted basegame folder. Default: 'basegame'");
        public readonly OptionFlag AltPath = new("-directory <path>", "Specify alternative path to extract the basegame to.");
        List<string> InitCommandArray;

        public ExtractBasegameCommand()
        {
            this.Name = "extract-basegame";
            this.Description = "Merge and extract the base game data_NNN.dat files from the configured data folder";
            this.Arguments = Array.Empty<string>();
            this.InitCommandArray = new List<string>();
        }

        public override bool HandleCommand(string[] _args)
        {
            this.InitCommandArray = new List<string>();
            // No required positional argument — skip base.HandleCommand length check
            if (!ParseCommand(_args.ToList()))
                return false;

            string gamePath = Utils.Data.GamePath;
            string[] datFiles = Directory.GetFiles(gamePath, "data_*.dat")
                .Where(f => Regex.IsMatch(Path.GetFileName(f), @"^data_\d+\.dat$"))
                .OrderBy(f => f)
                .ToArray();

            if (datFiles.Length == 0)
            {
                Console.WriteLine($"No data_NNN.dat files found in {gamePath}! Make sure the path to data/ is correct.");
                return false;
            }

            Console.WriteLine($"Found {datFiles.Length} data file(s), merging...");
            this.TempZipPath = Path.Combine(Path.GetTempPath(), "bbbuilder_basegame_temp.zip");

            if (!MergeDatFiles(datFiles))
                return false;

            bool directoryExisted = Directory.Exists(this.ModPath);
            InitCommand initCommand = new();
            if (!initCommand.HandleCommand(this.InitCommandArray.ToArray()))
            {
                Console.WriteLine("Error while creating new folder! Exiting...");
                return false;
            }

            if (!directoryExisted)
                this.Replace.Value = true;

            try
            {
                ZipFile.ExtractToDirectory(this.TempZipPath, this.ModPath, this.Replace);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error while extracting merged zip: {e}");
                return false;
            }

            DecompileFiles();
            ExtractBrushes();
            Console.WriteLine($"Basegame extracted to {this.ModPath}.");
            return true;
        }

        private bool ParseCommand(List<string> _args)
        {
            this.ParseFlags(_args);
            this.InitCommandArray.Add("init");
            this.ModName = this.Rename ? this.Rename.PositionalValue : "basegame";
            this.ModPath = Path.Combine(Utils.Data.ModPath, this.ModName);
            this.InitCommandArray.Add(this.ModName);
            if (this.AltPath)
            {
                if (!Directory.Exists(this.AltPath.PositionalValue))
                {
                    Console.WriteLine($"Passed alternative path {this.AltPath.PositionalValue} but this folder does not exist!");
                    return false;
                }
                this.ModPath = Path.Combine(Utils.Norm(this.AltPath.PositionalValue), this.ModName);
                this.InitCommandArray.Add(this.AltPath.Flag);
                this.InitCommandArray.Add(this.AltPath.PositionalValue);
            }
            if (this.Replace)
                this.InitCommandArray.Add("-overwrite");
            this.InitCommandArray.Add("-template");
            this.InitCommandArray.Add("blank");
            return true;
        }

        private bool MergeDatFiles(string[] datFiles)
        {
            try
            {
                if (File.Exists(this.TempZipPath))
                    File.Delete(this.TempZipPath);

                using var merged = ZipFile.Open(this.TempZipPath, ZipArchiveMode.Create);
                HashSet<string> addedEntries = new(StringComparer.OrdinalIgnoreCase);

                // Process in reverse order so higher-numbered (patch) files take priority over lower-numbered ones
                foreach (string datFile in datFiles.Reverse())
                {
                    Console.WriteLine($"Adding {Path.GetFileName(datFile)}...");
                    using var archive = ZipFile.OpenRead(datFile);
                    foreach (var entry in archive.Entries)
                    {
                        if (addedEntries.Contains(entry.FullName))
                            continue;
                        addedEntries.Add(entry.FullName);
                        var newEntry = merged.CreateEntry(entry.FullName);
                        using var sourceStream = entry.Open();
                        using var destStream = newEntry.Open();
                        sourceStream.CopyTo(destStream);
                    }
                }
                Console.WriteLine($"Merged {addedEntries.Count} entries from {datFiles.Length} data file(s).");
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Error merging data files: {e.Message}");
                return false;
            }
        }

        private bool DecompileFiles()
        {
            string[] allCnutFilesAsPath = Directory.GetFiles(this.ModPath, "*.cnut", SearchOption.AllDirectories);
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
                    decrypting.StartInfo.CreateNoWindow = true;
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
                    decompiling.StartInfo.CreateNoWindow = true;
                    decompiling.StartInfo.RedirectStandardOutput = true;
                    decompiling.StartInfo.FileName = Utils.NUTCRACKERPATH;
                    decompiling.StartInfo.Arguments = nutcrackerCommand;
                    decompiling.Start();

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
                return true;
            string[] brushFiles = Directory.GetFiles(brushesPath);
            if (brushFiles.Length == 0)
                return true;
            Directory.CreateDirectory(Path.Combine(this.ModPath, "unpacked_brushes"));

            foreach (string brushPath in brushFiles)
            {
                Console.WriteLine($"Extracting brush file {brushPath}");
                using (Process unpackBrush = new Process())
                {
                    unpackBrush.StartInfo.UseShellExecute = false;
                    unpackBrush.StartInfo.CreateNoWindow = true;
                    unpackBrush.StartInfo.RedirectStandardOutput = true;
                    unpackBrush.StartInfo.FileName = Utils.BBRUSHERPATH;
                    unpackBrush.StartInfo.Arguments = $"unpack {brushPath}";
                    unpackBrush.StartInfo.WorkingDirectory = Path.Combine(this.ModPath, "unpacked_brushes");
                    unpackBrush.Start();
                    unpackBrush.StandardOutput.ReadToEnd();
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

        public override void CleanUp(bool success)
        {
            if (this.TempZipPath != null && File.Exists(this.TempZipPath))
            {
                File.Delete(this.TempZipPath);
                Console.WriteLine("Cleaned up temporary merged zip.");
            }
        }
    }
}
