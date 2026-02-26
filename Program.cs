// Program.cs - entry point for the web API

// CreateBuilder collects configuration and prepares a place to register services.
var builder = WebApplication.CreateBuilder(args);

// Add controller support so our API endpoints (in Controllers) work.
builder.Services.AddControllers();

// Swagger gives a small web page to try your API while developing.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register app services that controllers will use.
// AddSingleton means "make one instance and share it for the whole app".
builder.Services.AddSingleton<RagBackend.Services.OpenRouterEmbeddingService>();
builder.Services.AddSingleton<RagBackend.Services.QdrantService>();
builder.Services.AddSingleton<RagBackend.Services.OpenRouterChatService>();
 

// Build the app. This prepares everything so the app can start.
var app = builder.Build();

// Only show the Swagger test page when we are in Development mode.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// If you want the app to force HTTPS, you can enable the line below.
 app.UseHttpsRedirection(); 

// UseAuthorization adds support for [Authorize] attributes on controllers. 
// If you do not use authorization in your app, this has no effect. 
app.UseAuthorization();

// Make controller endpoints available. This connects route attributes to HTTP paths. 
app.MapControllers();

// Start the web server and keep it running.
app.Run();
