using Microsoft.Data.Sqlite;
using TodoApi.Services;
using TodoApi.Middleware;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Add in-memory caching
builder.Services.AddMemoryCache();

// Register Todo service (IMemoryCache will be injected by DI)
builder.Services.AddScoped<ITodoService, TodoService>();

// API Versioning
builder.Services.AddApiVersioning(options =>
{
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.DefaultApiVersion = new Microsoft.AspNetCore.Mvc.ApiVersion(1, 0);
    options.ReportApiVersions = true;
});

// Versioned API explorer (for Swagger)
builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";          // e.g. "v1"
    options.SubstituteApiVersionInUrl = true;
});

// Swagger registration - we register a ConfigureOptions to create one document per API version
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddTransient<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerOptions>();

var app = builder.Build();

// Register global exception handling middleware early in the pipeline so it can catch downstream errors.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// NOTE: Database initialization is handled by the service (EnsureDatabaseAndTable).

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    var provider = app.Services.GetRequiredService<IApiVersionDescriptionProvider>();

    app.UseSwaggerUI(c =>
    {
        foreach (var desc in provider.ApiVersionDescriptions)
        {
            var url = $"/swagger/{desc.GroupName}/swagger.json";
#if DEBUG
            url += "?ts=" + DateTime.UtcNow.Ticks; // dev-only cache bust
#endif
            c.SwaggerEndpoint(url, $"Todo API {desc.GroupName}");
        }
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>
/// Creates a swagger document for each discovered API version.
/// </summary>
public class ConfigureSwaggerOptions : IConfigureOptions<SwaggerGenOptions>
{
    private readonly IApiVersionDescriptionProvider _provider;

    public ConfigureSwaggerOptions(IApiVersionDescriptionProvider provider)
    {
        _provider = provider;
    }

    public void Configure(SwaggerGenOptions options)
    {
        foreach (var description in _provider.ApiVersionDescriptions)
        {
            var info = new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "Todo API",
                Version = description.GroupName,
                Description = description.IsDeprecated ? "This API version has been deprecated." : "Todo API"
            };

            options.SwaggerDoc(description.GroupName, info);
        }
    }
}