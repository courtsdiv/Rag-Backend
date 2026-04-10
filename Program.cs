using RagBackend.Application.Interfaces;
using RagBackend.Application.Services;
using RagBackend.Domain.Models;
using RagBackend.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// MVC + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Application layer
builder.Services.AddScoped<RagService>();

// Infrastructure layer
builder.Services.AddScoped<IQdrantService, QdrantService>();

builder.Services.AddHttpClient<IEmbeddingService, OpenRouterEmbeddingService>(client =>
{
    client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
});

builder.Services.AddHttpClient<IChatCompletionService, OpenRouterChatService>(client =>
{
    client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
});

var app = builder.Build();

// Development tools 
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
// Middleware pipeline
app.UseHttpsRedirection();

// Global exception handling
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        Console.WriteLine("[FATAL] Unhandled exception: " + ex);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsJsonAsync(new ApiError
        {
            Message = "An unexpected server error occurred.",
            ErrorCode = "UNHANDLED_EXCEPTION"
        });
    }
});

app.UseAuthorization();

app.MapControllers();
app.Run();

// Required for integration testing
public partial class Program { }