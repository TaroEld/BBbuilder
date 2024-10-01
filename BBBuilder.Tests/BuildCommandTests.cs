using System;
using System.IO;
using System.Collections.Generic;
using Xunit;
using Moq;
using System.IO.Compression;

namespace BBBuilder.Tests
{
    public class BuildCommandTests : IDisposable
    {
        private readonly string testRootPath;
        private readonly string testModPath;
        private BuildCommand buildCommand;

        public BuildCommandTests()
        {
            // Setup
            testRootPath = Path.Combine("G:\\Programming\\Csharp\\BBBuilder\\BBBuilder.Tests", "BBBuilderTests");
            testModPath = Path.Combine(testRootPath, "test_mod");
            Directory.CreateDirectory(testModPath);
            CreateTestModStructure();

            buildCommand = new BuildCommand();

            // Mock Utils.Data
            Utils.Data = new ConfigData
            {
                GamePath = Path.Combine(testRootPath, "data"),
                MoveZip = false,
                Verbose = true,
                LogTime = false
            };
        }

        private void CreateTestModStructure()
        {
            Utils.Copy("G:\\Programming\\Csharp\\BBBuilder\\BBBuilder.Tests\\test_mod", testModPath);
            Directory.CreateDirectory(Path.Combine(testRootPath, "data"));
        }

        [Fact]
        public void HandleCommand_ValidArguments_ReturnsTrue()
        {
            // Arrange
            string[] args = new[] { "build", testModPath };

            // Act
            bool result = buildCommand.HandleCommand(args);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(testModPath, "brushes", "test_brush.brush")));
            Assert.True(File.Exists(Path.Combine(testModPath, "gfx", "test_brush.png")));
            Assert.True(File.Exists(Path.Combine(testModPath, "test_mod.zip")));
            Assert.True(File.Exists(Path.Combine(Utils.Data.GamePath, "test_mod.zip")));
        }

        [Fact]
        public void CompileFiles_ChangedFiles_CompileSuccessfully()
        {
            // Arrange
            string[] args = new[] { "build", testModPath };
            buildCommand.HandleCommand(args);
            buildCommand = new BuildCommand();

            // Simulate changed file
            File.WriteAllText(Path.Combine(testModPath, "scripts", "mods_preload", "test.nut"), "// Updated nut file");
            buildCommand.HandleCommand(args);
            Assert.Single(buildCommand.FilesWhichChanged);
            using (ZipArchive zip = ZipFile.Open(buildCommand.ZipPath, ZipArchiveMode.Read))
            {
                Assert.Equal(3, zip.Entries.Count);
            }

            buildCommand = new BuildCommand();
            Directory.Delete(Path.Combine(testModPath, "unpacked_brushes"), true);
            buildCommand.HandleCommand(args);
            Assert.Equal(2, buildCommand.FilesWhichChanged.Count);
            Assert.False(File.Exists(Path.Combine(testModPath, "brushes", "test_brush.brush")));
            Assert.False(File.Exists(Path.Combine(testModPath, "gfx", "test_brush.png")));
            Assert.False(Directory.Exists(Path.Combine(testModPath, "brushes")));

            using (ZipArchive zip = ZipFile.Open(buildCommand.ZipPath, ZipArchiveMode.Read))
            {
                Assert.Single(zip.Entries);
            }
        }

        //[Fact]
        //public void ZipFiles_FilesExist_ZipCreatedSuccessfully()
        //{
        //    // Arrange
        //    string[] args = new[] { "build", testModPath };
        //    buildCommand.HandleCommand(args);

        //    // Act
        //    var methodInfo = typeof(BuildCommand).GetMethod("ZipFiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        //    bool result = (bool)methodInfo.Invoke(buildCommand, null);

        //    // Assert
        //    Assert.True(result);
        //    Assert.True(File.Exists(Path.Combine(testModPath, "test_mod.zip")));
        //}

        [Fact]
        public void CopyZipToData_ZipExists_CopiedSuccessfully()
        {
            // Arrange
            string[] args = new[] { "build", testModPath };
            buildCommand.HandleCommand(args);

            // Ensure zip file exists
            var zipMethodInfo = typeof(BuildCommand).GetMethod("ZipFiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            zipMethodInfo.Invoke(buildCommand, null);

            // Act
            var methodInfo = typeof(BuildCommand).GetMethod("CopyZipToData", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            bool result = (bool)methodInfo.Invoke(buildCommand, null);

            // Assert
            Assert.True(result);
            Assert.True(File.Exists(Path.Combine(Utils.Data.GamePath, "test_mod.zip")));
        }

        [Fact]
        public void GetDiffFiles_GitNotUsed_ReturnsEmptyList()
        {
            // Arrange
            string[] args = new[] { "build", testModPath, "-diff", "main,feature" };
            buildCommand.HandleCommand(args);

            // Act
            var methodInfo = typeof(BuildCommand).GetMethod("GetDiffFiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var result = (List<string>)methodInfo.Invoke(buildCommand, null);

            // Assert
            Assert.Empty(result);
        }

        public void Dispose()
        {
            // Cleanup
            TestUtils.SafeDeleteDirectory(testRootPath);
        }
    }
}
