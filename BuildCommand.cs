﻿using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Reflection;
//using Ionic.Zip;
using Force.Crc32;
using System.Text.Json;

namespace BBBuilder
{
    public class BuildCommand : Command
    {
        readonly string[] NotIndexedFolders = new string[] { ".bbbuilder", ".git", ".github", ".vscode", ".utils", "assets", "modtools", "node_modules" };
        readonly string[] ExcludedZipFolders = new string[] {"unpacked_brushes"};
        readonly string[] ExcludedScriptFolders = new string[] { "ui", ".git", ".github", "gfx", "preload", "brushes", "music", "sounds", "unpacked_brushes", "tempfolder", ".vscode", "nexus", ".utils", "assets" };
        public readonly OptionFlag StartGame = new("-restart", "Exit and then start BattleBrothers.exe after building the mod.") { FlagAlias = "-rs"};
        public readonly OptionFlag Transpile = new("-transpile", "Translate js file to es3. It allow you to use modern js syntax and features to create your mod.");
        public readonly OptionFlag Rebuild = new("-rebuild", "Delete the database and the .zip to start from a clean slate.") { FlagAlias = "-rb" };
        public readonly OptionFlag Diff = new("-diff <referencebranch>,<wipbranch>", "Create the zip based on the diff between <referencebranch> and <wipbranch> Pass them comma-separated WITHOUT SPACE INBETWEEN.");

        string ModPath;
        string ModName;
        string ZipName;
        string ZipPath;
        string BuildPath;
        string GfxPath;
        string BrushesPath;
        Dictionary<string, Int64> FilesHashesInFolder;
        Dictionary<string, Int64> FileHashesInDB;
        Dictionary<string, Int64> FilesWhichChanged;
        public BuildCommand()
        {
            this.Name = "build";
            this.Description = "Builds your mod and creates a zip file that is copied to the data directory.";
            this.Arguments = new string[]
            {
                "<modPath>: Specify the path of the mod to be built. (Example: bbuilder build G:/Games/BB/Mods/WIP/mod_msu)",
            };
            this.FilesHashesInFolder = new();
            this.FileHashesInDB = new();
            this.FilesWhichChanged = new();
        }
        private bool ParseCommand(List<string> _args)
        {
            ParseFlags(_args);

            if (!Directory.Exists(_args[1]))
            {
                Utils.WriteRed($"Passed mod path {_args[1]} does not exist!");
                return false;
            }
            this.ModPath = Utils.Norm(_args[1]);
            this.BuildPath = this.ModPath;
            this.ModName = new DirectoryInfo(this.ModPath).Name;

            if (this.Transpile)
            {
                this.BuildPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "temp");
                if (Directory.Exists(this.BuildPath))
                {
                    Directory.Delete(this.BuildPath, true);
                }
                Directory.CreateDirectory(this.BuildPath);
                Utils.Copy(this.ModPath, this.BuildPath);
            }
            this.ZipName = this.ModName + ".zip";
            if (this.Diff)
                this.ZipName = this.ModName + "_patch.zip";
            this.ZipPath = Path.Combine(this.BuildPath, this.ZipName);
            this.GfxPath = Path.Combine(this.BuildPath, "gfx");
            this.BrushesPath = Path.Combine(this.BuildPath, "brushes");
            return true;
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
            string[] sameZipNameInData = Directory.GetFiles(Utils.Data.GamePath, "*.zip")
                .Where(f => Path.GetFileName(f) != this.ZipName && Path.GetFileName(f).StartsWith(this.ModName)).ToArray();
            if (sameZipNameInData.Length > 0)
            {
                Utils.WriteRed("Found other .zip files in data that seem to be the same mod!");
                foreach (string s in sameZipNameInData) { Utils.WriteRed(s); }
                return false;
            }
            Utils.LogTime($"BuildCommand: Initital checks");
            if (this.Diff)
            {
                if (!Utils.IsGitInstalled())
                {
                    Utils.WriteRed("Tried to use diff mode but git does not seem to be installed or accessible via PATH!");
                    return false;
                }
                string feature_branch_name = this.Diff.PositionalValue.Split(",")[1];
                string current_branch = GetCurrentGitBranch();
                if (feature_branch_name != current_branch)
                {
                    Utils.WriteRed($"Tried to use diff mode with feature branch {feature_branch_name} but {current_branch} is checked out! Make sure to check out the feature branch.");
                    return false;
                }
                Utils.LogTime($"BuildCommand: Diff checks");
            }
            if (Utils.Data.MoveZip && !this.Diff && File.Exists(Path.Combine(Utils.Data.GamePath, this.ZipName)) && !File.Exists(this.ZipPath))
            {
                Console.WriteLine("Copying zip from Data");
                File.Copy(Path.Combine(Utils.Data.GamePath, this.ZipName), this.ZipPath);
                Utils.LogTime($"BuildCommand: Moving zip");
            }
            if (this.Rebuild || !File.Exists(this.ZipPath))
            {
                DeleteZipAndDB();
                DeleteBrushAndGfxFiles();
                Utils.LogTime($"BuildCommand: Deleting zip and GFX folders");
            }
            if (this.Diff)
                File.Delete(this.ZipPath);
            // Create and/or read the DB filepath : hash dict, this only needs to be done once
            ReadFileDataFromDB();

