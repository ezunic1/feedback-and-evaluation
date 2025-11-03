
using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var username = builder.AddParameter("username", "postgres", secret: false);
var password = builder.AddParameter("password", "postgres", secret: true);

// Postgres baza
var postgres = builder.AddPostgres("postgres", username, password)
    .WithImage("postgres:17.6")
    .WithDataVolume()
    .WithEnvironment("POSTGRES_DB", "APDB")
    .WithEndpoint("tcp", ep => {
        ep.Port = 55432;
        ep.TargetPort = 5432;
        ep.IsProxied = false;
    });

var apLabDb = postgres.AddDatabase("APDB");

//keycloak
var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume("aplab-keycloak-data")
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithEndpoint("http", e => {
        e.Port = 8080;
        e.TargetPort = 8080;
        e.IsProxied = false;
    });



// migration service
var migrator = builder.AddProject<Projects.APLabApp_MigrationService>("migration-worker")
    .WithReference(apLabDb)
    .WaitFor(apLabDb);
// api
builder.AddProject<Projects.APLabApp_API>("aplabapp-api")
    .WithReference(apLabDb)
    .WithReference(keycloak)
    .WaitFor(migrator);

builder.Build().Run();
