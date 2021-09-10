// <copyright file="GitHub.cs" company="slskd Team">
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

namespace slskd
{
    using System;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Text.Json;
    using System.Threading.Tasks;

    public static class GitHub
    {
        public static async Task<Version> GetLatestReleaseVersion(string organization, string repository, string userAgent)
        {
            var url = $"https://api.github.com/repos/{organization}/{repository}/releases/latest";

            try
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.UserAgent.TryParseAdd(userAgent);

                var response = await http.GetFromJsonAsync<JsonDocument>(url);
                return Version.Parse(response.RootElement.GetProperty("tag_name").GetString());
            }
            catch (Exception ex)
            {
                throw new GitHubException($"Failed to retrieve latest release version from GitHub: {ex.Message}", ex);
            }
        }
    }
}