            Utils.LogTime($"BuildCommand: Reading hashes from DB");

            // Create the folder filepath : hash dict and check for differences between this and the DB one to know what files to build, this will be repeated later
            this.FilesHashesInFolder = ReadFileDataFromFolder(GetAllFoldersExcept(this.NotIndexedFolders));
            Utils.LogTime($"BuildCommand: Reading/Creating hashes from Folders");

            UpdateFilesWhichChanged(this.FilesHashesInFolder);
            Utils.LogTime($"BuildCommand: Creating changes dict");

            Console.WriteLine($"Attempting to build {this.ModPath}");
            if (!CompileFiles())
            {
                Utils.WriteRed("Failed while compiling files");
                return false;
            }
            Utils.LogTime($"BuildCommand: Compiling files");
            if (this.Transpile && !TranspileToES3())
            {
                Utils.WriteRed("Failed while transpiling to ES3!");
                return false;
            }
            if (!PackBrushFiles())
            {
                Utils.WriteRed("Failed while packing brush files");
                return false;
            }
            Utils.LogTime($"BuildCommand: Packing brush files");

            // re-init the folder filepath : datetime dict to make sure we don't miss something that changed in the meanwhile#
            this.FilesWhichChanged = new();
            var extendedNotIndexedFolders = this.NotIndexedFolders.Concat(new[] { "scripts", this.ModName, "unpacked_brushes"}).ToArray();
            var changes = ReadFileDataFromFolder(GetAllFoldersExcept(extendedNotIndexedFolders));
            foreach (var kvp in changes)
            {
                this.FilesHashesInFolder[kvp.Key] = kvp.Value;
            }
            UpdateFilesWhichChanged(this.FilesHashesInFolder);
            Utils.LogTime($"BuildCommand: Checking for new changes");


            if (!ZipFiles())
            {
                Utils.WriteRed("Failed while zipping files");
                return false;
            }
            Utils.LogTime($"BuildCommand: Zipping files");
            if (!CopyZipToData())
            {
                Utils.WriteRed("Failed while copying new zip to data!");
                return false;
            }

            if (this.StartGame)
            {
                Utils.KillAndStartBB();
                Utils.LogTime($"BuildCommand: Starting the game");
            }
            WriteFileDataToDB();
            Utils.LogTime($"BuildCommand: Writing hash DB");

            return true;
        }

        public void DeleteZipAndDB()
        {
            if (File.Exists(this.ZipPath))
                File.Delete(this.ZipPath);
            if (File.Exists(Path.Combine(Utils.Data.GamePath, this.ZipName)))
                File.Delete(Path.Combine(Utils.Data.GamePath, this.ZipName));
            if (File.Exists(Path.Combine(this.ModPath, ".bbbuilder", "hash.json")))
                File.Delete(Path.Combine(this.ModPath, ".bbbuilder", "hash.json"));
            Console.WriteLine("Rebuilding: Deleted .zip and database");
        }

