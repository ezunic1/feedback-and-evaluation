using APLabApp.Dal;
using Microsoft.EntityFrameworkCore;
using System;

var builder = Host.CreateApplicationBuilder(args);

// Enables Aspire defaults (logging, configuration, etc.)
builder.AddServiceDefaults();

// Register DbContext with Aspire-provided connection string
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("APDB")));

// Register the background migration worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();

host.Run();

