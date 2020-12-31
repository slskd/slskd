namespace slskd.DTO
{
    public class QueueDownloadRequest
    {
        /// <summary>
        ///     Gets or sets the filename to download.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets or sets the size of the file.
        /// </summary>
        public long? Size { get; set; }

        /// <summary>
        ///     Gets or sets the optional transfer token.
        /// </summary>
        public int? Token { get; set; }
    }
}