        private Dictionary<string, Int64> ReadFileDataFromFolder(string[] folders)
        {
            Dictionary<string, Int64> ret = new();
            foreach (var folder in folders)
            {
                foreach(var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                {
                    ret.Add(Path.GetRelativePath(this.BuildPath, file), CalculateChecksum(file));
                }
            }
            return ret;
        }

        static Int64 CalculateChecksum(string filePath)
        {
            return Crc32Algorithm.Compute(File.ReadAllBytes(filePath));
        }

        private void ReadFileDataFromDB()
        {
            var jsonPath = Path.Combine(this.ModPath, ".bbbuilder", "hash.json");
            if (!File.Exists(jsonPath))
                return;
            this.FileHashesInDB = JsonSerializer.Deserialize<Dictionary<string, Int64>>(File.ReadAllText(jsonPath));
        }

        private void WriteFileDataToDB()
        {
            if (!Directory.Exists(Path.Combine(this.ModPath, ".bbbuilder")))
                Directory.CreateDirectory(Path.Combine(this.ModPath, ".bbbuilder"));
            var jsonPath = Path.Combine(this.ModPath, ".bbbuilder", "hash.json");
            string jsonString = JsonSerializer.Serialize(this.FilesHashesInFolder);
            File.WriteAllText(jsonPath, jsonString);
        }

        private void UpdateFilesWhichChanged(Dictionary<string, Int64> dict)
        {
            foreach (var entry in dict)
            {
                if (!(this.FileHashesInDB.ContainsKey(entry.Key)) || this.FileHashesInDB[entry.Key] != entry.Value)
                    this.FilesWhichChanged.Add(entry.Key, entry.Value);
            }
        }

        private bool HasFileChanged(string filePath)
        {
            return this.FilesWhichChanged.ContainsKey(Path.GetRelativePath(this.BuildPath, filePath));
        }

        private bool CompileFiles()
        {
            //get this script directory
            Console.WriteLine("Starting to compile files...");
            string[] allNutFilesAsPath = GetAllowedScriptFiles();
            string[] changedNutFiles = allNutFilesAsPath.Where(f => HasFileChanged(f)).ToArray();
            int compiledFiles = 0;
            List<string> outputBuffer = new();
            List<string> errorBuffer = new();
            if (changedNutFiles.Length == 0)
            {
                Console.WriteLine("No files to compile!");
                return true;
            }

            bool noCompileErrors = true;
            string argument = String.Join(" ", changedNutFiles);
            bool args_too_long = argument.Length > 32650;
            // max argument len is 32699... So we write to file and tell sq to read from file
            if (args_too_long) {
                string file_path = Path.Combine(Utils.EXECUTINGFOLDER, "tools", "argument.txt");
                File.WriteAllLines(file_path, changedNutFiles);
                argument = $"-f {file_path}";
                Console.WriteLine($"Amount of files exceeded parameter limit, writing to file {file_path}");
            }
            using (Process compiling = new())
            {
                compiling.StartInfo.UseShellExecute = false;
                compiling.StartInfo.RedirectStandardOutput = true;
                compiling.StartInfo.RedirectStandardError = true;
                compiling.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                compiling.StartInfo.CreateNoWindow = true;
                compiling.StartInfo.FileName = Utils.SQPATH;
                compiling.StartInfo.Arguments = argument;
                compiling.OutputDataReceived += (o, e) => outputBuffer.Add(e.Data);
                compiling.ErrorDataReceived += (o, e) => errorBuffer.Add(e.Data);
                compiling.Start();
                compiling.BeginOutputReadLine();
                compiling.BeginErrorReadLine();
                compiling.WaitForExit();
                noCompileErrors = noCompileErrors && compiling.ExitCode != -2;
            };
            outputBuffer = outputBuffer.Where((e) => e != null && e.Length > 0).ToList();
            errorBuffer = errorBuffer.Where((e) => e != null && e.Length > 0).ToList();
            foreach (string line in outputBuffer)
            {
                Utils.VerbosePrint(line);
                compiledFiles++;
            }
            foreach (string line in errorBuffer)
            {
                Utils.WriteRed(line);
            }
            if (compiledFiles > 0)
            {
                Utils.WriteGreen($"Successfully compiled {compiledFiles} files.");
            }

            return noCompileErrors;
        }

        private bool TranspileToES3()
        {
            Console.WriteLine("Starting to transpile to ES3...");
            string localWorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string[] dependencies = new string[] { "@babel/cli", "@babel/preset-env", "browserify", "core-js" };

            if (!CheckNpmPresence()) return false;

            Console.WriteLine("-- Check npm dependencies...");
            if (!CheckNpmDependencies(dependencies, localWorkingDirectory)) return false;
            Utils.LogTime($"-- Check npm dependencies");
            string babelLoc = Path.Combine(localWorkingDirectory, "node_modules", ".bin", "babel");
            string browserifyLoc = Path.Combine(localWorkingDirectory, "node_modules", ".bin", "browserify");

            Console.WriteLine("-- Transpile from modern JS to old JS...");
            using (Process compiling = new())
            {
                compiling.StartInfo.UseShellExecute = true;
                compiling.StartInfo.FileName = babelLoc;
                compiling.StartInfo.Arguments = String.Format("\"{0}\" --out-dir \"{1}\" --config-file \"{2}\"", this.BuildPath, this.BuildPath, Path.Combine(localWorkingDirectory, "babel.config.json"));
                compiling.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                compiling.Start();
                compiling.WaitForExit();
            }
            Utils.LogTime($"-- Transpile from modern JS to old JS");

            Console.WriteLine("-- Browserify the transpilation result...");
            using (Process compiling = new())
            {
                compiling.StartInfo.UseShellExecute = true;
                compiling.StartInfo.FileName = browserifyLoc;
                compiling.StartInfo.Arguments = String.Format("\"{0}\" -o \"{0}\"", Path.Combine(this.BuildPath, "ui/mods/", ModName, "index.js"));
                compiling.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                compiling.Start();
                compiling.WaitForExit();
            }
            Utils.LogTime($"-- Browserify the transpilation result");

            if(Directory.Exists(Path.Combine(BuildPath, "node_modules"))){
                Console.WriteLine("-- Remove node_modules from buidPath...");
                //Directory.Delete(Path.Combine(BuildPath, "node_modules"), true);
                Console.WriteLine($"-- Remove node_modules");
            }

            return true;
        }

        private void DeleteBrushAndGfxFiles()
        {
            if (Directory.Exists(this.BrushesPath)) { 
                Directory.Delete(this.BrushesPath, true); 
            }
            if (Directory.Exists(this.GfxPath))
            {
                foreach (var item in Directory.GetFiles(this.GfxPath))
                {
                    File.Delete(item);
                }
            }
        }
        private bool PackBrushFiles()
        {
            string unpackedBrushesPath = Path.Combine(this.BuildPath, "unpacked_brushes");
            if (!Directory.Exists(unpackedBrushesPath) || Directory.GetDirectories(unpackedBrushesPath).Length == 0)
            {
                Console.WriteLine("No brush files to pack!");
                DeleteBrushAndGfxFiles();
                return true;
            }

            if (!Directory.Exists(this.BrushesPath)) Directory.CreateDirectory(this.BrushesPath);
            if (!Directory.Exists(this.GfxPath)) Directory.CreateDirectory(this.GfxPath);
            string[] existingBrushes = Directory.GetFiles(this.BrushesPath).Select(Path.GetFileName).ToArray();
            string[] existingGfx = Directory.GetFiles(this.GfxPath).Select(Path.GetFileName).ToArray();
            string[] subFolders = Directory.GetDirectories(unpackedBrushesPath);
            string[] subFoldersNameOnly = subFolders.Select(Path.GetFileName).ToArray();

            // delete brushes and gfx that dont exist anymore
            foreach (string brushFile in existingBrushes.Select(Path.GetFileNameWithoutExtension))
            {
                if (!subFoldersNameOnly.Contains(brushFile))
                {
                    Console.WriteLine("Deleting file " + brushFile + ".brush");
                    File.Delete(Path.Combine(this.BrushesPath, brushFile + ".brush"));

                }
            }
            foreach (string gfxFile in existingGfx.Select(Path.GetFileNameWithoutExtension))
            {
                if (!subFoldersNameOnly.Contains(gfxFile))
                {
                    Console.WriteLine("Deleting file " + gfxFile + ".png");
                    File.Delete(Path.Combine(this.GfxPath, gfxFile + ".png"));
                }
            }

            bool noCompileErrors = true;
            bool packedBrushes = false;
            List<string> outputBuffer = new();
            
            if (!Directory.Exists(this.BrushesPath))
            {
                Directory.CreateDirectory(this.BrushesPath);
            }

            //(int i = 0; i < subFolders.Length; i++)
            Parallel.For(0, subFolders.Length, (i) =>
            {
                string subFolder = subFolders[i];
                string nameOnly = Path.GetFileName(subFolder);
                bool hasBrush = existingBrushes.Contains(nameOnly + ".brush");
                bool hasGfx = existingGfx.Contains(nameOnly + ".png");
                string[] changedFiles = Directory.GetFiles(subFolder, "*", SearchOption.AllDirectories).Where(f => HasFileChanged(f)).ToArray();
                if (changedFiles.Length == 0 && hasBrush && hasGfx)
                {
                    return;
                }
                File.Delete(Path.Combine(this.BrushesPath, nameOnly + ".brush"));
                File.Delete(Path.Combine(this.GfxPath, nameOnly + ".png"));

                packedBrushes = true;

                string brushName = $"{nameOnly}.brush";
                string command = $"pack --gfxPath {this.BuildPath} \"brushes/{brushName}\" \"{subFolder}\"";

                using (Process packBrush = new())
                {
                    packBrush.StartInfo.UseShellExecute = false;
                    packBrush.StartInfo.RedirectStandardOutput = true;
                    packBrush.StartInfo.FileName = Utils.BBRUSHERPATH;
                    packBrush.StartInfo.Arguments = command;
                    packBrush.StartInfo.WorkingDirectory = this.BuildPath;
                    packBrush.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    packBrush.StartInfo.CreateNoWindow = true;
                    packBrush.Start();
                    string output = packBrush.StandardOutput.ReadToEnd();
                    packBrush.WaitForExit();
                    if (packBrush.ExitCode == 2)
                    {
                        outputBuffer.Add($"Error building brush {brushName}!");
                        outputBuffer.Add(output);
                        noCompileErrors = false;
                    }
                    else
                    {
                        outputBuffer.Add($"Packed Brush {brushName}");
                    }
                }
            });

            if (!noCompileErrors)
            {
                Utils.WriteRed("Errors while packing brushes!\n-------------------------------------");
                foreach (string line in outputBuffer)
                    Utils.WriteRed(line);
                Utils.WriteRed("-------------------------------------");
            }
            if (noCompileErrors && packedBrushes)
                Console.WriteLine("Successfully packed brush files!");
            if (!packedBrushes)
                Console.WriteLine("Brush files didn't change!");
            return noCompileErrors;
        }

        private List<string> GetDiffFiles()
        {
            string[] branches = this.Diff.PositionalValue.Split(",");
            try
            {
                string output;
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"diff {branches[0]} {branches[1]} --name-only",
                    RedirectStandardOutput = true,
                    RedirectStandardError = false,
                    UseShellExecute = false,
                    WorkingDirectory = this.BuildPath
                };

                using (var process = Process.Start(processStartInfo))
                {
                    StreamReader sr = process.StandardOutput;
                    output = sr.ReadToEnd();
                    process.WaitForExit();
                }
                var ret = output.Split("\n")[0..^1].ToList();
                return ret.Select(f => Path.Combine(this.BuildPath, Utils.Norm(f))).ToList();
            }
            catch (Exception ex)
            {
                Utils.WriteRed(ex.ToString());
            }
            return new List<string>();
        }

