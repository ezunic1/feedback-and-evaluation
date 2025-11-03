using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace APLabApp.Dal.Entities
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid KeycloakId { get; set; }         
        public string FullName { get; set; } = null!;
        public string? Desc { get; set; }
        public string? Email { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
