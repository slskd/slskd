using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using slskd.Validation;
using Xunit;

namespace slskd.Tests.Unit.Common.Validation;

public class AbsolutePathAttributeTests
{
    private static AbsolutePathAttribute CreateAttribute(OperatingSystem os, OperatingSystem? injected = null)
    {
        var attr = new AbsolutePathAttribute(os);

        if (injected.HasValue)
        {
            var field = typeof(AbsolutePathAttribute)
                .GetField("<Injected>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(attr, injected.Value);
        }

        return attr;
    }

    private static (bool IsValid, string ErrorMessage) Validate(string value, OperatingSystem os, OperatingSystem? injected = null)
    {
        var attr = CreateAttribute(os, injected);
        var context = new ValidationContext(new object()) { DisplayName = "Field", MemberName = "Field" };
        var result = attr.GetValidationResult(value, context);
        return (result == null, result?.ErrorMessage);
    }

    public class Constructor
    {
        [Fact]
        public void AcceptsCurrentOsValue()
        {
            var ex = Record.Exception(() => _ = new AbsolutePathAttribute(OperatingSystem.Current));

            Assert.Null(ex);
        }

        [Fact]
        public void AcceptsAnyOsValue()
        {
            var ex = Record.Exception(() => _ = new AbsolutePathAttribute(OperatingSystem.Any));

            Assert.Null(ex);
        }

        [Fact]
        public void AcceptsAllOsValue()
        {
            var ex = Record.Exception(() => _ = new AbsolutePathAttribute(OperatingSystem.All));

            Assert.Null(ex);
        }

        [Fact]
        public void ThrowsForWindowsOsValue()
        {
            Assert.Throws<ArgumentException>(() => new AbsolutePathAttribute(OperatingSystem.Windows));
        }

        [Fact]
        public void ThrowsForLinuxOsValue()
        {
            Assert.Throws<ArgumentException>(() => new AbsolutePathAttribute(OperatingSystem.Linux));
        }

        [Fact]
        public void ThrowsForNoneOsValue()
        {
            Assert.Throws<ArgumentException>(() => new AbsolutePathAttribute(OperatingSystem.None));
        }
    }

    public class NullOrEmpty
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Passes_ForCurrentWithWindowsInjected(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Current, OperatingSystem.Windows);
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Passes_ForCurrentWithLinuxInjected(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Current, OperatingSystem.Linux);
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Passes_ForAny(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Any);
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Passes_ForAll(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.All);
            Assert.True(isValid);
        }
    }

    // Current mode: validates using only the injected (or actual current) OS
    public class Current
    {
        public class WithWindowsInjected
        {
            [Theory]
            [InlineData("C:\\Music")]
            [InlineData("C:/Music")]
            [InlineData("C:\\Music\\Artist\\Album")]
            [InlineData("C:\\")]
            [InlineData("Z:\\")]
            [InlineData("\\\\server\\share")]
            [InlineData("\\\\server\\share\\folder")]
            [InlineData("\\\\192.168.1.1\\share")]
            [InlineData("\\\\server")]   // bare UNC (server only, no share) — StartsWith("\\") → absolute
            public void AbsolutePaths_Pass(string value)
            {
                var (isValid, _) = Validate(value, OperatingSystem.Current, OperatingSystem.Windows);
                Assert.True(isValid);
            }

            [Theory]
            [InlineData("subdir")]
            [InlineData("sub/dir")]
            [InlineData("sub\\dir")]
            [InlineData("Artist\\Album")]
            [InlineData("music.flac")]
            [InlineData("My Music")]
            [InlineData("..")]
            [InlineData(".hidden")]
            [InlineData("...dir")]
            [InlineData("\\Music")]         // drive-relative on Windows, not absolute
            [InlineData("/Music")]          // same
            [InlineData("/home/user")]      // same — leading slash without drive letter is not absolute on Windows
            [InlineData("//server/share")]  // forward-slash UNC is not recognized as absolute on Windows
            [InlineData("C:")]
            [InlineData("C:Music")]
            public void NonAbsolutePaths_Fail(string value)
            {
                var (isValid, _) = Validate(value, OperatingSystem.Current, OperatingSystem.Windows);
                Assert.False(isValid);
            }
        }

        public class WithLinuxInjected
        {
            [Theory]
            [InlineData("/")]
            [InlineData("/home")]
            [InlineData("/home/user")]
            [InlineData("/Music")]
            [InlineData("//server/share")]
            [InlineData("//192.168.1.1/share")]
            public void AbsolutePaths_Pass(string value)
            {
                var (isValid, _) = Validate(value, OperatingSystem.Current, OperatingSystem.Linux);
                Assert.True(isValid);
            }

            [Theory]
            [InlineData("subdir")]
            [InlineData("sub/dir")]
            [InlineData("sub\\dir")]
            [InlineData("Artist\\Album")]
            [InlineData("music.flac")]
            [InlineData("My Music")]
            [InlineData("..")]
            [InlineData(".hidden")]
            [InlineData("...dir")]
            [InlineData("C:\\Music")]       // backslash paths are relative on Linux
            [InlineData("C:/Music")]        // starts with 'C', not '/' — relative on Linux
            [InlineData("\\\\server\\share")] // backslash UNC is relative on Linux
            [InlineData("\\")]
            [InlineData("\\home")]
            public void NonAbsolutePaths_Fail(string value)
            {
                var (isValid, _) = Validate(value, OperatingSystem.Current, OperatingSystem.Linux);
                Assert.False(isValid);
            }
        }
    }

    public class ErrorMessages
    {
        [Fact]
        public void Current_Message_MentionsCurrentOperatingSystem()
        {
            var (_, msg) = Validate("relative/path", OperatingSystem.Current, OperatingSystem.Windows);
            Assert.Contains("current operating system", msg);
        }

        [Fact]
        public void Current_Message_IncludesFieldName()
        {
            var (_, msg) = Validate("relative/path", OperatingSystem.Current, OperatingSystem.Windows);
            Assert.Contains("Field", msg);
        }

        [Fact]
        public void AnyAll_Message_MentionsAllOperatingSystems()
        {
            var (_, msg) = Validate("/home/user", OperatingSystem.Any);
            Assert.Contains("all operating systems", msg);
        }
    }

    // Any and All use identical logic: path must be absolute on both Windows and Linux.
    // No common path satisfies this — Windows uses drive letters or \\ UNC, Linux uses /,
    // and no single path form is recognized as absolute by both detection rules.
    public class Any
    {
        [Fact]
        public void UsesAndLogic_NoPathCanEverPass_BecauseLinuxAndWindowsAbsoluteFormsAreIncompatible()
        {
            // If Any used OR-logic ("absolute on at least one OS"), a Linux-absolute path would pass.
            // Any actually requires absolute on ALL checked OSes (AND-logic), identical to All.
            // No path form satisfies both: Linux requires '/', Windows requires a drive letter or '\\'.
            var (linuxAbsPath, _) = Validate("/home/user", OperatingSystem.Any);
            var (winAbsPath, _) = Validate("C:\\Music", OperatingSystem.Any);
            Assert.False(linuxAbsPath);
            Assert.False(winAbsPath);
        }

        [Theory]
        [InlineData("subdir")]
        [InlineData("sub/dir")]
        [InlineData("sub\\dir")]
        [InlineData("Artist\\Album")]
        [InlineData("music.flac")]
        [InlineData("..")]
        [InlineData(".hidden")]
        public void RelativePaths_Fail(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Any);
            Assert.False(isValid);
        }

        // These are absolute on Windows but relative on Linux — fail because not absolute on both
        [Theory]
        [InlineData("C:\\Music")]
        [InlineData("C:/Music")]
        [InlineData("C:\\")]
        [InlineData("\\\\server\\share")]
        public void WindowsAbsolutePaths_Fail(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Any);
            Assert.False(isValid);
        }

        // These are absolute on Linux but relative on Windows — fail because not absolute on both
        [Theory]
        [InlineData("/home/user")]
        [InlineData("/Music")]
        [InlineData("//server/share")]
        [InlineData("/")]
        public void LinuxAbsolutePaths_Fail(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Any);
            Assert.False(isValid);
        }
    }

    public class All
    {
        [Theory]
        [InlineData("subdir")]
        [InlineData("sub/dir")]
        [InlineData("sub\\dir")]
        [InlineData("Artist\\Album")]
        [InlineData("music.flac")]
        [InlineData("..")]
        [InlineData(".hidden")]
        public void RelativePaths_Fail(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.All);
            Assert.False(isValid);
        }

        // These are absolute on Windows but relative on Linux — fail because not absolute on both
        [Theory]
        [InlineData("C:\\Music")]
        [InlineData("C:/Music")]
        [InlineData("C:\\")]
        [InlineData("\\\\server\\share")]
        public void WindowsAbsolutePaths_Fail(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.All);
            Assert.False(isValid);
        }

        // These are absolute on Linux but relative on Windows — fail because not absolute on both
        [Theory]
        [InlineData("/home/user")]
        [InlineData("/Music")]
        [InlineData("//server/share")]
        [InlineData("/")]
        public void LinuxAbsolutePaths_Fail(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.All);
            Assert.False(isValid);
        }
    }
}