        private string GetCurrentGitBranch()
        {
            string output;
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"rev-parse --abbrev-ref HEAD",
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                WorkingDirectory = this.BuildPath,
            };

            using (var process = Process.Start(processStartInfo))
            {
                StreamReader sr = process.StandardOutput;
                output = sr.ReadToEnd();
                process.WaitForExit();
            }
            return output.Split("\n")[0];
        }

        private List<string> RemoveExcludedFolderFiles(List<string> _filesToZip)
        {
            foreach (string folderName in this.ExcludedZipFolders)
            {
                string safetyPath = Path.Combine(this.BuildPath, folderName);
                _filesToZip = _filesToZip.Where(f => !f.Contains(safetyPath)).ToList();
            }
            return _filesToZip;
        }
        private List<string> GetFilesToZip()
        {
            List<string> files;
            if (this.Diff)
            {
                files = GetDiffFiles();
                List<string> brushesFiles = files.Where(f => f.Contains("unpacked_brushes")).ToList();
                List<string> brushesFolders = new();
                // add brushes folders as they are not tracked by git
                foreach (string file in brushesFiles)
                {
                    string[] directories = file.Split(Path.DirectorySeparatorChar);
                    string brushesFileName = directories[Array.IndexOf(directories, "unpacked_brushes") + 1] + ".brush";
                    string brushPath = Path.Combine(this.BrushesPath, brushesFileName);
                    brushesFolders.Add(brushPath);
                }
                files.AddRange(brushesFolders.Distinct().ToList());    
            }
            else files = this.FilesHashesInFolder.Keys.Select(f => Path.Combine(this.BuildPath, f)).ToList();
            files = files.Where(f => this.FilesWhichChanged.ContainsKey(Path.GetRelativePath(this.BuildPath, f))).ToList();
            files = RemoveExcludedFolderFiles(files);
            return files;
        }

        private bool ZipFiles()
        {
            List<string> toZip = GetFilesToZip();
            //Dictionary<string, string> debugToZip = ReplaceDebugStatements(toZip);
            int changedFiles = 0;
            int removedFiles = 0;
            ZipArchiveMode zipMode = ZipArchiveMode.Create;
            if (File.Exists(this.ZipPath))
            {
                zipMode = ZipArchiveMode.Update;
                using (ZipArchive zip = ZipFile.Open(this.ZipPath, zipMode))
                {
                    
                    for (int i = zip.Entries.Count-1; i > -1; i--)
                    {
                        ZipArchiveEntry entry = zip.Entries[i];
                        if (entry.Name == "") continue;
                        string relativePath = entry.FullName;
                        if (!this.FilesHashesInFolder.ContainsKey(relativePath))
                        {
                            Utils.VerbosePrint("Removing file in zip: " + relativePath);
                            entry.Delete();
                            removedFiles++;
                        }
                    };
                }
            }

            using (ZipArchive zip = ZipFile.Open(this.ZipPath, zipMode))
            {
                foreach (string file in toZip)
                { 
                    var relativePath = Path.GetRelativePath(this.BuildPath, file);
                    if (this.Diff && !File.Exists(file))
                    {
                        Console.WriteLine("Skipping file in zip due to -diff: " + file);
                    }
                    else
                    {
                        Utils.VerbosePrint("Updating file in zip: " + file + "(" + relativePath + ")");
                        if (zipMode == ZipArchiveMode.Update) zip.GetEntry(relativePath)?.Delete();
                        zip.CreateEntryFromFile(file, relativePath);
                        changedFiles++;
                    } 
                }
            }
            Console.WriteLine($"Successfully zipped {this.ModPath} ({this.ZipPath} | Added or changed files: {changedFiles}, removed files: {removedFiles})!");
            return true;
        }

        private bool CopyZipToData()
        {
            string gamePath = Utils.Data.GamePath;
            string dataZipPath = Path.Combine(gamePath, this.ZipName);
            if (File.Exists(dataZipPath))
            {
                File.Delete(dataZipPath);
            }
            File.Copy(this.ZipPath, dataZipPath);
            return true;
        }

        override public void CleanUp(bool success = true)
        {
            if (Utils.Data.MoveZip && File.Exists(this.ZipPath))
            {
                File.Delete(this.ZipPath);
                Console.WriteLine($"Removed file {this.ZipPath}");
            }
        }

        private static void InstallNpmDependency(String npmPackageToInstall, string installationPath)
        {
            /** By Kfox
            * install a npm dependency.
            */
            using (Process compiling = new())
            {
                //move process to the current directory
                compiling.StartInfo.WorkingDirectory = installationPath;

                compiling.StartInfo.UseShellExecute = true;
                compiling.StartInfo.FileName = "cmd.exe";
                compiling.StartInfo.Arguments = String.Format("/C npm i {0}", npmPackageToInstall);
                compiling.Start();
                compiling.WaitForExit();
            }
        }

        private static bool CheckNpmDependencies(string[] names, string installationPath, bool installIfMissing = true)
        {
            /** By KFfox
            * Checks if a npm dependency is installed and installs it if it is not.
            */
            foreach (string name in names)
            {
                string depPath = Path.Combine(installationPath, "node_modules", name);
                if (!Directory.Exists(depPath))
                {
                    Console.WriteLine($"Missing npm dependency {name}...");
                    if (installIfMissing)
                    {
                        Console.WriteLine($"Installing {name}...");
                        InstallNpmDependency(name, installationPath);
                    }
                    else
                    {
                        Console.WriteLine($"parameter installIfMissing is false, please install dependency {name} and try again");
                        return false;
                    }
                }
            }
            return true;
        }

        private static bool CheckNpmPresence()
        {
            /** By KFfox
            * Checks if npm is installed and exits the program if it is not.
            */
            string pathVar = Environment.GetEnvironmentVariable("path");
            bool hasNpm = pathVar.Contains("nodejs");
            if (!hasNpm)
            {
                Console.WriteLine("npm not found in PATH variable. Please install Node.js (https://nodejs.org/en/download) or add the folder to PATH and try again.");
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                return false;
            }
            return true;
        }

        private string[] GetAllFoldersExcept(string[] _forbiddenFolders)
        {
            List<string> ret = new();
            string[] allFolders = Directory.GetDirectories(this.BuildPath);
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

            string[] allowedFolders = GetAllFoldersExcept(this.ExcludedScriptFolders);
            foreach (string folderPath in allowedFolders)
            {
                ret.AddRange(Directory.GetFiles(folderPath, "*.nut", SearchOption.AllDirectories));
            }
            return ret.ToArray();
        }
    }
}
