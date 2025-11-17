using System;

namespace APLabApp.Dal.Entities
{
    public class Grade
    {
        public int FeedbackId { get; set; }
        public Feedback Feedback { get; set; } = null!;

        public int CareerSkills { get; set; }
        public int Communication { get; set; }
        public int Collaboration { get; set; }
    }
}
