namespace slskd.Tests.Unit.Stream
{
    using slskd.Stream.API;
    using Xunit;

    public class StreamControllerTests
    {
        public class GuessContentType
        {
            [Theory]
            [InlineData("song.mp3",  "audio/mpeg")]
            [InlineData("song.MP3",  "audio/mpeg")]
            [InlineData("song.flac", "audio/flac")]
            [InlineData("song.FLAC", "audio/flac")]
            [InlineData("song.ogg",  "audio/ogg")]
            [InlineData("song.m4a",  "audio/mp4")]
            [InlineData("song.wav",  "audio/wav")]
            public void Returns_Correct_MimeType_For_Known_Extension(string filename, string expected)
            {
                Assert.Equal(expected, StreamController.GuessContentType(filename));
            }

            [Theory]
            [InlineData("song.xyz")]
            [InlineData("song.txt")]
            [InlineData("noextension")]
            [InlineData("")]
            public void Returns_OctetStream_For_Unknown_Extension(string filename)
            {
                Assert.Equal("application/octet-stream", StreamController.GuessContentType(filename));
            }

            [Theory]
            [InlineData(@"C:\Music\Artist\Album\song.mp3",  "audio/mpeg")]
            [InlineData(@"C:\Music\Artist\Album\song.flac", "audio/flac")]
            public void Handles_Windows_Paths_Correctly(string filename, string expected)
            {
                // Soulseek filenames are Windows paths — extension must be extracted from the full path
                Assert.Equal(expected, StreamController.GuessContentType(filename));
            }
        }
    }
}
