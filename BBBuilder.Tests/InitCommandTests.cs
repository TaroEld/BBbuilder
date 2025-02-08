using System;
using System.IO;
using System.Linq;
using Xunit;
using Moq;
using System.Diagnostics;
using System.Text.Json;
using BBBuilder;

namespace BBBuilder.Tests
{
    public class InitCommandTests : IDisposable
    {
        private readonly string testRootPath;
        private readonly InitCommand initCommand;

        public InitCommandTests()
        {
            testRootPath = Path.Combine("G:\\Programming\\Csharp\\BBBuilder\\BBBuilder.Tests", "BBBuilderTests");
            if (Directory.Exists(testRootPath))
                TestUtils.SafeDeleteDirectory(testRootPath);
            Directory.CreateDirectory(testRootPath);
            initCommand = new InitCommand();
            SetupUtils();
        }

        internal void SetupUtils()
        {
            // Mock Utils.Data
            Utils.Data = new ConfigData();
            Utils.Data.ModPath = Path.Combine(testRootPath, "Mods");
            Utils.Data.FoldersArray = new[] { "folder1", "folder2" };

            // Set Utils.EXECUTINGFOLDER
            Utils.EXECUTINGFOLDER = "G:\\Programming\\Csharp\\BBBuilder\\BBBuilder.Core";
            Assert.True(Directory.Exists(Utils.EXECUTINGFOLDER));
            Utils.EXEPATH = Path.Combine(Utils.EXECUTINGFOLDER, "BBBuilder.exe");
            Utils.SQPATH = Path.Combine(Utils.EXECUTINGFOLDER, "tools", "sq_taro.exe");
            Utils.BBRUSHERPATH = Path.Combine(Utils.EXECUTINGFOLDER, "tools", "bbrusher.exe");
            Utils.BBSQPATH = Path.Combine(Utils.EXECUTINGFOLDER, "tools", "bbsq.exe");
            Utils.NUTCRACKERPATH = Path.Combine(Utils.EXECUTINGFOLDER, "tools", "nutcracker.exe");
            Utils.CONFIGPATH = Path.Combine(Utils.EXECUTINGFOLDER, "tools", "config.json");
            Utils.BBSTEAMID = "365360";
        }

        public void Dispose()
        {
            // Clean up after tests
            TestUtils.SafeDeleteDirectory(testRootPath);
            TestUtils.SafeDeleteDirectory(Path.Combine(Utils.EXECUTINGFOLDER, "Templates", "testrunner"));
        }

        [Fact]
        public void HandleCommand_ValidArguments_ReturnsTrue()
        {
            string[] args = new[] { "init", "test_mod" };
            Assert.True(initCommand.HandleCommand(args));
        }

        [Fact]
        public void HandleCommand_ExistingDirectory_ReturnsFalse()
        {
            string modPath = Path.Combine(Utils.Data.ModPath, "existing_mod");
            Directory.CreateDirectory(modPath);

            string[] args = new[] { "init", "existing_mod" };
            Assert.False(initCommand.HandleCommand(args));
        }

        [Fact]
        public void HandleCommand_OverwriteExistingDirectory_ReturnsTrue()
        {
            string modPath = Path.Combine(Utils.Data.ModPath, "overwrite_mod");
            Directory.CreateDirectory(modPath);

            string[] args = new[] { "init", "overwrite_mod", "-overwrite" };
            Assert.True(initCommand.HandleCommand(args));
        }

        [Fact]
        public void HandleCommand_AltPath_CreatesModInSpecifiedDirectory()
        {
            string altPath = Path.Combine(testRootPath, "AltMods");
            Directory.CreateDirectory(altPath);

            string[] args = new[] { "init", "alt_mod", "-directory", altPath };
            Assert.True(initCommand.HandleCommand(args));

            string expectedModPath = Path.Combine(altPath, "alt_mod");
            Assert.True(Directory.Exists(expectedModPath));
        }

        [Fact]
        public void HandleCommand_CustomTemplate_UsesSpecifiedTemplate()
        {
            string templatePath = Path.Combine(Utils.EXECUTINGFOLDER, "Templates", "testrunner");
            Directory.CreateDirectory(templatePath);
            File.WriteAllText(Path.Combine(templatePath, "custom_file.txt"), "Custom content");

            string[] args = new[] { "init", "custom_mod", "-template", "testrunner" };
            Assert.True(initCommand.HandleCommand(args));

            string expectedModPath = Path.Combine(Utils.Data.ModPath, "custom_mod");
            Assert.True(File.Exists(Path.Combine(expectedModPath, "custom_file.txt")));
        }

        [Fact]
        public void HandleCommand_CreatesExtraDirectories()
        {
            string[] args = new[] { "init", "extra_dirs_mod" };
            Assert.True(initCommand.HandleCommand(args));

            string modPath = Path.Combine(Utils.Data.ModPath, "extra_dirs_mod");
            Assert.True(Directory.Exists(Path.Combine(modPath, ".vscode")));
            Assert.True(Directory.Exists(Path.Combine(modPath, "assets")));
            Assert.True(Directory.Exists(Path.Combine(modPath, "unpacked_brushes")));
        }

        [Fact]
        public void HandleCommand_CreatesProjectFiles()
        {
            string[] args = new[] { "init", "project_files_mod" };
            Assert.True(initCommand.HandleCommand(args));

            string modPath = Path.Combine(Utils.Data.ModPath, "project_files_mod");
            Assert.True(File.Exists(Path.Combine(modPath, "project_files_mod.sublime-project")));
            Assert.True(File.Exists(Path.Combine(modPath, ".vscode", "project_files_mod.code-workspace")));
        }

        [Fact]
        public void HandleCommand_InitializesGitRepo()
        {
            string[] args = new[] { "init", "git_mod" };
            Assert.True(initCommand.HandleCommand(args));

            string modPath = Path.Combine(Utils.Data.ModPath, "git_mod");
            Assert.True(Directory.Exists(Path.Combine(modPath, ".git")));
        }

        [Fact]
        public void GetNameSpaceName_ConvertsUnderscoresToPascalCase()
        {
            var privateGetNameSpaceName = GetPrivateMethod<Func<string, string>>(initCommand, "GetNameSpaceName");

            Assert.Equal("TestMod", privateGetNameSpaceName("test_mod"));
            Assert.Equal("MyAwesomeMod", privateGetNameSpaceName("my_awesome_mod"));
            Assert.Equal("Mod", privateGetNameSpaceName("mod"));
        }

        private T GetPrivateMethod<T>(object obj, string methodName) where T : Delegate
        {
            var method = obj.GetType().GetMethod(methodName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (T)Delegate.CreateDelegate(typeof(T), obj, method);
        }
    }

    // Mock interfaces for dependency injection
    public interface IUtils
    {
        bool IsGitInstalled();
    }

    public interface IUtilsData
    {
        string ModPath { get; set; }
        string[] FoldersArray { get; set; }
    }
}