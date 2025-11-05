using System;
using System.Collections.Generic;

namespace APLabApp.Dal.Entities
{
    public class Season
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public Guid? MentorId { get; set; }
        public User? Mentor { get; set; }
        public ICollection<User> Users { get; } = new List<User>();
    }
}
