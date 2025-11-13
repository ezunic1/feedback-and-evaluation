using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using APLabApp.BLL.Errors;

namespace APLabApp.API.Infrastructure
{
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly IProblemDetailsService _pds;
        private readonly ILogger<GlobalExceptionHandler> _log;

        public GlobalExceptionHandler(IProblemDetailsService pds, ILogger<GlobalExceptionHandler> log)
        {
            _pds = pds;
            _log = log;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
        {
            var (status, title, detail) = Map(ex);

            var pd = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.com/{status}"
            };
            pd.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
            await _pds.WriteAsync(new() { HttpContext = http, ProblemDetails = pd });

            return true;
        }

        private static (int status, string title, string detail) Map(Exception ex) => ex switch
        {
            ValidationException ve => (400, "Validation error", ve.Message),
            NotFoundException nf => (404, "Not found", nf.Message),
            ConflictException cf => (409, "Conflict", cf.Message),
            ForbiddenException fb => (403, "Forbidden", fb.Message),

            DbUpdateException db when db.InnerException is PostgresException pg
                                     && pg.SqlState == PostgresErrorCodes.UniqueViolation
                => (409, "Conflict", "A record with these unique fields already exists."),

            ArgumentException ae => (400, "Validation error", ae.Message),
            InvalidOperationException ioe => (400, "Invalid operation", ioe.Message),

            _ => (500, "Server error", "An unexpected error occurred.")
        };
    }
}
