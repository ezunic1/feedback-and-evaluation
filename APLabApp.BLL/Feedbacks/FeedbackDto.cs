using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APLabApp.BLL.Feedbacks
{
    public sealed record FeedbackDto(
        int Id,
        int SeasonId,
        Guid SenderUserId,
        Guid ReceiverUserId,
        string Comment,
        DateTime CreatedAtUtc,
        GradeDto? Grade
    );
}
