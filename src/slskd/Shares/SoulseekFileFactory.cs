// <copyright file="SoulseekFileFactory.cs" company="slskd Team">
//     Copyright (c) slskd Team. All rights reserved.
//
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU Affero General Public License as published
//     by the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
//
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU Affero General Public License for more details.
//
//     You should have received a copy of the GNU Affero General Public License
//     along with this program.  If not, see https://www.gnu.org/licenses/.
// </copyright>

using System.IO;

namespace slskd.Shares
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Serilog;
    using Soulseek;

    /// <summary>
    ///     Creates instances of <see cref="Soulseek.File"/>.
    /// </summary>
    public interface ISoulseekFileFactory
    {
        /// <summary>
        ///     Creates an instance of <see cref="Soulseek.File"/> from the given path.
        /// </summary>
        /// <param name="filename">The fully qualified path to the file.</param>
        /// <param name="maskedFilename">The masked filename.</param>
        /// <returns>The created instance.</returns>
        File Create(string filename, string maskedFilename);
    }

    /// <summary>
    ///     Creates instances of <see cref="Soulseek.File"/>.
    /// </summary>
    public class SoulseekFileFactory : ISoulseekFileFactory
    {
        private static readonly string[] AudioExtensions = { "aa", "aax", "aac", "aiff", "ape", "dsf", "flac", "m4a", "m4b", "m4p", "mp3", "mpc", "mpp", "ogg", "oga", "wav", "wma", "wv", "webm" };
        private static readonly string[] VideoExtensions = { "mkv", "ogv", "avi", "wmv", "asf", "mp4", "m4p", "m4v", "mpg", "mpe", "mpv", "mpg", "m2v" };
        private static readonly HashSet<string> SupportedExtensions = AudioExtensions.Concat(VideoExtensions).ToHashSet();

        private ILogger Log { get; } = Serilog.Log.ForContext<SharedFileCache>();

        /// <summary>
        ///     Creates an instance of <see cref="Soulseek.File"/> from the given path.
        /// </summary>
        /// <param name="filename">The fully qualified path to the file.</param>
        /// <param name="maskedFilename">The masked filename.</param>
        /// <returns>The created instance.</returns>
        public File Create(string filename, string maskedFilename)
        {
            var code = 1;
            var size = new FileInfo(filename).Length;
            var extension = Path.GetExtension(filename)[1..].ToLowerInvariant();
            List<FileAttribute> attributeList = default;

            if (SupportedExtensions.Contains(extension))
            {
                attributeList = new List<FileAttribute>();
                TagLib.File file = default;

                try
                {
                    file = TagLib.File.Create(filename, TagLib.ReadStyle.Average | TagLib.ReadStyle.PictureLazy);

                    attributeList.Add(new FileAttribute(FileAttributeType.Length, (int)file.Properties.Duration.TotalSeconds));
                    attributeList.Add(new FileAttribute(FileAttributeType.BitRate, file.Properties.AudioBitrate));

                    if (file.Properties.BitsPerSample > 0)
                    {
                        attributeList.Add(new FileAttribute(FileAttributeType.SampleRate, file.Properties.AudioSampleRate));
                        attributeList.Add(new FileAttribute(FileAttributeType.BitDepth, file.Properties.BitsPerSample));
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning("Failed to read metadata from file '{Filename}'; the file is an unsupported format or may be corrupt ({ExceptionType})", filename, ex.GetType().Name);
                }
                finally
                {
                    file?.Dispose();
                }
            }

            return new File(code, maskedFilename, size, extension, attributeList);
        }
    }
}