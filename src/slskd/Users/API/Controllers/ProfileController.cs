// <copyright file="ProfileController.cs" company="JP Dillingham">
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

using Microsoft.Extensions.Options;

namespace slskd.Users.API
{
    using System;
    using System.IO;
    using System.Linq;
    using Asp.Versioning;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;
    using Serilog;
    using IOFile = System.IO.File;

    /// <summary>
    ///     The profile (description and picture) presented to other users on the Soulseek network.
    /// </summary>
    [Route("api/v{version:apiVersion}/[controller]")]
    [ApiVersion("0")]
    [ApiController]
    [Produces("application/json")]
    [Consumes("application/json")]
    public class ProfileController : ControllerBase
    {
        /// <summary>
        ///     The maximum size of an uploaded profile picture, in bytes.
        /// </summary>
        public const int MaxPictureBytes = 5 * 1024 * 1024;

        private const string PictureFilePrefix = "picture-";

        /// <summary>
        ///     Initializes a new instance of the <see cref="ProfileController"/> class.
        /// </summary>
        /// <param name="optionsSnapshot"></param>
        public ProfileController(IOptionsSnapshot<Options> optionsSnapshot)
        {
            OptionsSnapshot = optionsSnapshot;
        }

        private static string PictureDirectory => Path.Combine(Program.AppDirectory, "profile");
        private IOptionsSnapshot<Options> OptionsSnapshot { get; }
        private ILogger Log { get; } = Serilog.Log.ForContext<ProfileController>();

        /// <summary>
        ///     Retrieves the currently configured profile picture.
        /// </summary>
        /// <returns></returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="404">No picture is configured, or the configured picture can not be read.</response>
        [HttpGet("picture")]
        [Authorize(Policy = AuthPolicy.Any)]
        [Produces("image/jpeg", "image/png", "image/gif", "image/bmp", "image/webp", "application/octet-stream")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        public IActionResult GetPicture()
        {
            var picture = OptionsSnapshot.Value.Soulseek.Picture;

            if (string.IsNullOrWhiteSpace(picture))
            {
                return NotFound();
            }

            try
            {
                var bytes = IOFile.ReadAllBytes(picture);
                return File(bytes, DetectImageType(bytes)?.ContentType ?? "application/octet-stream");
            }
            catch (Exception ex)
            {
                Log.Warning("Failed to read profile picture {Picture}: {Message}", picture, ex.Message);
                return NotFound();
            }
        }

        /// <summary>
        ///     Saves a new profile picture to the application directory.
        /// </summary>
        /// <remarks>
        ///     This does not change the configuration; set soulseek.picture to the returned path to begin using the picture.
        /// </remarks>
        /// <param name="request">The picture to save.</param>
        /// <returns>The absolute path of the saved picture.</returns>
        /// <response code="200">The request completed successfully.</response>
        /// <response code="400">The picture is missing, too large, or not a supported image type.</response>
        /// <response code="403">Remote configuration is disabled.</response>
        [HttpPut("picture")]
        [Authorize(Policy = AuthPolicy.JwtOnly, Roles = AuthRole.AdministratorOnly)]
        [ProducesResponseType(typeof(string), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(403)]
        public IActionResult PutPicture([FromBody] ProfilePictureRequest request)
        {
            if (!OptionsSnapshot.Value.RemoteConfiguration)
            {
                return Forbid();
            }

            if (string.IsNullOrWhiteSpace(request?.Data))
            {
                return BadRequest("No picture data was provided");
            }

            byte[] bytes;

            try
            {
                bytes = Convert.FromBase64String(request.Data);
            }
            catch (FormatException)
            {
                return BadRequest("Picture data is not valid base64");
            }

            if (bytes.Length > MaxPictureBytes)
            {
                return BadRequest($"Picture is too large; the maximum size is {MaxPictureBytes / 1024 / 1024} MiB");
            }

            var type = DetectImageType(bytes);

            if (type is null)
            {
                return BadRequest("Picture must be a JPEG, PNG, GIF, BMP or WebP image");
            }

            try
            {
                System.IO.Directory.CreateDirectory(PictureDirectory);

                var path = Path.GetFullPath(Path.Combine(PictureDirectory, $"{PictureFilePrefix}{DateTime.UtcNow:yyyyMMddHHmmssfff}{type.Value.Extension}"));
                IOFile.WriteAllBytes(path, bytes);

                DeleteUnusedPictures(keep: path);

                Log.Information("Saved new profile picture to {Path}", path);
                return Ok(path);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save profile picture: {Message}", ex.Message);
                return StatusCode(500, $"Failed to save profile picture: {ex.Message}");
            }
        }

        private static (string ContentType, string Extension)? DetectImageType(byte[] bytes)
        {
            bool StartsWith(params byte[] signature) => bytes.Length >= signature.Length && bytes.Take(signature.Length).SequenceEqual(signature);

            if (StartsWith(0xFF, 0xD8, 0xFF))
            {
                return ("image/jpeg", ".jpg");
            }

            if (StartsWith(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A))
            {
                return ("image/png", ".png");
            }

            if (StartsWith(0x47, 0x49, 0x46, 0x38))
            {
                return ("image/gif", ".gif");
            }

            if (StartsWith(0x42, 0x4D))
            {
                return ("image/bmp", ".bmp");
            }

            // RIFF....WEBP
            if (StartsWith(0x52, 0x49, 0x46, 0x46) && bytes.Length >= 12 && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            {
                return ("image/webp", ".webp");
            }

            return null;
        }

        /// <summary>
        ///     Removes previously uploaded pictures, keeping the new picture and the one currently in use; the configuration
        ///     points at the current picture until the caller updates it, and deleting it would leave the configuration invalid.
        /// </summary>
        private void DeleteUnusedPictures(string keep)
        {
            var current = OptionsSnapshot.Value.Soulseek.Picture;
            var currentFullPath = string.IsNullOrWhiteSpace(current) ? null : Path.GetFullPath(current);
            var keepFullPath = Path.GetFullPath(keep);

            foreach (var file in System.IO.Directory.GetFiles(PictureDirectory, $"{PictureFilePrefix}*"))
            {
                var fullPath = Path.GetFullPath(file);

                if (string.Equals(fullPath, keepFullPath, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fullPath, currentFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    IOFile.Delete(fullPath);
                }
                catch (Exception ex)
                {
                    Log.Debug("Failed to delete unused profile picture {Path}: {Message}", fullPath, ex.Message);
                }
            }
        }
    }
}
