using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using BBBuilder;

namespace BBBuilder.Tests
{
    // Concrete implementation of Command for testing
    public class TestCommand : Command
    {
        public readonly OptionFlag TestFlag = new("-t", "Test flag");
        public readonly OptionFlag TestFlagWithParam = new("-p <param>", "Test flag with parameter");

        public TestCommand()
        {
            Name = "test";
            Description = "Test command";
            Arguments = new string[] { "arg1", "arg2" };
        }
    }

    public class CommandTests
    {
        private TestCommand command;

        public CommandTests()
        {
            command = new TestCommand();
        }

        [Fact]
        public void Constructor_InitializesFlags()
        {
            Assert.Equal(2, command.Flags.Length);
            Assert.Contains(command.Flags, f => f.Flag == "-t");
            Assert.Contains(command.Flags, f => f.Flag == "-p");
        }

        [Fact]
        public void HandleCommand_NoArguments_PrintsHelpAndReturnsFalse()
        {
            var output = CaptureConsoleOutput(() =>
            {
                bool result = command.HandleCommand(new string[] { "test" });
                Assert.False(result);
            });

            Assert.Contains("No argument passed. Printing help of test:", output);
            Assert.Contains("Test command", output);
        }

        [Fact]
        public void HandleCommand_WithArguments_ReturnsTrue()
        {
            bool result = command.HandleCommand(new string[] { "test", "arg" });
            Assert.True(result);
        }

        [Fact]
        public void ParseFlags_ValidFlags_SetsFlags()
        {
            var args = new List<string> { "test", "arg", "-t", "-p", "param" };
            command.ParseFlags(args);

            Assert.True(command.TestFlag.Value);
            Assert.True(command.TestFlagWithParam.Value);
            Assert.Equal("param", command.TestFlagWithParam.PositionalValue);
            Assert.Equal(2, args.Count); // "test" and "arg" should remain
        }

        [Fact]
        public void ParseFlags_UnknownArgs_PrintsWarning()
        {
            var args = new List<string> { "test", "arg", "-t", "--unknown" };
            var output = CaptureConsoleOutput(() => command.ParseFlags(args));

            Assert.Contains("Unknown args/flags found:", output);
            Assert.Contains("--unknown", output);
        }

        [Fact]
        public void CleanUp_DoesNotThrowException()
        {
            var exception = Record.Exception(() => command.CleanUp());
            Assert.Null(exception);
        }

        // Helper method to capture console output
        private string CaptureConsoleOutput(Action action)
        {
            var originalOutput = Console.Out;
            using (var consoleOutput = new StringWriter())
            {
                Console.SetOut(consoleOutput);
                action();
                Console.SetOut(originalOutput);
                return consoleOutput.ToString();
            }
        }
    }
}