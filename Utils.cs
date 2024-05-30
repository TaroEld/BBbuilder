using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;

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
        public static string CONFIGPATH = Path.Combine(Utils.EXECUTINGFOLDER, "tools", "config.json");
        public static ConfigData Data { get; set; }


        public static bool KillAndStartBB()
        {
            Process[] activeBBInstances = Process.GetProcessesByName("BattleBrothers");
            foreach (Process instance in activeBBInstances)
            {
                Console.WriteLine("Stopping BattleBrothers.exe...");
                instance.Kill();
            }
            string bbFolder = Directory.GetParent(Utils.Data.GamePath).ToString();
            string bbExe = Path.Combine(bbFolder, "win32", "BattleBrothers.exe");
            if (!File.Exists(bbExe))
            {
                Console.Error.WriteLine($"Battle Brothers Exe not found under path {bbExe}! Check your data path! Current path:({Utils.Data.GamePath})");
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

        public static void CreateJSON()
        {
            Utils.Data = new ConfigData();
            Utils.WriteJSON(Utils.Data);
        }

        public static void WriteJSON(ConfigData _configData)
        {
            string jsonString = JsonSerializer.Serialize(_configData);
            File.WriteAllText(Utils.CONFIGPATH, jsonString);
        }

        public static void GetJsonData()
        {
            if (!File.Exists(Utils.CONFIGPATH))
            {
                Utils.CreateJSON();
            }
            Utils.Data = JsonSerializer.Deserialize<ConfigData>(File.ReadAllText(Utils.CONFIGPATH))!;
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
