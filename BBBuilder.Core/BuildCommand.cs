using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
//using Ionic.Zip;
using Force.Crc32;
using System.Text.Json;
using System.Collections;
using System.Collections.Concurrent;

namespace BBBuilder
{
    public class BuildCommand : Command
    {
        readonly string[] NotIndexedFolders = new string[] { ".bbbuilder", ".git", ".github", ".vscode", ".utils", "assets", "modtools", "node_modules" };

        string[] ExcludedZipFolders = new string[] {"unpacked_brushes"};
        string[] IncludedZipFolders = new string[] {};
        string[] NormalizedExcludedZipFolders;
        string[] NormalizedIncludedZipFolders;
        readonly string[] ExcludedScriptFolders = new string[] { "ui", ".git", ".github", "gfx", "preload", "brushes", "music", "sounds", "unpacked_brushes", "tempfolder", ".vscode", "nexus", ".utils", "assets" };
        public readonly OptionFlag StartGame = new("-restart", "Exit and then start BattleBrothers.exe after building the mod.") { FlagAlias = "-rs"};
        public readonly OptionFlag Transpile = new("-transpile", "Translate js file to es3. It allow you to use modern js syntax and features to create your mod.");
        public readonly OptionFlag Rebuild = new("-rebuild", "Delete the database and the .zip to start from a clean slate.") { FlagAlias = "-rb" };
        public readonly OptionFlag Diff = new("-diff <referencebranch>,<wipbranch>", "Create the zip based on the diff between <referencebranch> and <wipbranch> Pass them comma-separated WITHOUT SPACE INBETWEEN. This requires the `git` command to be available via cmd. The purpose of this is creating patches.");
        public readonly OptionFlag CustomZipName = new("-zipname <name>", "Name of the resulting zip file. .zip extension is added. If not specified, mod name is used.") { FlagAlias = "-z" };
        public readonly OptionFlag ExcludeFolders = new("-excludedfolders <folderName1,[folderName2],...>", "Folders to remove from finished zip. The folder 'unpacked_brushes' will always be removed.") { FlagAlias = "-ex" };
        public readonly OptionFlag IncludeFolders = new("-includedfolders <folderName1,[folderName2],...>", "Folders to include into finished zip. To be used in combination with -excludedfolders when you want to add just a specific sub-folder, for example excluding all of scripts/ except for scripts/to_include/.") { FlagAlias = "-in" };

        string ModPath;
        string ModName;
        string ZipName;
        public string ZipPath;
        string BuildPath;
        // Brushes and their gfx are packed into this staging folder (not into the mod's own gfx/ and brushes/ folders) and zipped from there.
        string BrushOutputPath;
        string StagedBrushesPath;
        string StagedGfxPath;
        // Zip-relative path (e.g. "gfx\name.png") -> full path of the generated file in the staging folder
        Dictionary<string, string> GeneratedFiles = new();
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
            this.BrushOutputPath = Path.Combine(this.ModPath, ".bbbuilder", "brush_output");
            this.StagedBrushesPath = Path.Combine(this.BrushOutputPath, "brushes");
            this.StagedGfxPath = Path.Combine(this.BrushOutputPath, "gfx");
            if (this.ExcludeFolders)
            {
                ExcludedZipFolders = ExcludedZipFolders.Concat(ExcludeFolders.PositionalValue.Split(',')).ToArray();
            }
            this.NormalizedExcludedZipFolders = ExcludedZipFolders.Select(f => Utils.Norm(f)).ToArray();
            if (this.IncludeFolders)
            {
                IncludedZipFolders = IncludedZipFolders.Concat(IncludeFolders.PositionalValue.Split(',')).ToArray();
            }
            this.NormalizedIncludedZipFolders = IncludedZipFolders.Select(f => Utils.Norm(f)).ToArray();
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
                if (Directory.Exists(this.BrushOutputPath))
                    Directory.Delete(this.BrushOutputPath, true);
                Utils.LogTime($"BuildCommand: Deleting zip, database and packed brushes");
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
            if (!PackBrushFiles())
            {
                Utils.WriteRed("Failed while packing brush files");
                return false;
            }
            Utils.LogTime($"BuildCommand: Packing brush files");

