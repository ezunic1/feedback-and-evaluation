using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APLabApp.BLL.Feedbacks
{
    public sealed record CreateInternFeedbackRequest(
        Guid ReceiverUserId,
        string Comment
    );
}
