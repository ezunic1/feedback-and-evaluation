using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// Credentials
var username = builder.AddParameter("username", "postgres", secret: false);
var password = builder.AddParameter("password", "postgres", secret: true);

// PostgreSQL service
var postgres = builder.AddPostgres("postgres", username, password)
    .WithImage("postgres:17.6")
    .WithDataVolume()
    .WithEnvironment("POSTGRES_DB", "APDB")
    .WithEndpoint("tcp", endpoint =>
    {
        endpoint.Port = 55432;        
        endpoint.TargetPort = 5432;
        endpoint.IsProxied = false;
    });

// Add the database instance
var apLabDb = postgres.AddDatabase("APDB");

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume()
    .WithEnvironment("KEYCLOAK_ADMIN", "admin")
    .WithEnvironment("KEYCLOAK_ADMIN_PASSWORD", "admin")
    .WithExternalHttpEndpoints();

// Migration worker project
/*builder.AddProject<Projects.APLabApp_MigrationService>("migration-worker")
    .WithReference(apLabDb)
    .WaitFor(apLabDb);*/

// API project
builder.AddProject<Projects.APLabApp_API>("aplabapp-api")
    .WithReference(apLabDb)
    .WithReference(keycloak)
    .WaitFor(apLabDb);




//builder.AddProject<Projects.MigrationService>("migrationservice");

builder.Build().Run();
