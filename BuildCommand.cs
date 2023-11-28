using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Data.Sqlite;
using Ionic.Zip;

namespace BBbuilder
{
    class BuildCommand : Command
    {
        readonly string[] NotIndexedFolders = new string[] { ".bbbuilder", ".git", ".github", ".vscode", ".utils", "assets", "modtools", "node_modules" };
        readonly string[] ExcludedZipFolders = new string[] {".bbbuilder", ".git", ".github", "unpacked_brushes", ".vscode", ".utils", "assets", "modtools", "node_modules" };
        readonly string[] ExcludedScriptFolders = new string[] { "ui", ".git", ".github", "gfx", "preload", "brushes", "music", "sounds", "unpacked_brushes", "tempfolder", ".vscode", "nexus", ".utils", "assets" };
        readonly OptionFlag StartGame = new("-restart", "Exit and then start BattleBrothers.exe after building the mod.");
        readonly OptionFlag Transpile = new("-transpile", "Translate js file to es3. It allow you to use modern js syntax and features to create your mod.");
        readonly OptionFlag Rebuild = new("-rebuild", "Delete the database and the .zip to start from a clean slate.");
        readonly OptionFlag Diff = new("-diff <referencebranch>,<wipbranch>", "Create the zip based on the diff between <referencebranch> and <wipbranch> Pass them comma-separated WITHOUT SPACE INBETWEEN.");

        string ModPath;
        string ModName;
        string ZipPath;
        string BuildPath;
        string DBNAME = "dates.sqlite";
        string DB_path;
        string ConnectionString;
        Dictionary<string, DateTime> FileEditDatesInFolder;
        Dictionary<string, DateTime> FileEditDatesInDB;
        Dictionary<string, DateTime> FilesWhichChanged;
        public BuildCommand()
        {
            this.Name = "build";
            this.Description = "Builds your mod and creates a zip file that is copied to the data directory. Optionally can also simply compile the files.";
            this.Arguments = new string[]
            {
                "<modPath>: Specify the path of the mod to be built. (Example: bbuilder build G:/Games/BB/Mods/WIP/mod_msu)",
            };
            this.Flags = new OptionFlag[] {this.StartGame, this.Transpile, this.Rebuild, this.Diff};
            this.FileEditDatesInFolder = new();
            this.FileEditDatesInDB = new();
            this.FilesWhichChanged = new();
        }
        private bool ParseCommand(List<string> _args)
        {
            this.ParseFlags(_args);

            if (!Directory.Exists(_args[1]))
            {
                Console.WriteLine($"Passed mod path {_args[1]} does not exist!");
                return false;
            }
            this.ModPath = _args[1];
            this.BuildPath = this.ModPath;
            this.ModName = new DirectoryInfo(this.ModPath).Name;
            this.DB_path = Path.Combine(this.ModPath, ".bbbuilder", this.DBNAME);
            this.ConnectionString = $"Data Source={this.DB_path}";

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
            this.ZipPath = Path.Combine(this.BuildPath, this.ModName + ".zip");
            if (this.Diff)
                this.ZipPath = Path.Combine(this.BuildPath, this.ModName + "_patch.zip");
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
            if (this.Diff)
            {
                if (!Utils.IsGitInstalled())
                {
                    Console.WriteLine("Tried to use diff mode but git does not seem to be installed or accessible via PATH!");
                    return false;
                }
                string feature_branch_name = this.Diff.PositionalValue.Split(",")[1];
                string current_branch = this.GetCurrentGitBranch();
                if (feature_branch_name != current_branch)
                {
                    Console.WriteLine($"Tried to use diff mode with feature branch {feature_branch_name} but {current_branch} is checked out! Make sure to check out the feature branch.");
                    return false;
                }
            }
            if (Utils.Data.MoveZip && !this.Diff && File.Exists(Path.Combine(Utils.Data.GamePath, this.ModName + ".zip")) && !File.Exists(this.ZipPath))
            {
                Console.WriteLine("Copying zip from Data");
                File.Copy(Path.Combine(Utils.Data.GamePath, this.ModName + ".zip"), this.ZipPath);
            }
            if (this.Rebuild)
            {
                RebuildZipAndDB();
            }
            if (this.Diff)
                File.Delete(this.ZipPath);
            ReadFileDataFromFolder();
            SetupFileDateDB();
            ReadFileDataFromDB();
            GetFilesWhichChanged();
            Console.WriteLine($"Attempting to build {this.ModPath}");

            if (!CompileFiles())
            {
                Console.WriteLine("Failed while compiling files");
                return false;
            }
            if (this.Transpile && !this.TranspileToES3())
            {
                Console.WriteLine("Failed while transpiling to ES3!");
                return false;
            }

            if (!PackBrushFiles())
            {
                Console.WriteLine("Failed while packing brush files");
                return false;
            }
            if (!ZipFiles())
            {
                Console.WriteLine("Failed while zipping files");
                return false;
            }
            if (!CopyZipToData())
            {
                Console.WriteLine("Failed while copying new zip to data!");
                return false;
            }
            if (this.StartGame)
            {
                KillAndStartBB();
            }
            this.InsertFileData();
            return true;
        }

