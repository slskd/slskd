namespace slskd.Tests.Unit.Interests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using slskd.Interests;
    using Xunit;

    public class InterestMessagesTests
    {
        [Fact]
        public void Build_Writes_Length_Code_And_Strings()
        {
            var message = InterestMessages.Build(InterestMessages.InterestAdd, "jazz");

            var expected = Payload()
                .Int(InterestMessages.InterestAdd)
                .String("jazz")
                .ToArray();

            Assert.Equal(expected.Length, BitConverter.ToInt32(message, 0));
            Assert.Equal(expected, message[4..]);
        }

        [Fact]
        public void Build_Writes_Code_Alone_Without_Strings()
        {
            var message = InterestMessages.Build(InterestMessages.GetRecommendations);

            Assert.Equal(new byte[] { 4, 0, 0, 0, 54, 0, 0, 0 }, message);
        }

        [Fact]
        public void ReadUserInterests_Reads_Liked_And_Hated()
        {
            var payload = Payload()
                .String("alice")
                .Strings("jazz", "ambient")
                .Strings("noise")
                .ToArray();

            var interests = InterestMessages.ReadUserInterests(payload);

            Assert.Equal("alice", interests.Username);
            Assert.Equal(new[] { "jazz", "ambient" }, interests.Liked);
            Assert.Equal(new[] { "noise" }, interests.Hated);
        }

        [Fact]
        public void ReadRecommendations_Reads_Both_Lists()
        {
            var payload = Payload()
                .Int(2).String("jazz").Int(5).String("soul").Int(3)
                .Int(1).String("noise").Int(-2)
                .ToArray();

            var recommendations = InterestMessages.ReadRecommendations(payload);

            Assert.Null(recommendations.Item);
            Assert.Equal(new[] { ("jazz", 5), ("soul", 3) }, recommendations.Recommended.Select(r => (r.Item, r.Score)));
            Assert.Equal(new[] { ("noise", -2) }, recommendations.Unrecommended.Select(r => (r.Item, r.Score)));
        }

        [Fact]
        public void ReadRecommendations_Tolerates_Missing_Unrecommendations()
        {
            var payload = Payload()
                .String("jazz")
                .Int(1).String("soul").Int(3)
                .ToArray();

            var recommendations = InterestMessages.ReadRecommendations(payload, hasItem: true);

            Assert.Equal("jazz", recommendations.Item);
            Assert.Single(recommendations.Recommended);
            Assert.Empty(recommendations.Unrecommended);
        }

        [Fact]
        public void ReadSimilarUsers_Reads_Usernames_And_Ratings()
        {
            var payload = Payload()
                .Int(2).String("alice").Int(4).String("bob").Int(1)
                .ToArray();

            var users = InterestMessages.ReadSimilarUsers(payload);

            Assert.Equal(new[] { ("alice", 4), ("bob", 1) }, users.Select(u => (u.Username, u.Rating)));
        }

        [Fact]
        public void ReadItemSimilarUsers_Skips_Item_And_Reads_Usernames()
        {
            var payload = Payload()
                .String("jazz")
                .Strings("alice", "bob")
                .ToArray();

            Assert.Equal(new[] { "alice", "bob" }, InterestMessages.ReadItemSimilarUsers(payload));
        }

        [Fact]
        public void ReadKey_Reads_Leading_String()
        {
            var payload = Payload().String("jazz").Strings("alice").ToArray();

            Assert.Equal("jazz", InterestMessages.ReadKey(payload));
        }

        [Fact]
        public void Strings_Fall_Back_To_Latin1_When_Not_UTF8()
        {
            var latin1 = Encoding.Latin1.GetBytes("café");
            var payload = Payload().Int(latin1.Length).Bytes(latin1).ToArray();

            Assert.Equal("café", InterestMessages.ReadKey(payload));
        }

        [Fact]
        public void Throws_When_List_Length_Exceeds_Payload()
        {
            var payload = Payload().String("alice").Int(int.MaxValue).ToArray();

            Assert.Throws<InvalidDataException>(() => InterestMessages.ReadUserInterests(payload));
        }

        [Fact]
        public void Throws_When_Payload_Ends_Early()
        {
            var payload = Payload().Int(10).Bytes(new byte[] { 1, 2 }).ToArray();

            Assert.Throws<InvalidDataException>(() => InterestMessages.ReadKey(payload));
        }

        private static PayloadBuilder Payload() => new PayloadBuilder();

        private class PayloadBuilder
        {
            private List<byte> Data { get; } = new List<byte>();

            public PayloadBuilder Int(int value) => Bytes(BitConverter.GetBytes(value));

            public PayloadBuilder String(string value)
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                return Int(bytes.Length).Bytes(bytes);
            }

            public PayloadBuilder Strings(params string[] values)
            {
                Int(values.Length);

                foreach (var value in values)
                {
                    String(value);
                }

                return this;
            }

            public PayloadBuilder Bytes(byte[] bytes)
            {
                Data.AddRange(bytes);
                return this;
            }

            public byte[] ToArray() => Data.ToArray();
        }
    }
}
