using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BBBuilder.Tests
{
    internal class TestUtils
    {
        public static void SafeDeleteDirectory(string path)
        {
            if (!Directory.Exists(path))
                return;

            foreach (string subDir in Directory.GetDirectories(path))
            {
                SafeDeleteDirectory(subDir);
            }

            foreach (string file in Directory.GetFiles(path))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            Directory.Delete(path, false);
        }
    }
}