            AddGeneratedFilesToHashes();
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
            Console.WriteLine($"Mod '{this.ModName}' built successfully.");
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
                foreach (string s in sameZipNameInDataAllowed)
                {
                    Utils.WriteGreen(Path.GetFileName(s));
                }
            }

            if (sameZipNameInDataNotAllowed.Length > 0)
            {
                Utils.WriteRed("Found other .zip files in data that seem to be the same mod!");
                Utils.WriteGreen("Current name: " + this.ZipName);
                Utils.WriteRed("Similar zipe file(s):");
                foreach (string s in sameZipNameInDataNotAllowed)
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
            var files = folders.SelectMany(folder =>
                Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories));

            var result = new ConcurrentDictionary<string, Int64>();

            Parallel.ForEach(files, file =>
            {
                var relativePath = Path.GetRelativePath(this.BuildPath, file);
                var checksum = CalculateChecksum(file);
                result.TryAdd(relativePath, checksum);
            });

            return new Dictionary<string, Int64>(result);
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

        // Packs every unpacked_brushes/<name> folder into the staging folder (brushes/<name>.brush and gfx/<name>.png).
        // The mod's own gfx/ and brushes/ folders are never touched.
        private bool PackBrushFiles()
        {
            string unpackedBrushesPath = Path.Combine(this.BuildPath, "unpacked_brushes");
            string[] unpackedBrushesSubFolders = Directory.Exists(unpackedBrushesPath) ? Directory.GetDirectories(unpackedBrushesPath) : Array.Empty<string>();
            string[] unpackedBrushesSubFoldersNameOnly = unpackedBrushesSubFolders.Select(Path.GetFileName).ToArray();

            Directory.CreateDirectory(this.StagedBrushesPath);
            Directory.CreateDirectory(this.StagedGfxPath);

            // delete packed brushes and gfx whose unpacked_brushes folder doesn't exist anymore
            foreach (string file in Directory.GetFiles(this.StagedBrushesPath).Concat(Directory.GetFiles(this.StagedGfxPath)))
            {
                if (!unpackedBrushesSubFoldersNameOnly.Contains(Path.GetFileNameWithoutExtension(file)))
                {
                    Utils.VerbosePrint($"Deleting packed file {Path.GetFileName(file)} as no corresponding unpacked_brushes folder was found.");
                    File.Delete(file);
                }
            }
            if (unpackedBrushesSubFolders.Length == 0)
            {
                Console.WriteLine("No brush files to pack!");
                return true;
            }

            string[] existingBrushes = Directory.GetFiles(this.StagedBrushesPath).Select(Path.GetFileName).ToArray();
            string[] existingGfx = Directory.GetFiles(this.StagedGfxPath).Select(Path.GetFileName).ToArray();
            int failedCount = 0;
            int packedCount = 0;
            ConcurrentQueue<string> outputBuffer = new();

            Parallel.For(0, unpackedBrushesSubFolders.Length, (i) =>
            {
                string subFolder = unpackedBrushesSubFolders[i];
                string nameOnly = Path.GetFileName(subFolder);
                bool hasBrush = existingBrushes.Contains(nameOnly + ".brush");
                bool hasGfx = existingGfx.Contains(nameOnly + ".png");
                bool hasChanges = Directory.EnumerateFiles(subFolder, "*", SearchOption.AllDirectories).Any(f => HasFileChanged(f));
                if (!hasChanges && hasBrush && hasGfx)
                {
                    return;
                }
                File.Delete(Path.Combine(this.StagedBrushesPath, nameOnly + ".brush"));
                File.Delete(Path.Combine(this.StagedGfxPath, nameOnly + ".png"));
                Interlocked.Increment(ref packedCount);

                string brushName = $"{nameOnly}.brush";
                string command = $"pack --gfxPath \"{this.BrushOutputPath}\" \"brushes/{brushName}\" \"{subFolder}\"";

                using (Process packBrush = new())
                {
                    packBrush.StartInfo.UseShellExecute = false;
                    packBrush.StartInfo.RedirectStandardOutput = true;
                    packBrush.StartInfo.FileName = Utils.BBRUSHERPATH;
                    packBrush.StartInfo.Arguments = command;
                    packBrush.StartInfo.WorkingDirectory = this.BrushOutputPath;
                    packBrush.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    packBrush.StartInfo.CreateNoWindow = true;
                    packBrush.Start();
                    string output = packBrush.StandardOutput.ReadToEnd();
                    packBrush.WaitForExit();
                    if (packBrush.ExitCode == 2)
                    {
                        outputBuffer.Enqueue($"Error building brush {brushName}!");
                        outputBuffer.Enqueue(output);
                        Interlocked.Increment(ref failedCount);
                    }
                    else
                    {
                        outputBuffer.Enqueue($"Packed Brush {brushName}");
                    }
                }
            });

            if (failedCount > 0)
            {
                Utils.WriteRed("Errors while packing brushes!\n-------------------------------------");
                foreach (string line in outputBuffer)
                    Utils.WriteRed(line);
                Utils.WriteRed("-------------------------------------");
                return false;
            }
            if (packedCount > 0)
                Console.WriteLine("Successfully packed brush files!");
            else
                Console.WriteLine("Brush files didn't change!");
            return true;
        }

