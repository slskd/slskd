namespace slskd.DTO
{
    using Soulseek;
    using System;
    using System.Net;

    /// <summary>
    ///     A single file transfer.
    /// </summary>
    public class Transfer
    {
        /// <summary>
        ///     Gets the current average transfer speed.
        /// </summary>
        public double AverageSpeed { get; set; }

        /// <summary>
        ///     Gets the number of remaining bytes to be transferred.
        /// </summary>
        public long BytesRemaining { get; set; }

        /// <summary>
        ///     Gets the total number of bytes transferred.
        /// </summary>
        public long BytesTransferred { get; set; }

        /// <summary>
        ///     Gets the transfer direction.
        /// </summary>
        public TransferDirection Direction { get; set; }

        /// <summary>
        ///     Gets the current duration of the transfer, if it has been started.
        /// </summary>
        public double? ElapsedTime { get; set; }

        /// <summary>
        ///     Gets the UTC time at which the transfer transitioned into the <see cref="TransferStates.Completed"/> state.
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        ///     Gets the filename of the file to be transferred.
        /// </summary>
        public string Filename { get; set; }

        /// <summary>
        ///     Gets the transfer id.
        /// </summary>
        public string Id => Filename.Sha1();

        /// <summary>
        ///     Gets the ip endpoint of the remote transfer connection, if one has been established.
        /// </summary>
        public IPEndPoint IPEndPoint { get; set; }

        /// <summary>
        ///     Gets the current progress in percent.
        /// </summary>
        public double PercentComplete { get; set; }

        /// <summary>
        ///     Gets the current place in queue, if it has been fetched.
        /// </summary>
        public int? PlaceInQueue { get; set; }

        /// <summary>
        ///     Gets the projected remaining duration of the transfer.
        /// </summary>
        public double? RemainingTime { get; set; }

        /// <summary>
        ///     Gets the remote unique token for the transfer.
        /// </summary>
        public int? RemoteToken { get; set; }

        /// <summary>
        ///     Gets the size of the file to be transferred, in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        ///     Gets the starting offset of the transfer, in bytes.
        /// </summary>
        public long StartOffset { get; set; }

        /// <summary>
        ///     Gets the UTC time at which the transfer transitioned into the <see cref="TransferStates.InProgress"/> state.
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        ///     Gets the state of the transfer.
        /// </summary>
        public TransferStates State { get; set; }

        /// <summary>
        ///     Gets the unique token for the transfer.
        /// </summary>
        public int Token { get; set; }

        /// <summary>
        ///     Gets the username of the peer to or from which the file is to be transferred.
        /// </summary>
        public string Username { get; set; }

        public static Transfer FromSoulseekTransfer(Soulseek.Transfer transfer)
        {
            return new Transfer()
            {
                AverageSpeed = transfer.AverageSpeed,
                BytesRemaining = transfer.BytesRemaining,
                BytesTransferred = transfer.BytesTransferred,
                Direction = transfer.Direction,
                ElapsedTime = transfer.ElapsedTime?.TotalMilliseconds,
                EndTime = transfer.EndTime,
                Filename = transfer.Filename,
                IPEndPoint = transfer.IPEndPoint,
                PercentComplete = transfer.PercentComplete,
                RemainingTime = transfer.RemainingTime?.TotalMilliseconds,
                RemoteToken = transfer.RemoteToken,
                Size = transfer.Size,
                StartOffset = transfer.StartOffset,
                StartTime = transfer.StartTime,
                State = transfer.State,
                Token = transfer.Token,
                Username = transfer.Username
            };
        }
    }
}