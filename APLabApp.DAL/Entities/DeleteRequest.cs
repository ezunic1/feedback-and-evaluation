using System;

namespace APLabApp.Dal.Entities
{
    public class DeleteRequest
    {
        public int Id { get; set; }

        public int FeedbackId { get; set; }
        public Feedback Feedback { get; set; } = null!;

        public Guid SenderUserId { get; set; }
        public User SenderUser { get; set; } = null!;

        public string Reason { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
