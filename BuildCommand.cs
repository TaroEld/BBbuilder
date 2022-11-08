using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace BBbuilder
{
    class BuildCommand : Command
    {
        String[] ExcludedZipFolders = new String[] { ".git", ".github", "unpacked_brushes", ".vscode", ".utils", "assets" };
        String[] ExcludedScriptFolders = new String[] { ".git", ".github", "gfx", "ui", "preload", "brushes", "music", "sounds", "unpacked_brushes", "tempfolder", ".vscode", "nexus", ".utils", "assets" };
        string ModPath;
        string ModName;
        string ZipPath;
        string TempPath;
        public BuildCommand()
        {
            this.Name = "build";
            this.Description = "test";
            this.Commands = new Dictionary<string, string>
            {
                {"path", "Specify Path of mod to be built!" },
            };
        }

        public override bool HandleCommand(string[] _args)
        {
            if (!base.HandleCommand(_args))
            {
                return false;
            }
            this.ModPath = _args[1];
            bool bootAfterDone = false;
            if (_args.Length > 2 && _args[2] == "true")
            {
                bootAfterDone = true;
            }
            this.ModName = new DirectoryInfo(this.ModPath).Name;
            this.ZipPath = Path.Combine(this.ModPath, this.ModName) + ".zip";
            this.TempPath = Path.Combine(this.ModPath, "_zipTemp");

            Console.WriteLine($"Attempting to create {this.ZipPath}");

            RemoveOldFiles();
            if(!CompileFiles())
            {
                Console.WriteLine("Failed while compiling files");
                return false;
            }
            else Console.WriteLine("Successfully compiled files!");
            if(!PackBrushFiles())
            {
                Console.WriteLine("Failed while packing brush files");
                return false;
            }
            else Console.WriteLine("Successfully packed brush files!");
            if (!ZipFolders())
            {
                Console.WriteLine("Failed while zipping files");
                return false;
            }
            else Console.WriteLine($"Successfully zipped {this.ZipPath}!");
            CopyZipToData();
            Console.WriteLine("Successfully copied zip file!");
            if (bootAfterDone)
            {
                KillAndStartBB();
            }
            return true;
        }

        private bool CompileFiles()
        {
            string[] allNutFilesAsPath = GetAllowedScriptFiles();
/*            byte[] sqBytes = Properties.Resources.squirrel;
            string sqExe = Path.Combine(Path.GetTempPath(), "sq.exe");

            using (FileStream exeFile = new FileStream(sqExe, FileMode.CreateNew))
                exeFile.Write(sqBytes, 0, sqBytes.Length);*/

            bool noCompileErrors = true;

            foreach (string nutFilePath in allNutFilesAsPath)
            {
                string cnutFilePath = Path.ChangeExtension(nutFilePath, ".cnut");
                string sqCommand = String.Format("-o {0} -c {1}", cnutFilePath, nutFilePath);
                using (Process compiling = new Process())
                {
                    compiling.StartInfo.UseShellExecute = false;
                    compiling.StartInfo.RedirectStandardOutput = true;
                    compiling.StartInfo.FileName = @"./tools/sq.exe";
                    compiling.StartInfo.Arguments = sqCommand;
                    compiling.Start();
                    compiling.WaitForExit();
                    if (compiling.ExitCode == -2)
                    {
                        Console.WriteLine(String.Format("Error compiling file {0}!", nutFilePath));
                        noCompileErrors = false;
                    }
                    File.Delete(cnutFilePath);
                }
            }
            return noCompileErrors;
        }

        private string[] GetAllowedFolders(string[] _forbiddenFolders)
        {
            List<string> ret = new();
            string[] allFolders = Directory.GetDirectories(this.ModPath);
            foreach (string folderPath in allFolders)
            {
                string folderName = new DirectoryInfo(folderPath).Name;
                if (!_forbiddenFolders.Contains(folderName))
                {
                    ret.Add(folderPath);
                }
            }
            String[] retArray = ret.ToArray();
            return ret.ToArray();
        }

        private string[] GetAllowedScriptFiles()
        {
            List<string> ret = new();

            string[] allowedFolders = GetAllowedFolders(this.ExcludedScriptFolders);
            foreach (string folderPath in allowedFolders)
            {
                ret.AddRange(Directory.GetFiles(folderPath, "*.nut", SearchOption.AllDirectories));
            }
            String[] retArray = ret.ToArray();
            return retArray;
        }

        private bool ZipFolders()
        {
            // It appears to be easier, albeit slower, to just copy all the folders to be zipped to a new folder, then create a new zip from that
            // Might need to change this to proper zip append
            // Alternatively bundle with 7zip?
            string[] allowedFolders = GetAllowedFolders(this.ExcludedZipFolders);
            Directory.CreateDirectory(this.TempPath);
            foreach (string folderPath in allowedFolders)
            {
                DirectoryInfo target = new DirectoryInfo(folderPath);
                Copy(folderPath, Path.Combine(this.TempPath, target.Name));
            }
            ZipFile.CreateFromDirectory(this.TempPath, this.ZipPath);
            Directory.Delete(this.TempPath, true);
            return true;
        }

        private bool CopyZipToData()
        {
            string gamePath = Properties.Settings.Default.GamePath;
            string dataZipPath = Path.Combine(gamePath, $"{this.ModName}.zip");
            if (File.Exists(dataZipPath))
            {
                File.Delete(dataZipPath);
            }
            File.Copy(this.ZipPath, dataZipPath);
            return true;
        }

        private bool PackBrushFiles()
        {
            string brushesPath = Path.Combine(this.ModPath, "brushes");
            string folderPath = Path.Combine(this.ModPath, "unpacked_brushes");
            bool noCompileErrors = true;
            if (!Directory.Exists(folderPath))
            {
                return true;
            }
            if (!Directory.Exists(brushesPath))
            {
                Directory.CreateDirectory(brushesPath);
            }
            string[] subFolders = Directory.GetDirectories(folderPath);
            foreach (string subFolder in subFolders)
            {
                string folderName = new DirectoryInfo(subFolder).Name;
                string brushName = $"{folderName}.brush";
                string command = $"pack {brushName} {subFolder}";
                if(File.Exists(Path.Combine(brushesPath, brushName)))
                {
                    File.Delete(Path.Combine(brushesPath, brushName));
                }
                using (Process packBrush = new Process())
                {
                    packBrush.StartInfo.UseShellExecute = false;
                    packBrush.StartInfo.RedirectStandardOutput = true;
                    packBrush.StartInfo.FileName = @"./tools/bbrusher.exe";
                    packBrush.StartInfo.Arguments = command;
                    packBrush.StartInfo.WorkingDirectory = this.ModPath;
                    packBrush.Start();
                    string output = packBrush.StandardOutput.ReadToEnd();
                    packBrush.WaitForExit();
                    if (packBrush.ExitCode == -2)
                    {
                        Console.WriteLine($"Error building brush {brushName}!");
                        Console.WriteLine(output);
                        continue;
                    }
                    Console.WriteLine($"Packed Brush {brushName}");
                    File.Copy(Path.Combine(this.ModPath, brushName), Path.Combine(brushesPath, brushName));
                    File.Delete(Path.Combine(this.ModPath, brushName));
                }
            }
            DirectoryInfo wipFolder = Directory.GetParent(this.ModPath);
            if (Directory.Exists(Path.Combine(wipFolder.ToString(), "gfx")))
            {
                Copy(Path.Combine(wipFolder.ToString(), "gfx"), Path.Combine(this.ModPath, "gfx"));
                Directory.Delete(Path.Combine(wipFolder.ToString(), "gfx"), true);
            }


            return noCompileErrors;
        }

        private void KillAndStartBB()
        {
            Process[] activeBBInstances = Process.GetProcessesByName("BattleBrothers");
            foreach (Process instance in activeBBInstances)
            {
                instance.Kill();
            }
            string bbFolder = Directory.GetParent(Properties.Settings.Default.GamePath).ToString();
            string bbExe = Path.Combine(bbFolder, "win32/BattleBrothers.exe");
            Console.WriteLine($"Starting Battle Brothers ({bbExe})");
            Process.Start(bbExe);
        }

        private void RemoveOldFiles()
        {
            if (File.Exists(this.ZipPath))
            {
                Console.WriteLine($"Removing zip {this.ZipPath}");
                File.Delete(this.ZipPath);
            }
            if (Directory.Exists(this.TempPath))
            {
                Directory.Delete(this.TempPath, true);
            }
        }


        // copied from https://learn.microsoft.com/en-us/dotnet/api/system.io.directoryinfo?redirectedfrom=MSDN&view=net-6.0 
        private static void Copy(string sourceDirectory, string targetDirectory)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

            CopyAll(diSource, diTarget);
        }

        private static void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            Directory.CreateDirectory(target.FullName);

            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }
    }
}
