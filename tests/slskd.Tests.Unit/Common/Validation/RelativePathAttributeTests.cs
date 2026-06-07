using System;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using slskd.Validation;
using Xunit;

namespace slskd.Tests.Unit.Common.Validation;

public class RelativePathAttributeTests
{
    private static RelativePathAttribute CreateAttribute(OperatingSystem os, OperatingSystem? injected = null)
    {
        var attr = new RelativePathAttribute(os);

        if (injected.HasValue)
        {
            var field = typeof(RelativePathAttribute)
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
            var ex = Record.Exception(() => _ = new RelativePathAttribute(OperatingSystem.Current));

            Assert.Null(ex);
        }

        [Fact]
        public void AcceptsAnyOsValue()
        {
            var ex = Record.Exception(() => _ = new RelativePathAttribute(OperatingSystem.Any));

            Assert.Null(ex);
        }

        [Fact]
        public void AcceptsAllOsValue()
        {
            var ex = Record.Exception(() => _ = new RelativePathAttribute(OperatingSystem.All));

            Assert.Null(ex);
        }

        [Fact]
        public void ThrowsForWindowsOsValue()
        {
            Assert.Throws<ArgumentException>(() => new RelativePathAttribute(OperatingSystem.Windows));
        }

        [Fact]
        public void ThrowsForLinuxOsValue()
        {
            Assert.Throws<ArgumentException>(() => new RelativePathAttribute(OperatingSystem.Linux));
        }

        [Fact]
        public void ThrowsForNoneOsValue()
        {
            Assert.Throws<ArgumentException>(() => new RelativePathAttribute(OperatingSystem.None));
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

    public class ErrorMessages
    {
        [Fact]
        public void Current_Message_IncludesFieldName()
        {
            var (_, msg) = Validate("C:\\Music", OperatingSystem.Current, OperatingSystem.Windows);
            Assert.Contains("Field", msg);
        }

        [Fact]
        public void Current_Message_Text()
        {
            var (_, msg) = Validate("C:\\Music", OperatingSystem.Current, OperatingSystem.Windows);
            // Note: the implementation uses the same message for Current as for Any/All.
            // "on all operating systems" is misleading here since Current only validates
            // against the current (or injected) OS.
            Assert.Equal("The Field field must be a relative path on the current operating system.", msg);
        }

        [Fact]
        public void AnyAll_Message_MentionsRelativePath()
        {
            var (_, msg) = Validate("C:\\Music", OperatingSystem.Any);
            Assert.Contains("relative path", msg);
        }
    }

    // Current mode: validates using only the injected (or actual current) OS
    public class Current
    {
        public class WithWindowsInjected
        {
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
            [InlineData("/Music")]          // drive-relative on Windows, not absolute
            [InlineData("/home/user")]      // same — leading slash without drive letter is not absolute on Windows
            [InlineData("//server/share")]  // forward-slash UNC is not recognized as absolute on Windows
            public void RelativePaths_Pass(string value)
            {
                var (isValid, _) = Validate(value, OperatingSystem.Current, OperatingSystem.Windows);
                Assert.True(isValid);
            }

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
            public void AbsolutePaths_Fail(string value)
            {
                var (isValid, _) = Validate(value, OperatingSystem.Current, OperatingSystem.Windows);
                Assert.False(isValid);
            }
        }

        public class WithLinuxInjected
        {
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
            public void RelativePaths_Pass(string value)
            {
                var (isValid, _) = Validate(value, OperatingSystem.Current, OperatingSystem.Linux);
                Assert.True(isValid);
            }

            [Theory]
            [InlineData("/")]
            [InlineData("/home")]
            [InlineData("/home/user")]
            [InlineData("/Music")]
            [InlineData("//server/share")]
            [InlineData("//192.168.1.1/share")]
            public void AbsolutePaths_Fail(string value)
            {
                var (isValid, _) = Validate(value, OperatingSystem.Current, OperatingSystem.Linux);
                Assert.False(isValid);
            }
        }
    }

    // Any and All use identical logic: path must be relative on both Windows and Linux
    public class Any
    {
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
        public void TrulyRelativePaths_Pass(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Any);
            Assert.True(isValid);
        }

        // These are absolute on Windows, so they fail even though they're relative on Linux
        [Theory]
        [InlineData("C:\\Music")]
        [InlineData("C:/Music")]
        [InlineData("C:\\Music\\Artist\\Album")]
        [InlineData("C:\\")]
        [InlineData("Z:\\")]
        [InlineData("\\\\server\\share")]
        [InlineData("\\\\server\\share\\folder")]
        public void WindowsAbsolutePaths_Fail(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.Any);
            Assert.False(isValid);
        }

        // These are absolute on Linux, so they fail even though they're relative on Windows
        [Theory]
        [InlineData("/")]
        [InlineData("/home")]
        [InlineData("/home/user")]
        [InlineData("/Music")]
        [InlineData("//server/share")]
        [InlineData("//192.168.1.1/share")]
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
        [InlineData("My Music")]
        [InlineData("..")]
        [InlineData(".hidden")]
        [InlineData("...dir")]
        public void TrulyRelativePaths_Pass(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.All);
            Assert.True(isValid);
        }

        // These are absolute on Windows, so they fail even though they're relative on Linux
        [Theory]
        [InlineData("C:\\Music")]
        [InlineData("C:/Music")]
        [InlineData("C:\\Music\\Artist\\Album")]
        [InlineData("C:\\")]
        [InlineData("Z:\\")]
        [InlineData("\\\\server\\share")]
        [InlineData("\\\\server\\share\\folder")]
        public void WindowsAbsolutePaths_Fail(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.All);
            Assert.False(isValid);
        }

        // These are absolute on Linux, so they fail even though they're relative on Windows
        [Theory]
        [InlineData("/")]
        [InlineData("/home")]
        [InlineData("/home/user")]
        [InlineData("/Music")]
        [InlineData("//server/share")]
        [InlineData("//192.168.1.1/share")]
        public void LinuxAbsolutePaths_Fail(string value)
        {
            var (isValid, _) = Validate(value, OperatingSystem.All);
            Assert.False(isValid);
        }
    }
}
