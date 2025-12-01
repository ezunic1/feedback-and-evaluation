using APLabApp.API;
using APLabApp.API.Hubs;
using APLabApp.API.Infrastructure;
using APLabApp.Bll.Services;
using APLabApp.BLL.Auth;
using APLabApp.BLL.DeleteRequests;
using APLabApp.BLL.Feedbacks;
using APLabApp.BLL.Seasons;
using APLabApp.BLL.Users;
using APLabApp.Dal;
using APLabApp.Dal.Repositories;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var realm = builder.Configuration["Keycloak:Realm"] ?? "ApLabRealm";
var authUrl = (builder.Configuration["Keycloak:AuthServerUrl"] ?? "http://localhost:8080").TrimEnd('/');
var keycloakAuthority = builder.Configuration["Keycloak:Authority"] ?? $"{authUrl}/realms/{realm}";
var clientIdForResourceAccess = builder.Configuration["Keycloak:PublicClientId"] ?? builder.Configuration["Keycloak:ClientId"] ?? "aplab-api";

var validAudiences = new[]
{
    builder.Configuration["Keycloak:Audience"],
    builder.Configuration["Keycloak:PublicClientId"],
    builder.Configuration["Keycloak:ClientId"],
    "aplab-api",
    "account"
}
.Where(s => !string.IsNullOrWhiteSpace(s))!
.Distinct()
.ToArray();

builder.Services.AddScoped<RealtimeNotificationsInterceptor>();

builder.Services.AddDbContext<AppDbContext>((sp, o) =>
{
    o.UseNpgsql(builder.Configuration.GetConnectionString("APDB"));
    o.AddInterceptors(sp.GetRequiredService<RealtimeNotificationsInterceptor>());
});

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>();

builder.Services.AddScoped<ISeasonRepository, SeasonRepository>();
builder.Services.AddScoped<ISeasonService, SeasonService>();

builder.Services.AddScoped<IFeedbackRepository, FeedbackRepository>();
builder.Services.AddScoped<IGradeRepository, GradeRepository>();
builder.Services.AddScoped<IFeedbackService, FeedbackService>();

builder.Services.AddScoped<IDeleteRequestRepository, DeleteRequestRepository>();
builder.Services.AddScoped<IDeleteRequestService, DeleteRequestService>();

builder.Services.AddControllers().ConfigureApiBehaviorOptions(o => o.SuppressModelStateInvalidFilter = true);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddFluentValidationAutoValidation(o => o.DisableDataAnnotationsValidation = true);
builder.Services.AddValidatorsFromAssembly(typeof(APLabApp.BLL.Validation.ValidationMarker).Assembly);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "APLab API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Unesi samo JWT (bez 'Bearer '). Swagger dodaje prefiks."
    };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = keycloakAuthority;
        options.MetadataAddress = $"{keycloakAuthority.TrimEnd('/')}/.well-known/openid-configuration";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = keycloakAuthority.TrimEnd('/'),
            ValidateAudience = true,
            ValidAudiences = validAudiences,
            NameClaimType = "preferred_username",
            RoleClaimType = "roles",
            ClockSkew = TimeSpan.FromMinutes(2)
        };
        options.MapInboundClaims = false;
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = ctx =>
            {
                Console.WriteLine($"[JWT] Auth failed: {ctx.Exception.GetType().Name}: {ctx.Exception.Message}");
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                Console.WriteLine($"[JWT] Challenge: {ctx.Error} {ctx.ErrorDescription}");
                return Task.CompletedTask;
            },
            OnTokenValidated = ctx =>
            {
                var identity = ctx.Principal?.Identity as ClaimsIdentity;
                if (identity == null) return Task.CompletedTask;
                if (ctx.SecurityToken is JwtSecurityToken jwt)
                {
                    if (jwt.Payload.TryGetValue("realm_access", out var realmAccessObj))
                        foreach (var r in ExtractRolesFromObject(realmAccessObj, "roles"))
                            identity.AddClaim(new Claim("roles", r));
                    if (jwt.Payload.TryGetValue("resource_access", out var resAccessObj))
                        foreach (var r in ExtractClientRoles(resAccessObj, clientIdForResourceAccess))
                            identity.AddClaim(new Claim("roles", r));
                }
                var sub = ctx.Principal?.FindFirst("sub")?.Value;
                var roles = string.Join(",", identity.Claims.Where(c => c.Type == "roles").Select(c => c.Value));
                Console.WriteLine($"[JWT] Token OK for sub={sub} roles=[{roles}]");
                return Task.CompletedTask;
            },
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/notifications"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? new[] { "http://localhost:4200" };
builder.Services.AddCors(o =>
{
    o.AddPolicy("frontend", p =>
        p.WithOrigins(corsOrigins)
         .AllowAnyHeader()
         .AllowAnyMethod()
         .AllowCredentials());
});

builder.Services.AddSignalR();

IdentityModelEventSource.ShowPII = true;

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotificationsHub>("/hubs/notifications");
app.Run();

static IEnumerable<string> ExtractRolesFromObject(object? obj, string rolesPropertyName)
{
    if (obj is JsonElement je)
    {
        if (je.ValueKind == JsonValueKind.Object &&
            je.TryGetProperty(rolesPropertyName, out var arr) &&
            arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in arr.EnumerateArray())
                if (e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                    yield return e.GetString()!;
        }
        yield break;
    }

    if (obj is IDictionary<string, object> dict && dict.TryGetValue(rolesPropertyName, out var rolesObj))
    {
        if (rolesObj is IEnumerable<object> list)
            foreach (var r in list)
                if (r is string s && !string.IsNullOrWhiteSpace(s))
                    yield return s;
    }
}

static IEnumerable<string> ExtractClientRoles(object? resourceAccessObj, string clientId)
{
    if (resourceAccessObj is JsonElement je && je.ValueKind == JsonValueKind.Object)
    {
        if (je.TryGetProperty(clientId, out var clientElem) &&
            clientElem.ValueKind == JsonValueKind.Object &&
            clientElem.TryGetProperty("roles", out var rolesElem) &&
            rolesElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in rolesElem.EnumerateArray())
                if (e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                    yield return e.GetString()!;
        }
        yield break;
    }

    if (resourceAccessObj is IDictionary<string, object> dict &&
        dict.TryGetValue(clientId, out var clientObj) &&
        clientObj is IDictionary<string, object> inner &&
        inner.TryGetValue("roles", out var rolesObj) &&
        rolesObj is IEnumerable<object> list)
    {
        foreach (var r in list)
            if (r is string s && !string.IsNullOrWhiteSpace(s))
                yield return s;
    }
}
