using System;
using System.Collections.Generic;
using Xunit;
using BBBuilder;

namespace BBBuilder.Tests
{
    public class OptionFlagTests
    {
        [Fact]
        public void Constructor_SimpleFlag_SetsPropertiesCorrectly()
        {
            var flag = new OptionFlag("-flag", "Test flag");

            Assert.Equal("-flag", flag.Flag);
            Assert.Equal("-f", flag.FlagAlias);
            Assert.Null(flag.Parameter);
            Assert.Equal("Test flag", flag.Description);
            Assert.False(flag.Value);
        }

        [Fact]
        public void Constructor_FlagWithParameter_SetsPropertiesCorrectly()
        {
            var flag = new OptionFlag("-flag <param>", "Test flag with parameter");

            Assert.Equal("-flag", flag.Flag);
            Assert.Equal("-f", flag.FlagAlias);
            Assert.Equal("<param>", flag.Parameter);
            Assert.Equal("Test flag with parameter", flag.Description);
            Assert.False(flag.Value);
        }

        [Fact]
        public void Constructor_MultipleFlagsTest()
        {
            var flagWithParameter = new OptionFlag("-flag_with_parameter <param>", "Test flag with parameter");
            var flagWithoutParameter = new OptionFlag("-flag_without_parameter", "Test flag without parameter");
            var flagNotPassed = new OptionFlag("-flag_not_passed", "Test flag not passed");
            var aliasFlag = new OptionFlag("-aliasFlag", "Test flag alias");
            var args = new List<string> { "-flag_with_parameter", "param_value", "-flag_without_parameter", "-a", "flagAlias" };
            flagWithParameter.Validate(args);
            Assert.True(flagWithParameter.PositionalValue == "param_value");
            flagWithoutParameter.Validate(args);
            Assert.True(flagWithoutParameter.Value);
            Assert.True(flagWithoutParameter);
            flagNotPassed.Validate(args);
            Assert.False(flagNotPassed.Value);
            Assert.False(flagNotPassed);
            aliasFlag.Validate(args);
            Assert.True(aliasFlag.Value);
            Assert.True(aliasFlag);
        }

        [Fact]
        public void Validate_FlagPresent_SetsValueToTrue()
        {
            var flag = new OptionFlag("-flag", "Test flag");
            var args = new List<string> { "-flag", "otherarg" };

            flag.Validate(args);

            Assert.True(flag.Value);
            Assert.Single(args);
            Assert.Equal("otherarg", args[0]);
        }

        [Fact]
        public void Validate_FlagAliasPresent_SetsValueToTrue()
        {
            var flag = new OptionFlag("-flag", "Test flag");
            var args = new List<string> { "-f", "otherarg" };

            flag.Validate(args);

            Assert.True(flag.Value);
            Assert.Single(args);
            Assert.Equal("otherarg", args[0]);
        }

        [Fact]
        public void Validate_FlagNotPresent_ValueRemainsUnchanged()
        {
            var flag = new OptionFlag("-f", "Test flag");
            var args = new List<string> { "otherarg" };

            flag.Validate(args);

            Assert.False(flag.Value);
            Assert.Single(args);
            Assert.Equal("otherarg", args[0]);
        }

        [Fact]
        public void Validate_PositionalFlagWithValue_SetsPositionalValue()
        {
            var flag = new OptionFlag("-f <param>", "Test flag");
            var args = new List<string> { "-f", "value", "otherarg" };

            flag.Validate(args);

            Assert.True(flag.Value);
            Assert.Equal("value", flag.PositionalValue);
            Assert.Single(args);
            Assert.Equal("otherarg", args[0]);
        }

        [Fact]
        public void Validate_PositionalFlagWithoutValue_ThrowsException()
        {
            var flag = new OptionFlag("-f <param>", "Test flag");
            var args = new List<string> { "-f" };

            Assert.Throws<Exception>(() => flag.Validate(args));
        }

        [Fact]
        public void ToString_ReturnsCorrectString()
        {
            var flag = new OptionFlag("-f <param>", "Test flag");

            Assert.Equal("'-f (alias: -f) <param>': Test flag", flag.ToString());
        }

        [Fact]
        public void ImplicitBoolConversion_WorksCorrectly()
        {
            var flag = new OptionFlag("-f", "Test flag");
            Assert.False(flag);

            flag.Value = true;
            Assert.True(flag);
        }
    }
}