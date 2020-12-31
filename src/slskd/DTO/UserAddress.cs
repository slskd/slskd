namespace slskd.DTO
{
    /// <summary>
    ///     A user's IP address and port.
    /// </summary>
    public class UserAddress
    {
        /// <summary>
        ///     Gets or sets the IP address.
        /// </summary>
        public string IPAddress { get; set; }

        /// <summary>
        ///     Gets or sets the port.
        /// </summary>
        public int Port { get; set; }
    }
}