        // Adds the packed brushes and gfx from the staging folder to the hash dictionaries under their zip-relative path.
        private void AddGeneratedFilesToHashes()
        {
            this.GeneratedFiles.Clear();
            var stagedFolders = new[] { ("brushes", this.StagedBrushesPath), ("gfx", this.StagedGfxPath) };
            foreach (var (zipFolder, stagedFolder) in stagedFolders)
            {
                if (!Directory.Exists(stagedFolder))
                    continue;
                foreach (string file in Directory.GetFiles(stagedFolder))
                {
                    string relativePath = Path.Combine(zipFolder, Path.GetFileName(file));
                    Int64 checksum = CalculateChecksum(file);
                    if (File.Exists(Path.Combine(this.BuildPath, relativePath)))
                    {
                        Utils.WriteRed($"Warning: {relativePath} exists in the mod and is also generated from unpacked_brushes/{Path.GetFileNameWithoutExtension(file)}. The generated file is used in the zip. If it was generated by an older BBBuilder version, delete it from the mod.");
                    }
                    this.GeneratedFiles[relativePath] = file;
                    this.FilesHashesInFolder[relativePath] = checksum;
                    if (this.FileHashesInDB.TryGetValue(relativePath, out Int64 dbChecksum) && dbChecksum == checksum)
                        this.FilesWhichChanged.Remove(relativePath);
                    else
                        this.FilesWhichChanged[relativePath] = checksum;
                }
            }
        }

