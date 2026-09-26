// <copyright file="InterestMessages.cs" company="JP Dillingham">
//           ▄▄▄▄     ▄▄▄▄     ▄▄▄▄
//     ▄▄▄▄▄▄█  █▄▄▄▄▄█  █▄▄▄▄▄█  █
//     █__ --█  █__ --█    ◄█  -  █
//     █▄▄▄▄▄█▄▄█▄▄▄▄▄█▄▄█▄▄█▄▄▄▄▄█
//   ┍━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━ ━━━━ ━  ━┉   ┉     ┉
//   │ Copyright (c) JP Dillingham.
//   │
//   │ This program is free software: you can redistribute it and/or modify
//   │ it under the terms of the GNU Affero General Public License as published
//   │ by the Free Software Foundation, version 3.
//   │
//   │ This program is distributed in the hope that it will be useful,
//   │ but WITHOUT ANY WARRANTY; without even the implied warranty of
//   │ MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//   │ GNU Affero General Public License for more details.
//   │
//   │ You should have received a copy of the GNU Affero General Public License
//   │ along with this program.  If not, see https://www.gnu.org/licenses/.
//   │
//   │ This program is distributed with Additional Terms pursuant to Section 7
//   │ of the AGPLv3.  See the LICENSE file in the root directory of this
//   │ project for the complete terms and conditions.
//   │
//   │ https://slskd.org
//   │
//   ├╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌╌ ╌ ╌╌╌╌ ╌
//   │ SPDX-FileCopyrightText: JP Dillingham
//   │ SPDX-License-Identifier: AGPL-3.0-only
//   ╰───────────────────────────────────────────╶──── ─ ─── ─  ── ──┈  ┈
// </copyright>

