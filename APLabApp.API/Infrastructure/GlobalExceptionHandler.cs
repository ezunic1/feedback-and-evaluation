using APLabApp.BLL.Errors;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics;
using FVValidationException = FluentValidation.ValidationException;

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
            var pd = BuildProblemDetails(http, ex);
            await _pds.WriteAsync(new() { HttpContext = http, ProblemDetails = pd });
            return true;
        }

        private static ProblemDetails BuildProblemDetails(HttpContext http, Exception ex)
        {
            if (ex is FVValidationException fv)
            {
                var errors = fv.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Where(s => !string.IsNullOrWhiteSpace(s)).ToArray());

                var firstMessage = fv.Errors.Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m)) ?? "Validation failed.";

                var pd = new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = firstMessage,
                    Type = $"https://httpstatuses.com/{StatusCodes.Status400BadRequest}"
                };
                pd.Extensions["errors"] = errors;
                pd.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
                return pd;
            }

            var (status, title, detail) = Map(ex);

            var outPd = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = $"https://httpstatuses.com/{status}"
            };
            outPd.Extensions["traceId"] = Activity.Current?.Id ?? http.TraceIdentifier;
            return outPd;
        }

        private static (int status, string title, string detail) Map(Exception ex) => ex switch
        {
            AppValidationException ve => (StatusCodes.Status400BadRequest, "Bad Request", ve.Message),
            NotFoundException nf => (StatusCodes.Status404NotFound, "Not Found", nf.Message),
            ConflictException cf => (StatusCodes.Status409Conflict, "Conflict", cf.Message),
            ForbiddenException fb => (StatusCodes.Status403Forbidden, "Forbidden", fb.Message),

            DbUpdateException db when db.InnerException is PostgresException pg
                                     && pg.SqlState == PostgresErrorCodes.UniqueViolation
                => (StatusCodes.Status409Conflict, "Conflict", "A record with these unique fields already exists."),

            ArgumentException ae => (StatusCodes.Status400BadRequest, "Bad Request", ae.Message),
            InvalidOperationException ioe => (StatusCodes.Status400BadRequest, "Bad Request", ioe.Message),

            _ => (StatusCodes.Status500InternalServerError, "Server Error", "An unexpected error occurred.")
        };
    }
}
