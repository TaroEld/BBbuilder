using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Ionic;
using Ionic.Zip;

namespace BBbuilder
{
    class BuildCommand : Command
    {
        String[] ExcludedAssetFolders = new String[] { ".git", ".github", "unpacked_brushes", ".vscode", ".utils", "assets", "modtools", "music", "sounds", "gfx", "brushes" };
        String[] ExcludedZipFolders = new String[] { ".git", ".github", "unpacked_brushes", ".vscode", ".utils", "assets", "modtools" };
        String[] ExcludedScriptFolders = new String[] { "ui", ".git", ".github", "gfx", "preload", "brushes", "music", "sounds", "unpacked_brushes", "tempfolder", ".vscode", "nexus", ".utils", "assets" };
        string ModPath;
        string ModName;
        string ZipPath;
        bool ScriptOnly;
        bool CompileOnly;
        bool StartGame;
        bool UIOnly;
        bool NoCompile;
        bool NoPack;
        public BuildCommand()
        {
            this.Name = "build";
            this.Description = "Builds your mod and creates a zip file that is copied to the data directory. Optionally can also simply compile the files.";
            this.Arguments = new string[]
            {
                "Mandatory: Specify the path of the mod to be built. (Example: bbuilder build G:/Games/BB/Mods/WIP/mod_msu)",
                "Optional: Pass '-compileonly' to compile the files only without creating a zip.",
                "Optional: Pass '-restart' to close BattleBrothers.exe and start it again after building the mod.",
                "Optional: Pass '-scriptonly' to only pack script files. The mod will have a '_scripts' suffix.",
                "Optional: Pass '-uionly' to only zip the gfx and ui folders. The mod will have a '_ui' suffix.",
                "Optional: Pass '-nocompile' to speed up the build by not compiling files",
                "Optional: Pass '-nopack' to speed up the build by not repacking brushes",
            };
        }
        private bool ParseCommand(string[] _args)
        {
            if (!Directory.Exists(_args[1]))
            {
                Console.WriteLine($"Passed mod path {_args[1]} does not exist!");
                return false;
            }
            this.ModPath = _args[1];
            this.ModName = new DirectoryInfo(this.ModPath).Name;
            this.StartGame = Array.IndexOf(_args, "-restart") >= 0;

            this.UIOnly = Array.IndexOf(_args, "-uionly") >= 0;
            this.ScriptOnly = Array.IndexOf(_args, "-scriptonly") >= 0;

            this.NoCompile = Array.IndexOf(_args, "-nocompile") >= 0;
            this.CompileOnly = Array.IndexOf(_args, "-compileonly") >= 0;

            this.NoPack = Array.IndexOf(_args, "-nopack") >= 0 || this.UIOnly || this.ScriptOnly;

            if (this.ScriptOnly && this.UIOnly)
            {
                Console.WriteLine("-scriptonly and -uionly are mutually exclusive!");
                throw new Exception();
            }
            if (this.NoCompile && this.CompileOnly)
            {
                Console.WriteLine("-nocompile and -compileonly are mutually exclusive");
                throw new Exception();
            }
            if (this.ScriptOnly)
                this.ModName += "_scripts";
            if (this.UIOnly)
                this.ModName += "_ui";
            this.ZipPath = Path.Combine(this.ModPath, this.ModName) + ".zip";
            return true;
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
            if (!this.CompileOnly)
                Console.WriteLine($"Attempting to create {this.ZipPath}");
            else
                Console.WriteLine($"Compiling files of mod {this.ZipPath}");

            if (!this.UIOnly && !this.NoCompile && !CompileFiles())
            {
                Console.WriteLine("Failed while compiling files");
                RemoveOldFiles();
                return false;
            }
            // Leave early if compile only is specified
            if (this.CompileOnly)
            {
                return true;
            }
            if (!this.NoPack && !PackBrushFiles())
            {
                Console.WriteLine("Failed while packing brush files");
                RemoveOldFiles();
                return false;
            }
            if (!ZipFolders())
            {
                Console.WriteLine("Failed while zipping files");
                RemoveOldFiles();
                return false;
            }
            if (!CopyZipToData())
            {
                Console.WriteLine("Failed while copying new zip to data!");
                RemoveOldFiles();
                return false;
            }
            if (this.StartGame)
            {
                KillAndStartBB();
            }
            return true;
        }