        public void RebuildZipAndDB()
        {
            if (File.Exists(this.DB_path))
            {
                SqliteConnection.ClearAllPools();
                File.Delete(this.DB_path);
                SetupFileDateDB();
            } 
            if (File.Exists(this.ZipPath))
                File.Delete(this.ZipPath);
            Console.WriteLine("Rebuilding: Deleted .zip and database");
        }

        private void SetupFileDateDB()
        {
            if (!Directory.Exists(Path.Combine(this.BuildPath, ".bbbuilder")))
            {
                Directory.CreateDirectory(Path.Combine(this.BuildPath, ".bbbuilder"));
            }
            using (var connection = new SqliteConnection(this.ConnectionString))
            {
                connection.Open();
                bool use;
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='FileData';";
                    use = command.ExecuteScalar() == null;
                }
                if (use)
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = @"
                        CREATE TABLE FileData (
                            FilePath TEXT PRIMARY KEY,
                            LastModified DATETIME NOT NULL
                        );";
                        command.ExecuteNonQuery();
                    }
                }
            }
        }

        private void ReadFileDataFromFolder()
        {
            foreach (var folder in GetAllFoldersExcept(this.NotIndexedFolders))
            {
                foreach(var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                {
                    this.FileEditDatesInFolder.Add(Path.GetRelativePath(this.BuildPath, file), File.GetLastWriteTime(file));
                }
            }
        }

        private void ReadFileDataFromDB()
        {
            using (var connection = new SqliteConnection(this.ConnectionString))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT FilePath, LastModified FROM FileData;";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string filePath = reader.GetString(0);
                            DateTime lastModified = reader.GetDateTime(1);
                            this.FileEditDatesInDB.Add(filePath, lastModified);
                        }
                    }
                }
            }
        }

        private void GetFilesWhichChanged()
        {
            foreach (var entry in this.FileEditDatesInFolder)
            {
                if (!(this.FileEditDatesInDB.ContainsKey(entry.Key)) || this.FileEditDatesInDB[entry.Key] != entry.Value)
                    this.FilesWhichChanged.Add(entry.Key, entry.Value);
            }
        }

        private bool HasFileChanged(string filePath)
        {
            return this.FilesWhichChanged.ContainsKey(Path.GetRelativePath(this.BuildPath, filePath));
        }

        private void InsertFileData()
        {
            if (this.FilesWhichChanged.Count == 0) return;
            using (var connection = new SqliteConnection(this.ConnectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText =
                        @"
                            INSERT OR REPLACE INTO FileData (FilePath, LastModified) VALUES ($FilePath, $LastModified);
                        ";
                        var path_p = command.CreateParameter();
                        path_p.ParameterName = "$FilePath";
                        command.Parameters.Add(path_p);
                        var mod_p = command.CreateParameter();
                        mod_p.ParameterName = "$LastModified";
                        command.Parameters.Add(mod_p);
                        foreach (var pathtime in this.FilesWhichChanged)
                        {
                            path_p.Value = pathtime.Key;
                            mod_p.Value = pathtime.Value;
                            command.ExecuteNonQuery();
                        }
                        transaction.Commit();
                    }
                }
            }
        }
        private bool CompileFiles()
        {
            //get this script directory
            Console.WriteLine("Starting to compile files...");
            string[] allNutFilesAsPath = GetAllowedScriptFiles();
            string[] changedNutFiles = allNutFilesAsPath.Where(f => HasFileChanged(f)).ToArray();
            if (changedNutFiles.Length == 0)
            {
                Console.WriteLine("No files to compile!");
                return true;
            }

            bool noCompileErrors = true;
            Parallel.For(0, changedNutFiles.Length, (i, state) =>
            {
                string nutFilePath = changedNutFiles[i];
                string sqCommand = String.Format("-o NUL -c \"{0}\"", nutFilePath);

                using (Process compiling = new())
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
                    else Console.WriteLine("Successfully compiled file " + nutFilePath);
                }
            });
            
            if (noCompileErrors)
            {
                Console.WriteLine("Successfully compiled files!");
            }  

            return noCompileErrors;
        }

        private bool TranspileToES3()
        {
            Console.WriteLine("Starting to transpile to ES3...");
            string localWorkingDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string[] dependencies = new string[] { "@babel/cli", "@babel/preset-env", "browserify", "core-js" };

            if (!CheckNpmPresence()) return false;

            Stopwatch es3Watch = new();
            es3Watch.Start();
            Console.WriteLine("-- Check npm dependencies...");
            if (!CheckNpmDependencies(dependencies, localWorkingDirectory)) return false;
            Console.WriteLine($"-- Check npm dependencies took {es3Watch.Elapsed.TotalSeconds} s");
            string babelLoc = Path.Combine(localWorkingDirectory, "node_modules", ".bin", "babel");
            string browserifyLoc = Path.Combine(localWorkingDirectory, "node_modules", ".bin", "browserify");

            Console.WriteLine("-- Transpile from modern JS to old JS...");
            es3Watch.Restart();
            using (Process compiling = new())
            {
                compiling.StartInfo.UseShellExecute = true;
                compiling.StartInfo.FileName = babelLoc;
                compiling.StartInfo.Arguments = String.Format("\"{0}\" --out-dir \"{1}\" --config-file \"{2}\"", this.BuildPath, this.BuildPath, Path.Combine(localWorkingDirectory, "babel.config.json"));
                compiling.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                compiling.Start();
                compiling.WaitForExit();
            }
            Console.WriteLine($"-- Transpile from modern JS to old JS took {es3Watch.Elapsed.TotalSeconds} s");

            Console.WriteLine("-- Browserify the transpilation result...");
            es3Watch.Restart();
            using (Process compiling = new())
            {
                compiling.StartInfo.UseShellExecute = true;
                compiling.StartInfo.FileName = browserifyLoc;
                compiling.StartInfo.Arguments = String.Format("\"{0}\" -o \"{0}\"", Path.Combine(this.BuildPath, "ui/mods/", ModName, "index.js"));
                compiling.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                compiling.Start();
                compiling.WaitForExit();
            }
            Console.WriteLine($"-- Browserify the transpilation result took {es3Watch.Elapsed.TotalSeconds} s");

            if(Directory.Exists(Path.Combine(BuildPath, "node_modules"))){
                es3Watch.Restart();
                Console.WriteLine("-- Remove node_modules from buidPath...");
                //Directory.Delete(Path.Combine(BuildPath, "node_modules"), true);
                Console.WriteLine($"-- Remove node_modules took {es3Watch.Elapsed.TotalSeconds} s");
            }
            es3Watch.Stop();

            return true;
        }
        private bool PackBrushFiles()
        {
            string brushesPath = Path.Combine(this.BuildPath, "brushes");
            string folderPath = Path.Combine(this.BuildPath, "unpacked_brushes");
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
                string[] changedFiles = Directory.GetFiles(subFolder, "*", SearchOption.AllDirectories).Where(f => HasFileChanged(f)).ToArray();
                if (changedFiles.Length == 0) {
                    return;
                }

                string folderName = new DirectoryInfo(subFolder).Name;
                string brushName = $"{folderName}.brush";
                string command = $"pack {brushName} {subFolder}";

                if (File.Exists(Path.Combine(brushesPath, brushName)))
                {
                    File.Delete(Path.Combine(brushesPath, brushName));
                }
                using (Process packBrush = new())
                {
                    packBrush.StartInfo.UseShellExecute = false;
                    packBrush.StartInfo.RedirectStandardOutput = true;
                    packBrush.StartInfo.FileName = Utils.BBRUSHERPATH;
                    packBrush.StartInfo.Arguments = command;
                    packBrush.StartInfo.WorkingDirectory = this.BuildPath;
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
                        File.Copy(Path.Combine(this.BuildPath, brushName), Path.Combine(brushesPath, brushName));
                        File.Delete(Path.Combine(this.BuildPath, brushName));
                    }
                }
            });
            DirectoryInfo wipFolder = Directory.GetParent(this.BuildPath);
            if (Directory.Exists(Path.Combine(wipFolder.ToString(), "gfx")))
            {
                Utils.Copy(Path.Combine(wipFolder.ToString(), "gfx"), Path.Combine(this.BuildPath, "gfx"));
                Directory.Delete(Path.Combine(wipFolder.ToString(), "gfx"), true);
            }
            if (noCompileErrors)
                Console.WriteLine("Successfully packed brush files!");
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
                return ret.Select(f => Path.Combine(this.BuildPath, f)).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
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

        private List<string> GetFilesToZip()
        {
            List<string> files;
            if (this.Diff)
                return this.GetDiffFiles().Where(f => !f.Contains("unpacked_brushes")).ToList();
            else files = this.FileEditDatesInFolder.Keys.Select(f => Path.Combine(this.BuildPath, f)).ToList();
            files = files.Where(f => !f.Contains("unpacked_brushes")).ToList();

            if (!File.Exists(this.ZipPath))
                return files;

            bool recreateZip = false;
            using (var zip = ZipFile.Read(this.ZipPath))
            {
                foreach (ZipEntry entry in zip)
                {
                    if (entry.IsDirectory) continue;
                    string name = entry.FileName.Replace("/", @"\");
                    if (!this.FileEditDatesInFolder.ContainsKey(name))
                    {
                        Console.WriteLine($"Found a file in .zip that is not in the folder, re-creating zip to make sure: {name}");
                        recreateZip = true;
                        break;
                    }
                }
            }
            if (recreateZip)
            {
                RebuildZipAndDB();
                return files;
            }
            files = files.Where(f => this.FilesWhichChanged.ContainsKey(Path.GetRelativePath(this.BuildPath, f))).ToList();
            return files;
        }

        private bool ZipFiles()
        {
            List<string> toZip = GetFilesToZip();
            using (var zip = new ZipFile(this.ZipPath))
            {
                foreach (string file in toZip)
                { 
                    
                    var relativePath = Path.GetDirectoryName(Path.GetRelativePath(this.BuildPath, file));
                    if (this.Diff && !File.Exists(file))
                    {
                        Console.WriteLine("Skipping file in zip due to -diff: " + file);
                    }
                    else
                    {
                        Console.WriteLine("Updating file in zip: " + file);
                        zip.UpdateFile(file, relativePath);
                    } 
                }
                zip.Save();
            }
            Console.WriteLine($"Successfully zipped {this.ModPath} ({this.ZipPath})!");
            return true;
        }

        private bool CopyZipToData()
        {
            string gamePath = Utils.Data.GamePath;
            string zipName = $"{this.ModName}.zip";
            if(this.Diff) zipName = $"{this.ModName}_patch.zip";
            string dataZipPath = Path.Combine(gamePath, zipName);
            if (File.Exists(dataZipPath))
            {
                File.Delete(dataZipPath);
            }
            if (Utils.Data.MoveZip)
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
            string bbFolder = Directory.GetParent(Utils.Data.GamePath).ToString();
            string bbExe = Path.Combine(bbFolder, "win32", "BattleBrothers.exe");
            Console.WriteLine($"Starting Battle Brothers ({bbExe})");
            using (Process startGame = new())
            {
                startGame.StartInfo.UseShellExecute = true;
                startGame.StartInfo.FileName = bbExe;
                startGame.Start();
            }
        }

        new public void CleanUp()
        {
            if (File.Exists(this.ZipPath))
            {
                File.Delete(this.ZipPath);
                Console.WriteLine($"Removed file {this.ZipPath}");
            }
            string brushesPath = Path.Combine(this.BuildPath, "brushes");
            if (Directory.Exists(brushesPath))
            {
                Directory.Delete(brushesPath, true);
                Console.WriteLine($"Removed folder {brushesPath}");
            }
            DirectoryInfo wipFolder = Directory.GetParent(this.BuildPath);
            string wipGfxPath = Path.Combine(wipFolder.ToString(), "gfx");
            if (Directory.Exists(wipGfxPath))
            {
                Directory.Delete(wipGfxPath, true);
                Console.WriteLine($"Removed folder {wipGfxPath}");
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
            String[] retArray = ret.ToArray();
            return retArray;
        }
    }
}
