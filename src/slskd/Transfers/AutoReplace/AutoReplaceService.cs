// <copyright file="AutoReplaceService.cs" company="slskd Team">
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

namespace slskd.Transfers.AutoReplace
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Options;
    using Serilog;
    using slskd.Search;
    using Soulseek;
    using SlskdTransfer = slskd.Transfers.Transfer;

    /// <summary>
    ///     Service for automatically replacing stuck downloads with alternative sources.
    /// </summary>
    public interface IAutoReplaceService
    {
        /// <summary>
        ///     Gets all stuck downloads.
        /// </summary>
        /// <returns>A list of stuck downloads.</returns>
        IEnumerable<SlskdTransfer> GetStuckDownloads();

        /// <summary>
        ///     Finds alternative sources for a download.
        /// </summary>
        /// <param name="request">The request containing download details.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>A list of alternative candidates.</returns>
        Task<List<AlternativeCandidate>> FindAlternativesAsync(FindAlternativeRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Replaces a stuck download with an alternative source.
        /// </summary>
        /// <param name="request">The replacement request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if replacement was successful.</returns>
        Task<bool> ReplaceDownloadAsync(ReplaceDownloadRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        ///     Processes all stuck downloads and attempts auto-replacement.
        /// </summary>
        /// <param name="request">The auto-replace request.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The result of the auto-replace operation.</returns>
        Task<AutoReplaceResult> ProcessStuckDownloadsAsync(AutoReplaceRequest request, CancellationToken cancellationToken = default);
    }

    /// <summary>
    ///     Request for finding an alternative source for a download.
    /// </summary>
    public class FindAlternativeRequest
    {
        /// <summary>
        ///     Gets or sets the username of the original source.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     Gets or sets the filename to find an alternative for.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets or sets the expected file size.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        ///     Gets or sets the maximum size difference percentage for alternatives.
        /// </summary>
        public double Threshold { get; set; } = 5.0;
    }

    /// <summary>
    ///     Request for replacing a stuck download.
    /// </summary>
    public class ReplaceDownloadRequest
    {
        /// <summary>
        ///     Gets or sets the ID of the original download.
        /// </summary>
        public string OriginalId { get; set; }

        /// <summary>
        ///     Gets or sets the username of the original source.
        /// </summary>
        public string OriginalUsername { get; set; }

        /// <summary>
        ///     Gets or sets the username of the new source.
        /// </summary>
        public string NewUsername { get; set; }

        /// <summary>
        ///     Gets or sets the filename from the new source.
        /// </summary>
        public string NewFilename { get; set; }

        /// <summary>
        ///     Gets or sets the size of the new file.
        /// </summary>
        public long NewSize { get; set; }
    }

    /// <summary>
    ///     Request for auto-replacing stuck downloads.
    /// </summary>
    public class AutoReplaceRequest
    {
        /// <summary>
        ///     Gets or sets the maximum size difference percentage for auto-replacement.
        /// </summary>
        public double Threshold { get; set; } = 5.0;
    }

    /// <summary>
    ///     Result of an auto-replace operation.
    /// </summary>
    public class AutoReplaceResult
    {
        /// <summary>
        ///     Gets or sets the number of downloads that were replaced.
        /// </summary>
        public int Replaced { get; set; }

        /// <summary>
        ///     Gets or sets the number of downloads that could not be replaced.
        /// </summary>
        public int Failed { get; set; }

        /// <summary>
        ///     Gets or sets the number of downloads that were skipped.
        /// </summary>
        public int Skipped { get; set; }

        /// <summary>
        ///     Gets or sets details about each replacement.
        /// </summary>
        public List<ReplacementDetail> Details { get; set; } = new List<ReplacementDetail>();
    }

    /// <summary>
    ///     Details about a specific replacement.
    /// </summary>
    public class ReplacementDetail
    {
        /// <summary>
        ///     Gets or sets the original filename.
        /// </summary>
        public string OriginalFilename { get; set; }

        /// <summary>
        ///     Gets or sets the original username.
        /// </summary>
        public string OriginalUsername { get; set; }

        /// <summary>
        ///     Gets or sets the new username.
        /// </summary>
        public string NewUsername { get; set; }

        /// <summary>
        ///     Gets or sets the new filename.
        /// </summary>
        public string NewFilename { get; set; }

        /// <summary>
        ///     Gets or sets the size difference percentage.
        /// </summary>
        public double SizeDiffPercent { get; set; }

        /// <summary>
        ///     Gets or sets whether the replacement was successful.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        ///     Gets or sets the error message if the replacement failed.
        /// </summary>
        public string Error { get; set; }
    }

    /// <summary>
    ///     An alternative candidate for a stuck download.
    /// </summary>
    public class AlternativeCandidate
    {
        /// <summary>
        ///     Gets or sets the username of the alternative source.
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     Gets or sets the filename from the alternative source.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets or sets the file size.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        ///     Gets or sets the size difference percentage from the original.
        /// </summary>
        public double SizeDiffPercent { get; set; }

        /// <summary>
        ///     Gets or sets whether the user has a free upload slot.
        /// </summary>
        public bool HasFreeUploadSlot { get; set; }

        /// <summary>
        ///     Gets or sets the user's queue length.
        /// </summary>
        public int QueueLength { get; set; }

        /// <summary>
        ///     Gets or sets the user's upload speed.
        /// </summary>
        public int UploadSpeed { get; set; }
    }

    /// <summary>
    ///     Implementation of <see cref="IAutoReplaceService"/>.
    /// </summary>
    public class AutoReplaceService : IAutoReplaceService
    {
        private static readonly TransferStates[] StuckStates = new[]
        {
            TransferStates.Completed | TransferStates.TimedOut,
            TransferStates.Completed | TransferStates.Errored,
            TransferStates.Completed | TransferStates.Rejected,
            TransferStates.Completed | TransferStates.Cancelled,
        };

        /// <summary>
        ///     Initializes a new instance of the <see cref="AutoReplaceService"/> class.
        /// </summary>
        /// <param name="transferService">The transfer service.</param>
        /// <param name="searchService">The search service.</param>
        /// <param name="soulseekClient">The Soulseek client.</param>
        /// <param name="optionsMonitor">The options monitor.</param>
        public AutoReplaceService(
            ITransferService transferService,
            ISearchService searchService,
            ISoulseekClient soulseekClient,
            IOptionsMonitor<slskd.Options> optionsMonitor)
        {
            Transfers = transferService;
            Searches = searchService;
            Client = soulseekClient;
            OptionsMonitor = optionsMonitor;
        }

        private ITransferService Transfers { get; }

        private ISearchService Searches { get; }

        private ISoulseekClient Client { get; }

        private IOptionsMonitor<slskd.Options> OptionsMonitor { get; }

        private ILogger Log { get; } = Serilog.Log.ForContext<AutoReplaceService>();

        /// <inheritdoc/>
        public IEnumerable<SlskdTransfer> GetStuckDownloads()
        {
            return Transfers.Downloads.List(t =>
                StuckStates.Any(s => t.State == s));
        }

        /// <inheritdoc/>
        public async Task<List<AlternativeCandidate>> FindAlternativesAsync(
            FindAlternativeRequest request,
            CancellationToken cancellationToken = default)
        {
            var candidates = new List<AlternativeCandidate>();

            // Build search query from filename
            var searchText = CleanTrackTitle(request.Filename);
            if (string.IsNullOrWhiteSpace(searchText))
            {
                Log.Warning("Could not build search text from filename: {Filename}", request.Filename);
                return candidates;
            }

            Log.Information("Searching for alternatives: {SearchText}", searchText);

            var searchId = Guid.NewGuid();
            var searchOptions = new Soulseek.SearchOptions(
                searchTimeout: 15000,
                responseLimit: 100,
                fileLimit: 1000);

            try
            {
                await Searches.StartAsync(
                    searchId,
                    SearchQuery.FromText(searchText),
                    SearchScope.Network,
                    searchOptions);

                // Poll for search completion (up to 30 seconds)
                var maxWait = TimeSpan.FromSeconds(30);
                var pollInterval = TimeSpan.FromMilliseconds(1000);
                var waited = TimeSpan.Zero;
                slskd.Search.Search searchWithResponses = null;

                while (waited < maxWait)
                {
                    await Task.Delay(pollInterval, cancellationToken);
                    waited += pollInterval;

                    searchWithResponses = await Searches.FindAsync(s => s.Id == searchId, includeResponses: true);

                    if (searchWithResponses?.State.HasFlag(SearchStates.Completed) == true)
                    {
                        break;
                    }
                }

                if (searchWithResponses?.Responses == null || !searchWithResponses.Responses.Any())
                {
                    Log.Warning("No search responses found for: {SearchText}", searchText);
                    return candidates;
                }

                // Get expected extension
                var expectedExt = GetExtension(request.Filename)?.ToLowerInvariant();

                foreach (var response in searchWithResponses.Responses)
                {
                    // Skip the original source
                    if (response.Username.Equals(request.Username, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    foreach (var file in response.Files)
                    {
                        // Check extension match
                        var fileExt = GetExtension(file.Filename)?.ToLowerInvariant();
                        if (!string.IsNullOrEmpty(expectedExt) && !string.IsNullOrEmpty(fileExt) && fileExt != expectedExt)
                        {
                            continue;
                        }

                        // Check size difference
                        if (file.Size <= 0)
                        {
                            continue;
                        }

                        var sizeDiff = Math.Abs(file.Size - request.Size) / (double)request.Size * 100;
                        if (sizeDiff > request.Threshold * 2)
                        {
                            continue;
                        }

                        candidates.Add(new AlternativeCandidate
                        {
                            Username = response.Username,
                            Filename = file.Filename,
                            Size = file.Size,
                            SizeDiffPercent = sizeDiff,
                            HasFreeUploadSlot = response.HasFreeUploadSlot,
                            QueueLength = (int)response.QueueLength,
                            UploadSpeed = response.UploadSpeed,
                        });
                    }
                }

                // Sort by: size difference, free slot, queue length, speed
                candidates = candidates
                    .OrderBy(c => c.SizeDiffPercent)
                    .ThenByDescending(c => c.HasFreeUploadSlot)
                    .ThenBy(c => c.QueueLength)
                    .ThenByDescending(c => c.UploadSpeed)
                    .Take(10)
                    .ToList();

                Log.Information("Found {Count} alternative candidates for: {SearchText}", candidates.Count, searchText);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error searching for alternatives: {Message}", ex.Message);
            }

            return candidates;
        }

        /// <inheritdoc/>
        public async Task<bool> ReplaceDownloadAsync(
            ReplaceDownloadRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!Guid.TryParse(request.OriginalId, out var originalGuid))
                {
                    Log.Warning("Invalid original download ID: {Id}", request.OriginalId);
                    return false;
                }

                // Cancel and remove the original download
                Transfers.Downloads.TryCancel(originalGuid);
                Transfers.Downloads.Remove(originalGuid);

                Log.Information("Removed stuck download from {Username}: {Filename}",
                    request.OriginalUsername,
                    CleanTrackTitle(request.NewFilename));

                // Enqueue the new download
                var (enqueued, failed) = await Transfers.Downloads.EnqueueAsync(
                    request.NewUsername,
                    new[] { (request.NewFilename, request.NewSize) },
                    cancellationToken);

                if (enqueued.Count > 0)
                {
                    Log.Information("Enqueued replacement from {Username}: {Filename}",
                        request.NewUsername,
                        CleanTrackTitle(request.NewFilename));
                    return true;
                }
                else
                {
                    Log.Warning("Failed to enqueue replacement from {Username}: {Filename}",
                        request.NewUsername,
                        CleanTrackTitle(request.NewFilename));
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error replacing download: {Message}", ex.Message);
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<AutoReplaceResult> ProcessStuckDownloadsAsync(
            AutoReplaceRequest request,
            CancellationToken cancellationToken = default)
        {
            var result = new AutoReplaceResult();

            var stuckDownloads = GetStuckDownloads().ToList();
            if (stuckDownloads.Count == 0)
            {
                return result;
            }

            Log.Information("Processing {Count} stuck downloads for auto-replacement", stuckDownloads.Count);

            // Track processed to avoid duplicates
            var processedTracks = new HashSet<string>();

            foreach (var download in stuckDownloads)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                var trackKey = download.Filename.ToLowerInvariant();
                if (processedTracks.Contains(trackKey))
                {
                    result.Skipped++;
                    continue;
                }

                processedTracks.Add(trackKey);

                var detail = new ReplacementDetail
                {
                    OriginalFilename = download.Filename,
                    OriginalUsername = download.Username,
                };

                try
                {
                    // Find alternatives
                    var alternatives = await FindAlternativesAsync(
                        new FindAlternativeRequest
                        {
                            Username = download.Username,
                            Filename = download.Filename,
                            Size = download.Size,
                            Threshold = request.Threshold,
                        },
                        cancellationToken);

                    // Find the best candidate within threshold
                    var bestCandidate = alternatives
                        .Where(c => c.SizeDiffPercent <= request.Threshold)
                        .FirstOrDefault();

                    if (bestCandidate == null)
                    {
                        detail.Error = "No suitable alternative found";
                        result.Failed++;
                        result.Details.Add(detail);
                        continue;
                    }

                    detail.NewUsername = bestCandidate.Username;
                    detail.NewFilename = bestCandidate.Filename;
                    detail.SizeDiffPercent = bestCandidate.SizeDiffPercent;

                    // Replace the download
                    var replaced = await ReplaceDownloadAsync(
                        new ReplaceDownloadRequest
                        {
                            OriginalId = download.Id.ToString(),
                            OriginalUsername = download.Username,
                            NewUsername = bestCandidate.Username,
                            NewFilename = bestCandidate.Filename,
                            NewSize = bestCandidate.Size,
                        },
                        cancellationToken);

                    if (replaced)
                    {
                        detail.Success = true;
                        result.Replaced++;
                        Log.Information("Replaced: {Original} -> {New} (diff: {Diff:F1}%)",
                            CleanTrackTitle(download.Filename),
                            CleanTrackTitle(bestCandidate.Filename),
                            bestCandidate.SizeDiffPercent);
                    }
                    else
                    {
                        detail.Error = "Failed to enqueue replacement";
                        result.Failed++;
                    }
                }
                catch (Exception ex)
                {
                    detail.Error = ex.Message;
                    result.Failed++;
                    Log.Error(ex, "Error processing: {Filename}", CleanTrackTitle(download.Filename));
                }

                result.Details.Add(detail);

                // Brief delay between operations
                await Task.Delay(500, cancellationToken);
            }

            Log.Information("Auto-replace complete: {Replaced} replaced, {Failed} failed, {Skipped} skipped",
                result.Replaced,
                result.Failed,
                result.Skipped);

            return result;
        }

        /// <summary>
        ///     Clean a track title for searching.
        /// </summary>
        private static string CleanTrackTitle(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return string.Empty;
            }

            // Handle both Windows and Unix path separators
            var name = filename;
            var lastBackslash = name.LastIndexOf('\\');
            var lastSlash = name.LastIndexOf('/');
            var lastSep = Math.Max(lastBackslash, lastSlash);
            if (lastSep >= 0)
            {
                name = name.Substring(lastSep + 1);
            }

            // Remove extension
            var lastDot = name.LastIndexOf('.');
            if (lastDot > 0)
            {
                name = name.Substring(0, lastDot);
            }

            // Replace underscores with spaces
            name = name.Replace("_", " ");

            // Strip quality/bitrate info
            name = Regex.Replace(name, @"\s*\(?\[?(?:FLAC|MP3|AAC|ALAC|WAV|OGG|WMA)[\s\d]*(?:kbps|kHz|bit)?\]?\)?", string.Empty, RegexOptions.IgnoreCase);
            name = Regex.Replace(name, @"\s*\(?\[?\d+\s*kbps\]?\)?", string.Empty, RegexOptions.IgnoreCase);

            // Strip leading track numbers
            name = Regex.Replace(name, @"^[0-9]{1,4}[\s.\-)_]+", string.Empty);

            // Strip year patterns
            name = Regex.Replace(name, @"\s*[\(\[]?\d{4}[\)\]]?\s*", " ");

            // Collapse whitespace and trim
            name = Regex.Replace(name, @"\s+", " ").Trim();
            name = name.Trim('-', ' ');

            return name;
        }

        /// <summary>
        ///     Get file extension handling both Windows and Unix paths.
        /// </summary>
        private static string GetExtension(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                return null;
            }

            var lastBackslash = filename.LastIndexOf('\\');
            var lastSlash = filename.LastIndexOf('/');
            var lastSep = Math.Max(lastBackslash, lastSlash);
            var lastDot = filename.LastIndexOf('.');

            if (lastDot > lastSep && lastDot < filename.Length - 1)
            {
                return filename.Substring(lastDot + 1);
            }

            return null;
        }
    }
}
