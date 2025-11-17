using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APLabApp.BLL.Feedbacks
{
    public sealed record GradeDto(
         int CareerSkills,
         int Communication,
         int Collaboration
     );
}
