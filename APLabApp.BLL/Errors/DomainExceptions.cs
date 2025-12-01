using System;

namespace APLabApp.BLL.Errors
{
    public class AppValidationException : Exception { public AppValidationException(string m) : base(m) { } }
    public class NotFoundException : Exception { public NotFoundException(string m) : base(m) { } }
    public class ConflictException : Exception { public ConflictException(string m) : base(m) { } }
    public class ForbiddenException : Exception { public ForbiddenException(string m) : base(m) { } }
    public class UnauthorizedException : Exception { public UnauthorizedException(string m) : base(m) { } }
    public class BusinessRuleViolationException : Exception
    {
        public string Code { get; }
        public BusinessRuleViolationException(string code, string message) : base(message) { Code = code; }
    }
}
