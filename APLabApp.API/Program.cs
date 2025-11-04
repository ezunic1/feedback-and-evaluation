using System.IdentityModel.Tokens.Jwt;
using APLabApp.BLL.Auth;
using APLabApp.BLL.Users;
using APLabApp.Dal;
using APLabApp.Dal.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var realm = builder.Configuration["Keycloak:Realm"] ?? "ApLabRealm";
var authUrl = (builder.Configuration["Keycloak:AuthServerUrl"] ?? "http://localhost:8080").TrimEnd('/');
var keycloakAuthority = builder.Configuration["Keycloak:Authority"] ?? $"{authUrl}/realms/{realm}";

var validAudiences = new[]
{
    builder.Configuration["Keycloak:Audience"],
    builder.Configuration["Keycloak:ClientId"],
    "aplab-api"
}
.Where(s => !string.IsNullOrWhiteSpace(s))!
.Distinct()
.ToArray();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("APDB")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddHttpClient<IKeycloakAdminService, KeycloakAdminService>();

builder.Services.AddControllers();

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
                var sub = ctx.Principal?.FindFirst("sub")?.Value;
                var roles = string.Join(",", ctx.Principal?.FindAll("roles")?.Select(c => c.Value) ?? Array.Empty<string>());
                Console.WriteLine($"[JWT] Token OK for sub={sub} roles=[{roles}]");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

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
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

IdentityModelEventSource.ShowPII = true;

var app = builder.Build();

app.MapDefaultEndpoints();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
