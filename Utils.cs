using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

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

        public static string GamePath = "";
        public static string ModPath = "";
        public static string Folders = "";
        public static string[] FoldersArray = Array.Empty<string>();
        public static bool MoveZip = false;

        public static string ReadFile(string _path)
        {
            string fileAsString;
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream(_path))
            using (StreamReader reader = new StreamReader(stream))
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

        // copied from https://learn.microsoft.com/en-us/dotnet/api/system.io.directoryinfo?redirectedfrom=MSDN&view=net-6.0 
        public static void Copy(string sourceDirectory, string targetDirectory)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

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