        private bool CompileFiles()
        {
            Console.WriteLine("Starting to compile files...");
            string[] allNutFilesAsPath = GetAllowedScriptFiles();
            if (allNutFilesAsPath.Length == 0)
            {
                Console.WriteLine("No files to compile!");
                return true;
            }

            bool noCompileErrors = true;
            Parallel.For(0, allNutFilesAsPath.Length, (i, state) =>
            {
                string nutFilePath = allNutFilesAsPath[i];
                string cnutFilePath = Path.ChangeExtension(nutFilePath, ".cnut");
                string sqCommand = String.Format("-o NUL -c \"{1}\"", cnutFilePath, nutFilePath);

                using (Process compiling = new Process())
                {
                    compiling.StartInfo.UseShellExecute = false;
                    compiling.StartInfo.RedirectStandardOutput = true;
                    compiling.StartInfo.FileName = Utils.SQPATH;
                    compiling.StartInfo.Arguments = sqCommand;
                    compiling.Start();
                    compiling.WaitForExit();
                    if (compiling.ExitCode == -2)
                    {
                        Console.WriteLine(String.Format("Error compiling file {0}!", nutFilePath));
                        StreamReader myStreamReader = compiling.StandardOutput;
                        Console.WriteLine(myStreamReader.ReadLine());

                        noCompileErrors = false;
                    }
                }
            });
            if (noCompileErrors)
                Console.WriteLine("Successfully compiled files!");
            return noCompileErrors;
        }

        private string[] getAllowedFolders(string[] _allowedFolders)
        {
            List<string> ret = new();
            string[] allFolders = Directory.GetDirectories(this.ModPath);
            foreach (string folderPath in allFolders)
            {
                string folderName = new DirectoryInfo(folderPath).Name;
                if (_allowedFolders.Contains(folderName))
                {
                    ret.Add(folderPath);
                }
            }
            return ret.ToArray();
        }

        private string[] getAllFoldersExcept(string[] _forbiddenFolders)
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
            return ret.ToArray();
        }

        private string[] GetAllowedScriptFiles()
        {
            List<string> ret = new();

            string[] allowedFolders = getAllFoldersExcept(this.ExcludedScriptFolders);
            foreach (string folderPath in allowedFolders)
            {
                ret.AddRange(Directory.GetFiles(folderPath, "*.nut", SearchOption.AllDirectories));
            }
            String[] retArray = ret.ToArray();
            return retArray;
        }

        private bool PackBrushFiles()
        {
            string brushesPath = Path.Combine(this.ModPath, "brushes");
            if (Directory.Exists(brushesPath))
            {
                Directory.Delete(brushesPath, true);
                Console.WriteLine($"Removed folder {brushesPath}");
            }
            string folderPath = Path.Combine(this.ModPath, "unpacked_brushes");
            bool noCompileErrors = true;
            if (!Directory.Exists(folderPath) || Directory.GetDirectories(folderPath).Length == 0)
            {
                Console.WriteLine("No brush files to pack!");
                return true;
            }
            if (!Directory.Exists(brushesPath))
            {
                Directory.CreateDirectory(brushesPath);
            }
            string[] subFolders = Directory.GetDirectories(folderPath);
            Parallel.For(0, subFolders.Length, (i, state) =>
            {
                string subFolder = subFolders[i];
                string folderName = new DirectoryInfo(subFolder).Name;
                string brushName = $"{folderName}.brush";
                string command = $"pack {brushName} {subFolder}";
                if (File.Exists(Path.Combine(brushesPath, brushName)))
                {
                    File.Delete(Path.Combine(brushesPath, brushName));
                }
                using (Process packBrush = new Process())
                {
                    packBrush.StartInfo.UseShellExecute = false;
                    packBrush.StartInfo.RedirectStandardOutput = true;
                    packBrush.StartInfo.FileName = Utils.BBRUSHERPATH;
                    packBrush.StartInfo.Arguments = command;
                    packBrush.StartInfo.WorkingDirectory = this.ModPath;
                    packBrush.Start();
                    string output = packBrush.StandardOutput.ReadToEnd();
                    packBrush.WaitForExit();
                    if (packBrush.ExitCode == 2)
                    {
                        Console.WriteLine($"Error building brush {brushName}!");
                        Console.WriteLine(output);
                        noCompileErrors = false;
                    }
                    else
                    {
                        Console.WriteLine($"Packed Brush {brushName}");
                        File.Copy(Path.Combine(this.ModPath, brushName), Path.Combine(brushesPath, brushName));
                        File.Delete(Path.Combine(this.ModPath, brushName));
                    }
                }
            });
            DirectoryInfo wipFolder = Directory.GetParent(this.ModPath);
            if (Directory.Exists(Path.Combine(wipFolder.ToString(), "gfx")))
            {
                Copy(Path.Combine(wipFolder.ToString(), "gfx"), Path.Combine(this.ModPath, "gfx"));
                Directory.Delete(Path.Combine(wipFolder.ToString(), "gfx"), true);
            }
            if (noCompileErrors)
                Console.WriteLine("Successfully packed brush files!");
            return noCompileErrors;
        }

