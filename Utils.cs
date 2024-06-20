using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Win32;

namespace BBbuilder
{
    class Utils
    {
        public static string EXECUTINGFOLDER = AppDomain.CurrentDomain.BaseDirectory;
        public static string EXEPATH = Path.Combine(EXECUTINGFOLDER, "BBbuilder.exe");
        public static string SQPATH = Path.Combine(EXECUTINGFOLDER, "tools", "sq.exe");
        public static string BBRUSHERPATH = Path.Combine(EXECUTINGFOLDER, "tools", "bbrusher.exe");
        public static string BBSQPATH = Path.Combine(EXECUTINGFOLDER, "tools", "bbsq.exe");
        public static string NUTCRACKERPATH = Path.Combine(EXECUTINGFOLDER, "tools", "nutcracker.exe");
        public static string CONFIGPATH = Path.Combine(EXECUTINGFOLDER, "tools", "config.json");
        public static string BBSTEAMID = "365360";
        public static ConfigData Data { get; set; }


        public static bool KillAndStartBB()
        {
            KillBB();
            if (Data.UseSteam && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return StartFromSteam();
            }
            else
            {
                return StartFromExe();
            }
        }

        static void KillBB()
        {
            Process[] activeBBInstances = Process.GetProcessesByName("BattleBrothers");
            foreach (Process instance in activeBBInstances)
            {
                Console.WriteLine("Stopping BattleBrothers.exe...");
                instance.Kill();
            }
        }

        [SupportedOSPlatform("windows")]
        static bool StartFromSteam()
        {
            RegistryKey key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam", false);
            var folder = key.GetValue("InstallPath");
            if (folder == null)
            {
                Console.Error.WriteLine("Could not start via Steam: steam folder not found in registry!");
                return false;
            }
                  
            var exe = key.GetValue("InstallPath") + "\\steam.exe";
            Console.WriteLine(exe);
            using (Process startGame = new())
            {
                startGame.StartInfo.UseShellExecute = true;
                startGame.StartInfo.FileName = exe;
                startGame.StartInfo.Arguments = $"steam://rungameid/{BBSTEAMID}";
                startGame.Start();
            }
            return true;
        }

        static bool StartFromExe()
        {
            string bbFolder = Directory.GetParent(Data.GamePath).ToString();
            string bbExe = Path.Combine(bbFolder, "win32", "BattleBrothers.exe");
            if (!File.Exists(bbExe))
            {
                Console.Error.WriteLine($"Battle Brothers Exe not found under path {bbExe}! Check your data path! Current path:({Data.GamePath})");
                return false;
            }
            Console.WriteLine($"Starting Battle Brothers ({bbExe})");
            using (Process startGame = new())
            {
                startGame.StartInfo.UseShellExecute = true;
                startGame.StartInfo.FileName = bbExe;
                startGame.Start();
            }
            return true;
        }

        public static bool UpdatePathVariable()
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "where",
                Arguments = "bbbuilder",
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath()

            };
            string output;
            using (var process = Process.Start(processStartInfo))
            {
                process.WaitForExit();
                output = process.StandardOutput.ReadToEnd();
            }
            string[] envPaths = output.Split("\r\n").Where(l => l != "").Select(l => new FileInfo(l).Directory.FullName).ToArray();
            string currentBBBPath = "";
            if (envPaths.Length != 0)
            {
                currentBBBPath = envPaths[0];
                if (currentBBBPath + "\\" == EXECUTINGFOLDER)
                {
                    return false;
                }
            }

            // get user path environment
            var scope = EnvironmentVariableTarget.User;
            string oldPath = Environment.GetEnvironmentVariable("PATH", scope);
            // can be null, so init it
            if (oldPath == null)
            {
                oldPath = "";
            }

            string newPath;
            // path has changed; update with new path
            if (currentBBBPath != "" && oldPath != "" && oldPath.Contains(currentBBBPath))
            {
                Console.WriteLine($"Replacing old BBBuilder user %PATH% folder with new folder: {currentBBBPath} -> {EXECUTINGFOLDER}, please wait...");
                newPath = oldPath.Replace(currentBBBPath, EXECUTINGFOLDER);

            }
            // path wasn't in PATH, add path instead
            else
            {
                Console.WriteLine($"Adding BBBuilder folder to user %PATH%: {EXECUTINGFOLDER}, please wait...");
                newPath = oldPath + ";" + EXECUTINGFOLDER;
            }
            Environment.SetEnvironmentVariable("PATH", newPath, scope);
            Console.WriteLine($"BBBUILDER PATH HAS BEEN UPDATED - RESTART YOUR EDITOR / TERMINAL!");
            return true; // has changed
        }

        public static void CreateJSON()
        {
            Data = new ConfigData();
            WriteJSON(Data);
        }

        public static void WriteJSON(ConfigData _configData)
        {
            string jsonString = JsonSerializer.Serialize(_configData);
            File.WriteAllText(CONFIGPATH, jsonString);
        }

        public static void GetJsonData()
        {
            if (!File.Exists(CONFIGPATH))
            {
                CreateJSON();
            }
            Data = JsonSerializer.Deserialize<ConfigData>(File.ReadAllText(CONFIGPATH))!;
        }

        public static string ReadFile(string _path)
        {
            string fileAsString;
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(_path))
            using (StreamReader reader = new(stream))
            {
                fileAsString = reader.ReadToEnd();
            }
            return fileAsString;
        }

        public static void PrintHelp(Dictionary<string, Command> _commands)
        {
            foreach (KeyValuePair<string, Command> entry in _commands)
            {
                entry.Value.PrintHelp();
                Console.WriteLine("\n");
            }
            return;
        }

        public static bool IsGitInstalled()
        {
            try
            {
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = "git",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                };

                using (var process = Process.Start(processStartInfo))
                {
                    StreamReader sr = process.StandardError;
                    string output = sr.ReadToEnd();
                    process.WaitForExit();
                    return output.Length == 0;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }


        // copied from https://learn.microsoft.com/en-us/dotnet/api/system.io.directoryinfo?redirectedfrom=MSDN&view=net-6.0 
        public static void Copy(string sourceDirectory, string targetDirectory)
        {
            DirectoryInfo diSource = new(sourceDirectory);
            DirectoryInfo diTarget = new(targetDirectory);

            CopyAll(diSource, diTarget);
        }

        public static void CopyAll(DirectoryInfo source, DirectoryInfo target)
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
