// Program.cs - entry point for the web API

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RagBackend.Domain.Models;
using RagBackend.Infrastructure;
using RagBackend.Infrastructure.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// Add controller support so our API endpoints (in API folder) work.
builder.Services.AddControllers();

// Swagger gives a small web page to try your API while developing.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register app services that controllers will use.
// AddSingleton = "create one instance and reuse it for the whole app".
builder.Services.AddSingleton<IOpenRouterEmbeddingService, OpenRouterEmbeddingService>();
builder.Services.AddSingleton<IQdrantService, QdrantService>();
builder.Services.AddSingleton<IOpenRouterChatService, OpenRouterChatService>();


// Build the app (assemble all the registered services and middleware).
var app = builder.Build();

// Global exception handler - this MUST run early in the pipeline
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine("[FATAL] Unhandled exception: " + ex);

        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";

        var error = new ApiError
        {
            Message = "An unexpected server error occurred.",
            ErrorCode = "UNHANDLED_EXCEPTION"
        };

        await context.Response.WriteAsJsonAsync(error);
    }
});

// Only show the Swagger test page when we are in Development mode.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Optional: force HTTPS redirects.
app.UseHttpsRedirection();

// UseAuthorization enables support for [Authorize] attributes.
// Not using auth yet, but this is fine to keep for later.
app.UseAuthorization();

// Map controller endpoints (connects routes like [HttpPost("answer")] to URLs).
app.MapControllers();

// Start the web server and keep it running.
app.Run();

public partial class Program { }