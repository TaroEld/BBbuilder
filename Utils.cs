using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Threading;
using Microsoft.Win32;

namespace BBbuilder
{
    public class Utils
    {
        public static readonly string EXECUTINGFOLDER = AppDomain.CurrentDomain.BaseDirectory;
        public static readonly string EXEPATH = Path.Combine(EXECUTINGFOLDER, "BBbuilder.exe");
        public static readonly string SQPATH = Path.Combine(EXECUTINGFOLDER, "tools", "sq_taro.exe");
        public static readonly string BBRUSHERPATH = Path.Combine(EXECUTINGFOLDER, "tools", "bbrusher.exe");
        public static readonly string BBSQPATH = Path.Combine(EXECUTINGFOLDER, "tools", "bbsq.exe");
        public static readonly string NUTCRACKERPATH = Path.Combine(EXECUTINGFOLDER, "tools", "nutcracker.exe");
        public static readonly string CONFIGPATH = Path.Combine(EXECUTINGFOLDER, "tools", "config.json");
        public static readonly string BBSTEAMID = "365360";
        public static readonly bool DEBUG = false;
        public static ConfigData Data { get; set; }
        public static Stopwatch Stopwatch = new Stopwatch();
        public static TimeSpan LastTime;

        public static void LogTime(string msg)
        {
            if (Data.LogTime)
            {
                string delta = LastTime != TimeSpan.MinValue ? (Stopwatch.Elapsed - LastTime).Milliseconds.ToString() : "";
                Console.WriteLine($"TIME::{msg} {Stopwatch.Elapsed.Milliseconds} ms (d {delta})");
                LastTime = Stopwatch.Elapsed;
            }
        }

        public static void VerbosePrint(string msg)
        {
            if (Data.Verbose)
                Console.WriteLine(msg);
        }

        public static bool KillAndStartBB()
        {
            if (!KillBB())
            {
                Console.WriteLine("Failed to stop Battle Brothers. Please close it manually and try again.");
                return false;
            }

            if (Data.UseSteam && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return StartFromSteam();
            }
            else
            {
                return StartFromExe();
            }
        }

        public static Process[] getBBProcesses()
        {
            return Process.GetProcessesByName("BattleBrothers");
        }

        public static bool KillBB()
        {
            do
            {
                Process[] activeBBInstances = getBBProcesses();
                foreach (Process instance in activeBBInstances)
                {
                    Console.WriteLine("Stopping BattleBrothers.exe...");
                    try
                    {
                        instance.Kill();
                        
                        if (!instance.WaitForExit(5000))  // Wait up to 5 seconds
                        {
                            Console.WriteLine("Process did not exit in time.");
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error stopping process: {ex.Message}");
                        return false;
                    }
                }
                Thread.Sleep(25);
            } while (getBBProcesses().Length > 0);

            return true;
        }

        [SupportedOSPlatform("windows")]
        public static bool StartFromSteam()
        {
            using (var key = Registry.LocalMachine.OpenSubKey("SOFTWARE\\WOW6432Node\\Valve\\Steam", false))
            {
                if (key == null)
                {
                    Console.Error.WriteLine("Could not start via Steam: Steam registry key not found!");
                    return false;
                }

                var folder = key.GetValue("InstallPath") as string;
                if (string.IsNullOrEmpty(folder))
                {
                    Console.Error.WriteLine("Could not start via Steam: Steam installation folder not found in registry!");
                    return false;
                }

                var exe = Path.Combine(folder, "steam.exe");
                if (!File.Exists(exe))
                {
                    Console.Error.WriteLine($"Could not start via Steam: {exe} not found!");
                    return false;
                }

                try
                {
                    using (Process startGame = new Process())
                    {
                        startGame.StartInfo.UseShellExecute = true;
                        startGame.StartInfo.FileName = exe;
                        startGame.StartInfo.Arguments = $"steam://rungameid/{BBSTEAMID}";
                        startGame.Start();
                    }
                    return true;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Error starting Steam: {ex.Message}");
                    return false;
                }
            }
        }

        public static bool StartFromExe()
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
            var scope = EnvironmentVariableTarget.User;
            var oldPath = Environment.GetEnvironmentVariable("PATH", scope);
            var folders = new HashSet<string>(oldPath.Split(';', StringSplitOptions.RemoveEmptyEntries));

            bool hasChanged = false;
            bool hasExecutingFolder = folders.Contains(EXECUTINGFOLDER);

            folders.RemoveWhere(folder =>
            {
                if (folder != EXECUTINGFOLDER && File.Exists(Path.Combine(folder, "BBbuilder.exe")))
                {
                    Console.WriteLine($"Removing folder from %PATH%: {folder}");
                    hasChanged = true;
                    return true;
                }
                return false;
            });

            if (!hasExecutingFolder)
            {
                Console.WriteLine($"Adding BBBuilder folder to user %PATH%: {EXECUTINGFOLDER}");
                folders.Add(EXECUTINGFOLDER);
                hasChanged = true;
            }

            if (hasChanged)
            {
                Console.WriteLine("Updating PATH environment variable, please wait...");
                Environment.SetEnvironmentVariable("PATH", string.Join(';', folders), scope);
                Console.WriteLine("BBBUILDER PATH HAS BEEN UPDATED - RESTART YOUR EDITOR / TERMINAL!");
            }

            return hasChanged;
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

        public static bool IsGitInstalled()
        {
            string fileName;
            string arguments;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                fileName = "cmd.exe";
                arguments = "/c where git";
            }
            else
            {
                fileName = "/bin/sh";
                arguments = "-c \"command -v git\"";
            }

            try
            {
                using (var process = new Process())
                {
                    process.StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    return process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output);
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string Norm(string _str)
        {
            return _str.Replace('/', Path.DirectorySeparatorChar);
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