        // Full path of the file that is zipped as relativePath: generated brushes/gfx come from the staging folder.
        private string GetSourcePath(string relativePath)
        {
            return this.GeneratedFiles.TryGetValue(relativePath, out string stagedPath) ? stagedPath : Path.Combine(this.BuildPath, relativePath);
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
        // _relativePath is relative to the mod root (BuildPath), with either separator.
        private static bool IsFileInFolderList(string _relativePath, string[] _folderList)
        {
            string relativePath = Utils.Norm(_relativePath).TrimStart(Path.DirectorySeparatorChar);
            return _folderList.Any(folder => relativePath.StartsWith(folder));
        }

        // Whether a file is kept out of the zip: -excludedfolders excludes, -includedfolders overrides it.
        private bool IsExcludedFromZip(string _relativePath)
        {
            return IsFileInFolderList(_relativePath, NormalizedExcludedZipFolders)
                && !IsFileInFolderList(_relativePath, NormalizedIncludedZipFolders);
        }

        // Returns the relative paths (as used in the hash dictionaries) of the changed files that go into the zip.
        private List<string> GetChangedFiles()
        {
            List<string> files;
            if (this.Diff)
            {
                files = GetDiffFiles().Select(f => Path.GetRelativePath(this.BuildPath, f)).ToList();
                // add packed brushes and their gfx, as they are not tracked by git
                List<string> generatedFiles = new();
                foreach (string file in files)
                {
                    string[] directories = file.Split(Path.DirectorySeparatorChar);
                    int index = Array.IndexOf(directories, "unpacked_brushes");
                    if (index < 0 || index + 2 >= directories.Length)
                        continue;
                    string brushName = directories[index + 1];
                    generatedFiles.Add(Path.Combine("brushes", brushName + ".brush"));
                    generatedFiles.Add(Path.Combine("gfx", brushName + ".png"));
                }
                files.AddRange(generatedFiles);
                files = files.Distinct().ToList();
            }
            else files = this.FilesHashesInFolder.Keys.ToList();
            return files.Where(f => this.FilesWhichChanged.ContainsKey(f) && !IsExcludedFromZip(f)).ToList();
        }

        private bool ZipFiles()
        {
            List<string> changedFiles = GetChangedFiles();
            int addedOrChangedCount = 0;
            int removedCount = 0;

            ZipArchiveMode zipMode = File.Exists(this.ZipPath) ? ZipArchiveMode.Update : ZipArchiveMode.Create;

            using (ZipArchive zip = ZipFile.Open(this.ZipPath, zipMode))
            {
                // Step 1: Clean up the zip - remove files that no longer exist or are excluded
                if (zipMode == ZipArchiveMode.Update)
                {
                    removedCount = RemoveObsoleteEntries(zip);
                }

                // Step 2: Add/update changed files
                addedOrChangedCount += AddOrUpdateFiles(zip, changedFiles, zipMode);

                // Step 3: Re-add files that are missing, for example due to previously being excluded
                if (zipMode == ZipArchiveMode.Update)
                {
                    addedOrChangedCount += AddMissingFiles(zip);
                }
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

                // Remove if file no longer exists in our tracked files, or if it's excluded
                if (!this.FilesHashesInFolder.ContainsKey(relativePath) || IsExcludedFromZip(relativePath))
                {
                    Utils.VerbosePrint("Removing file in zip: " + entry.FullName);
                    entry.Delete();
                    removedCount++;
                }
            }

            return removedCount;
        }

        private int AddOrUpdateFiles(ZipArchive zip, List<string> relativePaths, ZipArchiveMode zipMode)
        {
            int count = 0;

            foreach (string relativePath in relativePaths)
            {
                string sourcePath = GetSourcePath(relativePath);
                if (this.Diff && !File.Exists(sourcePath))
                {
                    Console.WriteLine("Skipping file in zip due to -diff: " + sourcePath);
                    continue;
                }

                string zipPath = relativePath.Replace(@"\", @"/");

                Utils.VerbosePrint("Updating file in zip: " + sourcePath + "(" + zipPath + ")");
                // Remove existing entry if it exists and we're updating an existing mod
                if (zipMode == ZipArchiveMode.Update)
                {
                    zip.GetEntry(zipPath)?.Delete();
                }

                zip.CreateEntryFromFile(sourcePath, zipPath);
                count++;
            }

            return count;
        }

        private int AddMissingFiles(ZipArchive zip)
        {
            int count = 0;

            foreach (string relativePath in this.FilesHashesInFolder.Keys)
            {
                string zipPath = relativePath.Replace(@"\", @"/");

                if (IsExcludedFromZip(relativePath))
                    continue;

                // Skip if file is already in the zip
                if (zip.GetEntry(zipPath) != null)
                    continue;

                // This file should be in the zip but isn't - add it
                string sourcePath = GetSourcePath(relativePath);
                if (File.Exists(sourcePath))
                {
                    Utils.VerbosePrint("Adding missing file: " + sourcePath + "(" + zipPath + ")");
                    zip.CreateEntryFromFile(sourcePath, zipPath);
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