        private bool ZipFolders()
        {
            // Using the Ionic DotNetZip library as this makes it significantly easier to recursively zip folders
            
            string[] allowedFolders; 
            if (this.ScriptOnly)
            {
                string[] excludedFolders = this.ExcludedScriptFolders.Where(val => val != "ui").ToArray();
                allowedFolders = getAllFoldersExcept(excludedFolders);
            }
            else if (this.UIOnly)
            {
                allowedFolders = getAllowedFolders(new string[] { "ui", "gfx" });
            }
            else
                allowedFolders = getAllFoldersExcept(this.ExcludedZipFolders);
            using (var zip = new Ionic.Zip.ZipFile(this.ZipPath))
            {
                foreach (string folderPath in allowedFolders)
                {
                    if (Directory.GetFiles(folderPath).Length == 0 && Directory.GetDirectories(folderPath).Length == 0)
                        continue;
                    DirectoryInfo target = new DirectoryInfo(folderPath);
                    Console.WriteLine($"Added folder {target.Name} to zip.");
                    zip.AddDirectory(folderPath, target.Name);
                }
                zip.Save();
            }
            Console.WriteLine($"Successfully zipped {this.ZipPath}!");
            return true;
        }

        private bool CopyZipToData()
        {
            string gamePath = Properties.Settings.Default.GamePath;
            string zipName = $"{this.ModName}.zip";
            
            string dataZipPath = Path.Combine(gamePath, zipName);
            if (File.Exists(dataZipPath))
            {
                File.Delete(dataZipPath);
            }
            if (Properties.Settings.Default.MoveZip)
                File.Move(this.ZipPath, dataZipPath);
            else
                File.Copy(this.ZipPath, dataZipPath);
            return true;
        }

        private void KillAndStartBB()
        {
            Process[] activeBBInstances = Process.GetProcessesByName("BattleBrothers");
            foreach (Process instance in activeBBInstances)
            {
                Console.WriteLine("Stopping BattleBrothers.exe...");
                instance.Kill();
            }
            string bbFolder = Directory.GetParent(Properties.Settings.Default.GamePath).ToString();
            string bbExe = Path.Combine(bbFolder, "win32", "BattleBrothers.exe");
            Console.WriteLine($"Starting Battle Brothers ({bbExe})");
            using (Process startGame = new Process())
            {
                startGame.StartInfo.UseShellExecute = true;
                startGame.StartInfo.FileName = bbExe;
                startGame.Start();
            }
        }

        private void RemoveOldFiles()
        {
            if (File.Exists(this.ZipPath))
            {
                File.Delete(this.ZipPath);
                Console.WriteLine($"Removed file {this.ZipPath}");
            }
            string brushesPath = Path.Combine(this.ModPath, "brushes");
            if (Directory.Exists(brushesPath))
            {
                Directory.Delete(brushesPath, true);
                Console.WriteLine($"Removed folder {brushesPath}");
            }
            DirectoryInfo wipFolder = Directory.GetParent(this.ModPath);
            string wipGfxPath = Path.Combine(wipFolder.ToString(), "gfx");
            if (Directory.Exists(wipGfxPath))
            {
                Directory.Delete(wipGfxPath, true);
                Console.WriteLine($"Removed folder {wipGfxPath}");
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
