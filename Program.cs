using RagBackend.Application.Interfaces;
using RagBackend.Application.Services;
using RagBackend.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

/// <summary>
/// Application entry point.
/// 
/// This file is responsible for:
/// - registering services with dependency injection
/// - configuring middleware
/// - starting the HTTP API
/// </summary>


// ----------------------------
// MVC + API tooling
// ----------------------------

// Enable controller-based APIs
builder.Services.AddControllers();

// Enable API metadata (used by Swagger)
builder.Services.AddEndpointsApiExplorer();

// Enable Swagger for API documentation and testing
builder.Services.AddSwaggerGen();


// ----------------------------
// Application layer services
// ----------------------------

// Core RAG workflow service
builder.Services.AddScoped<RagService>();

// Chat orchestration and guardrails
builder.Services.AddScoped<ChatService>();


// ----------------------------
// Infrastructure layer services
// ----------------------------

// Vector store implementation
builder.Services.AddScoped<IVectorStore, VectorStoreService>();

// Embedding service (calls OpenRouter)
builder.Services.AddHttpClient<IEmbeddingService, LlmEmbeddingService>(client =>
{
    client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
});

// Chat completion service (calls OpenRouter)
builder.Services.AddHttpClient<IChatCompletionService, LlmService>(client =>
{
    client.BaseAddress = new Uri("https://openrouter.ai/api/v1/");
});


// ----------------------------
// Build the application
// ----------------------------

var app = builder.Build();


// ----------------------------
// Development-only tools
// ----------------------------

if (app.Environment.IsDevelopment())
{
    // Enable Swagger UI when running locally
    app.UseSwagger();
    app.UseSwaggerUI();
}


// ----------------------------
// Middleware pipeline
// ----------------------------

// Only enforce HTTPS outside development.
// This avoids issues with local proxies (e.g. Vite).
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Enable authorization middleware
app.UseAuthorization();

// Map controller routes (e.g. /api/Rag/chat)
app.MapControllers();

// Start the application
app.Run();


// Required for integration testing
public partial class Program { }