using System;
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

        string[] ExcludedZipFolders = new string[] {"unpacked_brushes"};
        string[] NormalizedExcludedZipFolders;
        readonly string[] ExcludedScriptFolders = new string[] { "ui", ".git", ".github", "gfx", "preload", "brushes", "music", "sounds", "unpacked_brushes", "tempfolder", ".vscode", "nexus", ".utils", "assets" };
        public readonly OptionFlag StartGame = new("-restart", "Exit and then start BattleBrothers.exe after building the mod.") { FlagAlias = "-rs"};
        public readonly OptionFlag Transpile = new("-transpile", "Translate js file to es3. It allow you to use modern js syntax and features to create your mod.");
        public readonly OptionFlag Rebuild = new("-rebuild", "Delete the database and the .zip to start from a clean slate.") { FlagAlias = "-rb" };
        public readonly OptionFlag Diff = new("-diff <referencebranch>,<wipbranch>", "Create the zip based on the diff between <referencebranch> and <wipbranch> Pass them comma-separated WITHOUT SPACE INBETWEEN.");
        public readonly OptionFlag CustomZipName = new("-zipname <name>", "Name of the resulting zip file. .zip extension is added. If not specified, mod name is used.") { FlagAlias = "-z" };
        public readonly OptionFlag ExcludeFolders = new("-excludedfolders <folderName1,[folderName2],...>", "Folders to remove from finished zip. The folder 'unpacked_brushes' will always be removed.") { FlagAlias = "-ex" };

        string ModPath;
        string ModName;
        string ZipName;
        public string ZipPath;
        string BuildPath;
        string GfxPath;
        string BrushesPath;
        public Dictionary<string, Int64> FilesHashesInFolder;
        public Dictionary<string, Int64> FileHashesInDB;
        public Dictionary<string, Int64> FilesWhichChanged;
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
            if (this.CustomZipName) {
                this.ZipName = this.CustomZipName.PositionalValue + ".zip";
            }         
            else if (this.Diff) {
                this.ZipName = this.ModName + "_patch.zip";
            }
            else {
                this.ZipName = this.ModName + ".zip";
            }
            this.ZipPath = Path.Combine(this.BuildPath, this.ZipName);
            this.GfxPath = Path.Combine(this.BuildPath, "gfx");
            this.BrushesPath = Path.Combine(this.BuildPath, "brushes");
            if (this.ExcludeFolders)
            {
                ExcludedZipFolders = ExcludedZipFolders.Concat(ExcludeFolders.PositionalValue.Split(',')).ToArray();
            }
            this.NormalizedExcludedZipFolders = ExcludedZipFolders.Select(f => Utils.Norm(f)).ToArray();
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
            if (Utils.Data.GamePath  == "")
            {
                Utils.WriteRed("Game path is not set - set it via the config command!");
                return false;
            }
            if (!CheckAllowedZipNames())
            {
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

            this.FilesWhichChanged = CompareDictionaries(this.FileHashesInDB, this.FilesHashesInFolder);
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
            var extendedNotIndexedFolders = this.NotIndexedFolders.Concat(new[] { "scripts", this.ModName, "unpacked_brushes" }).ToArray();
            var folders = GetAllFoldersExcept(extendedNotIndexedFolders);
            var beforeBrush = ReadFileDataFromFolder(folders);
            if (!PackBrushFiles())
            {
                Utils.WriteRed("Failed while packing brush files");
                return false;
            }
            Utils.LogTime($"BuildCommand: Packing brush files");
            
            folders = GetAllFoldersExcept(extendedNotIndexedFolders);
            var afterBrush = ReadFileDataFromFolder(folders);
            foreach (var kvp in afterBrush)
            {
                this.FilesHashesInFolder[kvp.Key] = kvp.Value;
                this.FileHashesInDB[kvp.Key] = kvp.Value;
                if (!beforeBrush.ContainsKey(kvp.Key) || !beforeBrush[kvp.Key].Equals(kvp.Value))
                {
                    this.FilesWhichChanged[kvp.Key] = kvp.Value;
                }
            }
            foreach (var kvp in beforeBrush)
            {
                if (!afterBrush.ContainsKey(kvp.Key))
                {
                    this.FileHashesInDB.Remove(kvp.Key);
                    this.FilesHashesInFolder.Remove(kvp.Key);
                }
            }
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

        private bool CheckAllowedZipNames()
        {
            string allowedZipNamesPath = Path.Combine(this.ModPath, ".bbbuilder", "allowed_zip_names.txt");
            string[] allowedZipNames;
            if (File.Exists(allowedZipNamesPath))
            {
                allowedZipNames = File.ReadAllLines(allowedZipNamesPath);
            }
            else
            {
                allowedZipNames = Array.Empty<string>();
            }
            string[] sameZipNameInData = Directory.GetFiles(Utils.Data.GamePath, "*.zip")
                .Select(
                    f => {
                        string fileName = Path.GetFileName(f);
                        return Path.GetFileNameWithoutExtension(fileName);
                    })
                .Where(f => {
                    var zipNameWithoutExtension = Path.GetFileNameWithoutExtension(this.ZipName);
                    return f != zipNameWithoutExtension && f.StartsWith(zipNameWithoutExtension);
                })
                .ToArray();

            string[] sameZipNameInDataNotAllowed = sameZipNameInData.Where(f => !allowedZipNames.Contains(f)).ToArray();
            string[] sameZipNameInDataAllowed = sameZipNameInData.Where(f => allowedZipNames.Contains(f)).ToArray();
            if (sameZipNameInDataAllowed.Length > 0)
            {
                Utils.WriteGreen("Found other .zip files in data that seem to be the same mod, but have previously been allowed (in .bbbuilder/allowed_zip_names.txt):");
                foreach (string s in sameZipNameInData)
                {
                    Utils.WriteGreen(Path.GetFileName(s));
                }
            }

            if (sameZipNameInDataNotAllowed.Length > 0)
            {
                Utils.WriteRed("Found other .zip files in data that seem to be the same mod!");
                Utils.WriteGreen("Currently building: " + this.ZipName);
                Utils.WriteRed("Similar zipe file(s):");
                foreach (string s in sameZipNameInData)
                {
                    Utils.WriteRed(Path.GetFileName(s));
                }

                Console.WriteLine("Do you want to continue anyway? (y/n)");
                string response = Console.ReadLine()?.Trim().ToLower();

                if (response != "y")
                {
                    return false;
                }
                else
                {
                    Console.WriteLine("Do you want to allow this name in the future? If yes, your choice will be saved in .bbbuilder/allowed_zip_names.txt. (y/n)");
                    response = Console.ReadLine()?.Trim().ToLower();
                    if (response == "y")
                    {
                        var path = Path.Combine(this.ModPath, ".bbbuilder", "allowed_zip_names.txt");
                        allowedZipNames = allowedZipNames.Union(sameZipNameInData).ToArray();
                        File.WriteAllLines(path, allowedZipNames);
                    }
                }
            }
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

        private Dictionary<string, Int64> CompareDictionaries(Dictionary<string, Int64> dict1, Dictionary<string, Int64> dict2)
        {
            Dictionary<string, Int64> changes = new();
            foreach (var entry in dict1)
            {
                if (!dict2.ContainsKey(entry.Key) || !dict2[entry.Key].Equals(entry.Value))
                {
                    changes.Add(entry.Key, entry.Value);
                }
            }
            foreach (var entry in dict2)
            {
                if (!dict1.ContainsKey(entry.Key))
                {
                    changes.Add(entry.Key, entry.Value);
                }
            }
            return changes;
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
            string argument = String.Join(" ", changedNutFiles.Select(s => "\"" + s + "\"").ToArray());
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
                compiling.StartInfo.Arguments = String.Format("\"{0}\" --out-dir \"{1}\" --config-file \"{2}\"", this.BuildPath, this.BuildPath, Path.Combine(localWorkingDirectory, "assets", "babel.config.json"));
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
            string[] unpackedBrushesSubFolders = Directory.Exists(unpackedBrushesPath) ? Directory.GetDirectories(unpackedBrushesPath) : Array.Empty<string>();
            string[] unpackedBrushesSubFoldersNameOnly = unpackedBrushesSubFolders.Select(Path.GetFileName).ToArray();

            if (!Directory.Exists(this.BrushesPath)) Directory.CreateDirectory(this.BrushesPath);
            if (!Directory.Exists(this.GfxPath)) Directory.CreateDirectory(this.GfxPath);
            string[] existingBrushes = Directory.GetFiles(this.BrushesPath).Select(Path.GetFileName).ToArray();
            string[] existingGfx = Directory.GetFiles(this.GfxPath).Select(Path.GetFileName).ToArray();


            // delete brushes and gfx that dont exist anymore
            foreach (string brushFile in existingBrushes.Select(Path.GetFileNameWithoutExtension))
            {
                if (!unpackedBrushesSubFoldersNameOnly.Contains(brushFile))
                {
                    Console.WriteLine("Deleting file " + brushFile + ".brush as no corresponding unpacked_brushes folder was found.");
                    File.Delete(Path.Combine(this.BrushesPath, brushFile + ".brush"));

                }
            }
            foreach (string gfxFile in existingGfx.Select(Path.GetFileNameWithoutExtension))
            {
                if (!unpackedBrushesSubFoldersNameOnly.Contains(gfxFile))
                {
                    Console.WriteLine("Deleting file " + gfxFile + ".png as no corresponding unpacked_brushes folder was found.");
                    File.Delete(Path.Combine(this.GfxPath, gfxFile + ".png"));
                }
            }
            if (unpackedBrushesSubFolders.Length == 0)
            {
                Console.WriteLine("No brush files to pack!");
                DeleteBrushAndGfxFiles();
                return true;
            }


            bool noCompileErrors = true;
            bool packedBrushes = false;
            List<string> outputBuffer = new();
            
            if (!Directory.Exists(this.BrushesPath))
            {
                Directory.CreateDirectory(this.BrushesPath);
            }

            //(int i = 0; i < subFolders.Length; i++)
            Parallel.For(0, unpackedBrushesSubFolders.Length, (i) =>
            {
                string subFolder = unpackedBrushesSubFolders[i];
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
        private bool IsExcluded(string filePath)
        {
            string normalizedPath = Utils.Norm(filePath).Replace(this.ModPath, "");

            // Get relative path properly (handles separators automatically)
            string relativePath = normalizedPath.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return NormalizedExcludedZipFolders.Any(excludedFolder =>
                relativePath.StartsWith(excludedFolder) ||
                relativePath == excludedFolder);
        }

        private List<string> RemoveExcludedFolderFiles(List<string> _filesToZip)
        {
            return _filesToZip.Where(f => !IsExcluded(f)).ToList();
        }

        private List<string> GetChangedFiles()
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

        static string GetRootFolder(string path)
        {
            while (true)
            {
                string temp = Path.GetDirectoryName(path);
                if (String.IsNullOrEmpty(temp))
                    break;
                path = temp;
            }
            return path;
        }

        private bool ZipFiles()
        {
            List<string> changedFiles = GetChangedFiles();
            int addedOrChangedCount = 0;
            int removedCount = 0;

            ZipArchiveMode zipMode = File.Exists(this.ZipPath) ? ZipArchiveMode.Update : ZipArchiveMode.Create;

            using (ZipArchive zip = ZipFile.Open(this.ZipPath, zipMode))
            {
                // Step 1: Clean up the zip - remove files that no longer exist or are in excluded folders
                if (zipMode == ZipArchiveMode.Update)
                {
                    removedCount = RemoveObsoleteEntries(zip);
                }

                // Step 2: Add/update changed files
                addedOrChangedCount += AddOrUpdateFiles(zip, changedFiles, zipMode);

                // Step 3: Re-add files that are missing, for example due to previously being excluded
                addedOrChangedCount += AddMissingFiles(zip, zipMode);
            }

            Console.WriteLine($"Successfully zipped {this.ModPath} ({this.ZipPath} | Added or changed files: {addedOrChangedCount}, removed files: {removedCount})!");
            return true;
        }

        private int RemoveObsoleteEntries(ZipArchive zip)
        {
            int removedCount = 0;

            for (int i = zip.Entries.Count - 1; i >= 0; i--)
            {
                ZipArchiveEntry entry = zip.Entries[i];
                if (string.IsNullOrEmpty(entry.Name)) continue;

                string relativePath = entry.FullName.Replace("/", @"\");
                bool isExcluded = IsExcluded(entry.FullName);

                // Remove if file no longer exists in our tracked files, or if it's in an excluded folder
                if (!this.FilesHashesInFolder.ContainsKey(relativePath) || isExcluded)
                {
                    Utils.VerbosePrint("Removing file in zip: " + entry.FullName);
                    entry.Delete();
                    removedCount++;
                }
            }

            return removedCount;
        }

        private int AddOrUpdateFiles(ZipArchive zip, List<string> filesToAdd, ZipArchiveMode zipMode)
        {
            int count = 0;

            foreach (string file in filesToAdd)
            {
                if (this.Diff && !File.Exists(file))
                {
                    Console.WriteLine("Skipping file in zip due to -diff: " + file);
                    continue;
                }

                string relativePath = Path.GetRelativePath(this.BuildPath, file)
                    .Replace(@"\\", @"/")
                    .Replace(@"\", @"/");

                Utils.VerbosePrint("Updating file in zip: " + file + "(" + relativePath + ")");
                // Remove existing entry if it exists and we're updating an existing mod
                if (zipMode == ZipArchiveMode.Update)
                {
                    zip.GetEntry(relativePath)?.Delete();
                }

                // Add the file
                zip.CreateEntryFromFile(file, relativePath);
                count++;
            }

            return count;
        }

        private int AddMissingFiles(ZipArchive zip, ZipArchiveMode zipMode)
        {
            int count = 0;

            foreach (var fileHash in this.FilesHashesInFolder)
            {
                string relativePath = fileHash.Key;
                string zipPath = relativePath.Replace(@"\", @"/");

                // Skip if this file is in an excluded folder
                if (IsExcluded(relativePath))
                        continue;

                // Skip if file is already in the zip
                if (zipMode == ZipArchiveMode.Update && zip.GetEntry(zipPath) != null)
                {
                    continue;
                }                   

                // This file should be in the zip but isn't - add it
                string fullPath = Path.Combine(this.BuildPath, relativePath);
                if (File.Exists(fullPath))
                {
                    Utils.VerbosePrint("Adding missing file file: " + fullPath + "(" + zipPath + ")");
                    zip.CreateEntryFromFile(fullPath, zipPath);
                    count++;
                }
            }

            return count;
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
