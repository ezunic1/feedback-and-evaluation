using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APLabApp.BLL.DeleteRequests
{
    public class CreateDeleteRequestDto
    {
        public int FeedbackId { get; set; }
        public Guid SenderUserId { get; set; }
        public string Reason { get; set; } = null!;
    }
}
