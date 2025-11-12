using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace APLabApp.BLL.Errors
{
    public class ValidationException : Exception { public ValidationException(string m) : base(m) { } }
    public class NotFoundException : Exception { public NotFoundException(string m) : base(m) { } }
    public class ConflictException : Exception { public ConflictException(string m) : base(m) { } }
    public class ForbiddenException : Exception { public ForbiddenException(string m) : base(m) { } }
}
