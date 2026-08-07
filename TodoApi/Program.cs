using Microsoft.Data.Sqlite;
using TodoApi.Services;
using TodoApi.Middleware;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add in-memory caching
builder.Services.AddMemoryCache();

// Register Todo service (IMemoryCache will be injected by DI)
builder.Services.AddScoped<ITodoService, TodoService>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Register global exception handling middleware early in the pipeline so it can catch downstream errors.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// NOTE: Database initialization is handled by the service (EnsureDatabaseAndTable).
// Removing the duplicate initialization here avoids schema mismatches.

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();