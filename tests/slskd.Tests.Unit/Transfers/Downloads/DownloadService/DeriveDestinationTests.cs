namespace slskd.Tests.Unit.Transfers.Downloads;

using System;
using System.IO;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using slskd.Search;
using slskd.Transfers;
using slskd.Transfers.Downloads;
using Xunit;

public partial class DownloadServiceTests
{
    public class DeriveDestination
    {
        [Fact]
        public async Task Explicit_Destination_Takes_Precedence_Over_Subdirectory_Pattern()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { Destination = "explicit-dir" } };
            var (service, _) = GetFixture("${SOURCE_USERNAME}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("explicit-dir", result);
        }

        [Fact]
        public async Task Explicit_Destination_Tokens_Are_Not_Expanded()
        {
            // explicit destination is sanitized but not token-substituted
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { Destination = "${SOURCE_USERNAME}" } };
            var (service, _) = GetFixture("${SOURCE_USERNAME}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("${SOURCE_USERNAME}", result);
        }

        [Fact]
        public async Task Explicit_Destination_Is_Always_Relative()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { Destination = "/etc/passwd" } };
            var (service, _) = GetFixture("irrelevant", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.False(FileSafety.IsPathAbsolute(result));
        }

        [Fact]
        public async Task Explicit_Destination_Traversal_Segments_Do_Not_Survive()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { Destination = "../../../etc/passwd" } };
            var (service, _) = GetFixture("irrelevant", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.False(FileSafety.ContainsTraversalSegments(result));
        }

        [Fact]
        public async Task Explicit_Destination_Null_Byte_Replaced()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { Destination = "down\0loads" } };
            var (service, _) = GetFixture("irrelevant", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("down_loads", result);
        }

        [Fact]
        public async Task SOURCE_USERNAME_Is_Substituted()
        {
            var (service, _) = GetFixture("${SOURCE_USERNAME}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3" });

            Assert.Equal("alice", result);
        }

        [Fact]
        public async Task SOURCE_USERNAME_Pattern_Match_Is_Case_Insensitive()
        {
            var (service, _) = GetFixture("${source_username}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3" });

            Assert.Equal("alice", result);
        }

        [Fact]
        public async Task SOURCE_USERNAME_Forward_Slash_Replaced_With_Underscore()
        {
            var (service, _) = GetFixture("${SOURCE_USERNAME}");
            var result = await service.DeriveDestination(new Transfer { Username = "al/ice", Filename = "@alice\\track.mp3" });

            Assert.Equal("al_ice", result);
        }

        [Fact]
        public async Task SOURCE_USERNAME_Backslash_Replaced_With_Underscore()
        {
            var (service, _) = GetFixture("${SOURCE_USERNAME}");
            var result = await service.DeriveDestination(new Transfer { Username = "al\\ice", Filename = "@alice\\track.mp3" });

            Assert.Equal("al_ice", result);
        }

        [Fact]
        public async Task SOURCE_USERNAME_Null_Byte_Replaced_With_Underscore()
        {
            var (service, _) = GetFixture("${SOURCE_USERNAME}");
            var result = await service.DeriveDestination(new Transfer { Username = "al\0ice", Filename = "@alice\\track.mp3" });

            Assert.Equal("al_ice", result);
        }

        [Theory]
        [InlineData("al\\..\\ice")]
        [InlineData("al/../ice")]
        [InlineData("al\\../ice")]
        [InlineData("al/..\\ice")]
        public async Task SOURCE_USERNAME_Traversal_Segment_Periods_Remains_After_Replacing_Slashes_With_Underscore(string username)
        {
            var (service, _) = GetFixture("${SOURCE_USERNAME}");
            var result = await service.DeriveDestination(new Transfer { Username = username, Filename = "@alice\\track.mp3" });

            Assert.Equal("al_.._ice", result);
        }

        [Fact]
        public async Task SOURCE_DIRECTORY_Is_Immediate_Parent_Of_Remote_Filename_Backslash()
        {
            var (service, _) = GetFixture("${SOURCE_DIRECTORY}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\Music\\album\\track.mp3" });

            Assert.Equal("album", result);
        }

        [Fact]
        public async Task SOURCE_DIRECTORY_Is_Immediate_Parent_Of_Remote_Filename_Forward_Slash()
        {
            var (service, _) = GetFixture("${SOURCE_DIRECTORY}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice/Music/album/track.mp3" });

            Assert.Equal("album", result);
        }

        [Fact]
        public async Task SOURCE_DIRECTORY_Is_Empty_If_File_Is_In_Root()
        {
            var (service, _) = GetFixture("${SOURCE_DIRECTORY}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "track.mp3" });

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task SOURCE_PATH_Is_Full_Directory_Of_Remote_Filename()
        {
            var (service, _) = GetFixture("${SOURCE_PATH}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\Music\\album\\track.mp3" });

            Assert.Equal(Path.Combine("@alice", "Music", "album"), result);
        }

        [Fact]
        public async Task SOURCE_PATH_Is_Empty_If_File_Is_In_Root()
        {
            var (service, _) = GetFixture("${SOURCE_PATH}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "track.mp3" });

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public async Task SOURCE_PATH_And_SOURCE_DIRECTORY_Match_If_File_Is_In_One_Subdirectory()
        {
            var (service, _) = GetFixture("${SOURCE_PATH}-${SOURCE_DIRECTORY}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3" });

            Assert.Equal("@alice-@alice", result);
        }

        [Fact]
        public async Task BATCH_ID_Is_Substituted_When_Batch_Exists()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId };
            var (service, _) = GetFixture("${BATCH_ID}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal(batchId.ToString(), result);
        }

        [Fact]
        public async Task BATCH_ID_Is_Fallback_When_No_Batch()
        {
            var (service, _) = GetFixture("${BATCH_ID}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3" });
            Assert.Equal("unknown_batch_id", result);
        }

        [Fact]
        public async Task BATCH_ID_Is_Fallback_When_Batch_Id_Is_Empty_Guid()
        {
            var batch = new Batch { Id = Guid.Empty };
            var (service, _) = GetFixture("${BATCH_ID}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = Guid.Empty });

            Assert.Equal("unknown_batch_id", result);
        }

        [Fact]
        public async Task BATCH_EXTERNAL_ID_Is_Substituted_When_Batch_Has_External_Id()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { ExternalId = "my-ext-id" } };
            var (service, _) = GetFixture("${BATCH_EXTERNAL_ID}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("my-ext-id", result);
        }

        [Fact]
        public async Task BATCH_EXTERNAL_ID_Is_Fallback_When_No_Batch()
        {
            var (service, _) = GetFixture("${BATCH_EXTERNAL_ID}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3" });

            Assert.Equal("unknown_batch_external_id", result);
        }

        [Fact]
        public async Task BATCH_EXTERNAL_ID_Is_Fallback_When_External_Id_Is_Null()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { ExternalId = null } };
            var (service, _) = GetFixture("${BATCH_EXTERNAL_ID}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("unknown_batch_external_id", result);
        }

        [Fact]
        public async Task BATCH_EXTERNAL_ID_Is_Fallback_When_External_Id_Is_Whitespace()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { ExternalId = "   " } };
            var (service, _) = GetFixture("${BATCH_EXTERNAL_ID}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("unknown_batch_external_id", result);
        }

        [Fact]
        public async Task BATCH_EXTERNAL_ID_Forward_Slash_In_Value_Replaced_With_Underscore()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { ExternalId = "foo/bar" } };
            var (service, _) = GetFixture("${BATCH_EXTERNAL_ID}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("foo_bar", result);
        }

        [Fact]
        public async Task BATCH_EXTERNAL_ID_Backslash_In_Value_Replaced_With_Underscore()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { ExternalId = "foo\\bar" } };
            var (service, _) = GetFixture("${BATCH_EXTERNAL_ID}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("foo_bar", result);
        }

        [Fact]
        public async Task BATCH_EXTERNAL_ID_Null_Byte_In_Value_Replaced_With_Underscore()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { ExternalId = "foo\0bar" } };
            var (service, _) = GetFixture("${BATCH_EXTERNAL_ID}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("foo_bar", result);
        }

        [Fact]
        public async Task SEARCH_ID_Is_Substituted_When_Search_Exists()
        {
            var searchId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = searchId };
            var search = new Search { Id = searchId, SearchText = "some query" };
            var (service, _) = GetFixture("${SEARCH_ID}", batch: batch, search: search);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal(searchId.ToString(), result);
        }

        [Fact]
        public async Task SEARCH_ID_Is_Fallback_When_No_Batch()
        {
            // no batch → SearchId is never consulted
            var (service, _) = GetFixture("${SEARCH_ID}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3" });

            Assert.Equal("unknown_search_id", result);
        }

        [Fact]
        public async Task SEARCH_ID_Is_Fallback_When_Batch_Has_No_SearchId()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = null };
            var (service, _) = GetFixture("${SEARCH_ID}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("unknown_search_id", result);
        }

        [Fact]
        public async Task SEARCH_ID_Is_Fallback_When_Search_Not_Found()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = Guid.NewGuid() };
            var (service, _) = GetFixture("${SEARCH_ID}", batch: batch, search: null);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("unknown_search_id", result);
        }

        [Fact]
        public async Task SEARCH_ID_Is_Fallback_When_Search_ID_Is_Empty_Guid()
        {
            var searchId = Guid.Empty;
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = searchId };
            var search = new Search { Id = searchId, SearchText = "some query" };
            var (service, _) = GetFixture("${SEARCH_ID}", batch: batch, search: search);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("unknown_search_id", result);
        }

        [Fact]
        public async Task SEARCH_TEXT_Is_Substituted_When_Search_Exists()
        {
            var searchId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = searchId };
            var search = new Search { Id = searchId, SearchText = "foo bar baz" };
            var (service, _) = GetFixture("${SEARCH_TEXT}", batch: batch, search: search);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("foo bar baz", result);
        }

        [Fact]
        public async Task SEARCH_TEXT_Is_Fallback_When_No_Search()
        {
            var (service, _) = GetFixture("${SEARCH_TEXT}");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3" });

            Assert.Equal("unknown_search_text", result);
        }

        [Fact]
        public async Task SEARCH_TEXT_Forward_Slash_In_Value_Replaced_With_Underscore()
        {
            var searchId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = searchId };
            var search = new Search { Id = searchId, SearchText = "foo/bar" };
            var (service, _) = GetFixture("${SEARCH_TEXT}", batch: batch, search: search);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("foo_bar", result);
        }

        [Fact]
        public async Task SEARCH_TEXT_Backlash_In_Value_Replaced_With_Underscore()
        {
            var searchId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = searchId };
            var search = new Search { Id = searchId, SearchText = "foo\\bar" };
            var (service, _) = GetFixture("${SEARCH_TEXT}", batch: batch, search: search);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("foo_bar", result);
        }

        [Fact]
        public async Task Batch_With_Null_Options_Falls_Through_To_Pattern()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = null };
            var (service, _) = GetFixture("${SOURCE_USERNAME}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("alice", result);
        }

        [Fact]
        public async Task Batch_With_Whitespace_Destination_Falls_Through_To_Pattern()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { Destination = "   " } };
            var (service, _) = GetFixture("${SOURCE_USERNAME}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("alice", result);
        }

        [Fact]
        public async Task Pattern_Forward_Slash_Separator_Preserved_In_Output()
        {
            var (service, _) = GetFixture("${SOURCE_USERNAME}/downloads");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3" });

            Assert.Equal(Path.Combine("alice", "downloads"), result);
        }

        [Fact]
        public async Task Pattern_Backslash_Separator_Preserved_In_Output()
        {
            var (service, _) = GetFixture("${SOURCE_USERNAME}\\downloads");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3" });

            Assert.Equal(Path.Combine("alice", "downloads"), result);
        }

        [Fact]
        public async Task Pattern_Output_Is_Always_Relative()
        {
            var (service, _) = GetFixture("/etc/passwd");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3" });

            Assert.False(FileSafety.IsPathAbsolute(result));
        }

        [Fact]
        public async Task Pattern_Traversal_Segments_Do_Not_Survive()
        {
            var (service, _) = GetFixture("../../../etc/passwd");
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3" });

            Assert.False(FileSafety.ContainsTraversalSegments(result));
        }

        [Fact]
        public async Task Username_Containing_Traversal_Is_Safe()
        {
            // exact '..' in username must not produce a traversal segment in the final output
            var (service, _) = GetFixture("${SOURCE_USERNAME}");
            var result = await service.DeriveDestination(new Transfer { Username = "..", Filename = "@alice\\track.mp3" });

            Assert.False(FileSafety.ContainsTraversalSegments(result));
        }

        [Fact]
        public async Task SEARCH_TEXT_Null_Byte_In_Value_Replaced_With_Underscore()
        {
            var searchId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = searchId };
            var search = new Search { Id = searchId, SearchText = "foo\0bar" };
            var (service, _) = GetFixture("${SEARCH_TEXT}", batch: batch, search: search);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("foo_bar", result);
        }

        [Fact]
        public async Task SEARCH_TEXT_Is_Fallback_When_Search_Not_Found()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = Guid.NewGuid() };
            var (service, _) = GetFixture("${SEARCH_TEXT}", batch: batch, search: null);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("unknown_search_text", result);
        }

        [Fact]
        public async Task SEARCH_TEXT_Is_Fallback_When_Batch_Has_No_SearchId()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = null };
            var (service, _) = GetFixture("${SEARCH_TEXT}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("unknown_search_text", result);
        }

        [Fact]
        public async Task SEARCH_TEXT_Is_Fallback_When_Search_Text_Is_Empty()
        {
            var searchId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = searchId };
            var search = new Search { Id = searchId, SearchText = "" };
            var (service, _) = GetFixture("${SEARCH_TEXT}", batch: batch, search: search);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("unknown_search_text", result);
        }

        [Fact]
        public async Task SEARCH_TEXT_Is_Fallback_When_Search_Text_Is_Whitespace()
        {
            var searchId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = searchId };
            var search = new Search { Id = searchId, SearchText = "   " };
            var (service, _) = GetFixture("${SEARCH_TEXT}", batch: batch, search: search);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("unknown_search_text", result);
        }

        [Fact]
        public async Task SEARCH_TEXT_Traversal_Segments_Do_Not_Survive()
        {
            var searchId = Guid.NewGuid();
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, SearchId = searchId };
            var search = new Search { Id = searchId, SearchText = "../evil" };
            var (service, _) = GetFixture("${SEARCH_TEXT}", batch: batch, search: search);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.False(FileSafety.ContainsTraversalSegments(result));
        }

        [Fact]
        public async Task BATCH_EXTERNAL_ID_Is_Fallback_When_External_Id_Is_Empty_String()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { ExternalId = "" } };
            var (service, _) = GetFixture("${BATCH_EXTERNAL_ID}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.Equal("unknown_batch_external_id", result);
        }

        [Fact]
        public async Task BATCH_EXTERNAL_ID_Traversal_Segments_Do_Not_Survive()
        {
            var batchId = Guid.NewGuid();
            var batch = new Batch { Id = batchId, Options = new BatchOptions { ExternalId = "../evil" } };
            var (service, _) = GetFixture("${BATCH_EXTERNAL_ID}", batch: batch);
            var result = await service.DeriveDestination(new Transfer { Username = "alice", Filename = "@alice\\track.mp3", BatchId = batchId });

            Assert.False(FileSafety.ContainsTraversalSegments(result));
        }
    }

    private static (DownloadService service, Mocks mocks) GetFixture(
        string subdirectory = "${SOURCE_USERNAME}",
        Batch batch = null,
        Search search = null)
    {
        var mocks = new Mocks(subdirectory, batch, search);
        var service = new DownloadService(
            mocks.BatchService.Object,
            mocks.SearchService.Object,
            mocks.OptionsMonitor,
            null,
            new Mock<IDbContextFactory<TransfersDbContext>>().Object,
            null,
            null,
            null,
            null);
        return (service, mocks);
    }

    private class Mocks
    {
        public Mocks(string subdirectory = null, Batch batch = null, Search search = null)
        {
            OptionsMonitor = new TestOptionsMonitor<Options>(new Options
            {
                Transfers = new Options.TransfersOptions
                {
                    Download = new Options.TransfersOptions.GlobalDownloadOptions
                    {
                        Destination = new Options.TransfersOptions.GlobalDownloadOptions.DestinationOptions
                        {
                            Subdirectory = subdirectory
                        }
                    }
                }
            });

            BatchService.Setup(b => b.FindAsync(It.IsAny<Expression<Func<Batch, bool>>>()))
                .ReturnsAsync(batch);
            SearchService.Setup(s => s.FindAsync(It.IsAny<Expression<Func<Search, bool>>>(), It.IsAny<bool>()))
                .ReturnsAsync(search);
        }

        public Mock<IBatchService> BatchService { get; } = new();
        public Mock<ISearchService> SearchService { get; } = new();
        public TestOptionsMonitor<Options> OptionsMonitor { get; }
    }
}
