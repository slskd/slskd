namespace slskd.Transfers
{
    public static class Extensions
    {
        public static Transfer WithSoulseekTransfer(this Transfer transfer, Soulseek.Transfer t)
        {
            return new Transfer()
            {
                Id = transfer.Id,
                Username = transfer.Username,
                Direction = transfer.Direction,
                Filename = transfer.Filename,
                Size = transfer.Size,
                StartOffset = transfer.StartOffset,
                State = t.State,
                RequestedAt = transfer.RequestedAt,
                EnqueuedAt = transfer.EnqueuedAt,
                StartedAt = t.StartTime,
                EndedAt = t.EndTime,
                BytesTransferred = t.BytesTransferred,
                AverageSpeed = t.AverageSpeed,
                Exception = t.Exception,
            };
        }
    }
}
