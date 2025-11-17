using System;
using System.Diagnostics;

namespace APLabApp.Dal.Entities
{
    public class Feedback
    {
        public int Id { get; set; }

        public int SeasonId { get; set; }
        public Season Season { get; set; } = null!;

        public Guid SenderUserId { get; set; }
        public User SenderUser { get; set; } = null!;

        public Guid ReceiverUserId { get; set; }
        public User ReceiverUser { get; set; } = null!;

        public string Comment { get; set; } = null!;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        public Grade? Grade { get; set; }
    }
}