namespace slskd.Interests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text;

    /// <summary>
    ///     Builds and reads the server messages for interests, recommendations and similar users.
    /// </summary>
    public static class InterestMessages
    {
        /// <summary>Adds a liked interest.</summary>
        public const int InterestAdd = 51;

        /// <summary>Removes a liked interest.</summary>
        public const int InterestRemove = 52;

        /// <summary>Requests recommendations based on the current user's interests.</summary>
        public const int GetRecommendations = 54;

        /// <summary>Requests the most popular interests across the network.</summary>
        public const int GetGlobalRecommendations = 56;

        /// <summary>Requests the interests of a user.</summary>
        public const int GetUserInterests = 57;

        /// <summary>Requests users with interests similar to the current user's.</summary>
        public const int GetSimilarUsers = 110;

        /// <summary>Requests recommendations related to an item.</summary>
        public const int GetItemRecommendations = 111;

        /// <summary>Requests users who like an item.</summary>
        public const int GetItemSimilarUsers = 112;

        /// <summary>Adds a disliked interest.</summary>
        public const int HatedInterestAdd = 117;

        /// <summary>Removes a disliked interest.</summary>
        public const int HatedInterestRemove = 118;

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        /// <summary>
        ///     Builds a message with the specified <paramref name="code"/> and string arguments.
        /// </summary>
        /// <param name="code">The message code.</param>
        /// <param name="strings">The string arguments, in order.</param>
        /// <returns>The message, including the length prefix.</returns>
        public static byte[] Build(int code, params string[] strings)
        {
            using var body = new MemoryStream();
            body.Write(BitConverter.GetBytes(code));

            foreach (var value in strings)
            {
                var bytes = Encoding.UTF8.GetBytes(value);
                body.Write(BitConverter.GetBytes(bytes.Length));
                body.Write(bytes);
            }

            var message = new byte[body.Length + 4];
            BitConverter.GetBytes((int)body.Length).CopyTo(message, 0);
            body.ToArray().CopyTo(message, 4);

            return message;
        }

        /// <summary>
        ///     Reads the leading string of a payload; responses about a user or item start with it.
        /// </summary>
        /// <param name="payload">The message payload.</param>
        /// <returns>The string.</returns>
        public static string ReadKey(byte[] payload) => new Reader(payload).ReadString();

        /// <summary>
        ///     Reads a user interests response.
        /// </summary>
        /// <param name="payload">The message payload.</param>
        /// <returns>The user's interests.</returns>
        public static UserInterests ReadUserInterests(byte[] payload)
        {
            var reader = new Reader(payload);

            return new UserInterests
            {
                Username = reader.ReadString(),
                Liked = reader.ReadStrings(),
                Hated = reader.ReadStrings(),
            };
        }

        /// <summary>
        ///     Reads a recommendations response.
        /// </summary>
        /// <param name="payload">The message payload.</param>
        /// <param name="hasItem">A value indicating whether the payload starts with the item the recommendations are for.</param>
        /// <returns>The recommendations.</returns>
        public static Recommendations ReadRecommendations(byte[] payload, bool hasItem = false)
        {
            var reader = new Reader(payload);
            var item = hasItem ? reader.ReadString() : null;
            var recommended = ReadRecommendationList(reader);

            // older servers end the message after the first list
            var unrecommended = reader.HasMore ? ReadRecommendationList(reader) : Array.Empty<Recommendation>();

            return new Recommendations
            {
                Item = item,
                Recommended = recommended,
                Unrecommended = unrecommended,
            };
        }

        /// <summary>
        ///     Reads a similar users response.
        /// </summary>
        /// <param name="payload">The message payload.</param>
        /// <returns>The similar users.</returns>
        public static IReadOnlyList<SimilarUser> ReadSimilarUsers(byte[] payload)
        {
            var reader = new Reader(payload);
            var count = reader.ReadCount(itemLength: 8);
            var users = new List<SimilarUser>(count);

            for (var i = 0; i < count; i++)
            {
                users.Add(new SimilarUser { Username = reader.ReadString(), Rating = reader.ReadInt32() });
            }

            return users;
        }

        /// <summary>
        ///     Reads an item similar users response.
        /// </summary>
        /// <param name="payload">The message payload.</param>
        /// <returns>The usernames of the users who like the item.</returns>
        public static IReadOnlyList<string> ReadItemSimilarUsers(byte[] payload)
        {
            var reader = new Reader(payload);
            reader.ReadString();

            return reader.ReadStrings();
        }

        private static Recommendation[] ReadRecommendationList(Reader reader)
        {
            var count = reader.ReadCount(itemLength: 8);
            var list = new Recommendation[count];

            for (var i = 0; i < count; i++)
            {
                list[i] = new Recommendation { Item = reader.ReadString(), Score = reader.ReadInt32() };
            }

            return list;
        }

        private sealed class Reader
        {
            public Reader(byte[] payload)
            {
                Payload = payload;
            }

            public bool HasMore => Position < Payload.Length;
            private byte[] Payload { get; }
            private int Position { get; set; }

            public int ReadInt32()
            {
                Require(4);
                var value = BitConverter.ToInt32(Payload, Position);
                Position += 4;
                return value;
            }

            /// <summary>
            ///     Reads the length of a list, checking it against the bytes that remain so a bad value can't cause a
            ///     huge allocation.
            /// </summary>
            public int ReadCount(int itemLength)
            {
                var count = ReadInt32();

                if (count < 0 || count > (Payload.Length - Position) / itemLength)
                {
                    throw new InvalidDataException("The server message contains an invalid list length");
                }

                return count;
            }

            public string ReadString()
            {
                var length = ReadInt32();
                Require(length);

                string value;

                // the library decodes the same way: UTF-8, falling back to ISO-8859-1 for older clients
                try
                {
                    value = StrictUtf8.GetString(Payload, Position, length);
                }
                catch (DecoderFallbackException)
                {
                    value = Encoding.Latin1.GetString(Payload, Position, length);
                }

                Position += length;
                return value;
            }

            public string[] ReadStrings()
            {
                var count = ReadCount(itemLength: 4);
                var list = new string[count];

                for (var i = 0; i < count; i++)
                {
                    list[i] = ReadString();
                }

                return list;
            }

            private void Require(int length)
            {
                if (length < 0 || Position + length > Payload.Length)
                {
                    throw new InvalidDataException("The server message ended unexpectedly");
                }
            }
        }
    }
}
