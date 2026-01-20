// ============================================================================
// AG-UI Backend Server - Microsoft Agent Framework
// Single-file implementation with all features
// ============================================================================
// 
// SETUP:
//   1. Create project: dotnet new web -n AgentServer
//   2. Replace Program.cs with this file
//   3. Add packages:
//      dotnet add package Microsoft.Agents.AI.Hosting.AGUI.AspNetCore --prerelease
//      dotnet add package Azure.AI.OpenAI --prerelease
//      dotnet add package Azure.Identity
//      dotnet add package Microsoft.Extensions.AI.OpenAI --prerelease
//   4. Set environment variables and run: dotnet run --urls http://localhost:5000
//
// ENVIRONMENT VARIABLES:
//   AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
//   AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o
//   AZURE_OPENAI_API_KEY=your-api-key (optional, uses DefaultAzureCredential if not set)
//
// ============================================================================

using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Hosting.AGUI.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;
using System.ClientModel;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

// ============================================================================
// BUILDER CONFIGURATION
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddHttpClient();
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
});

// Configure JSON serialization with our custom types
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.TypeInfoResolverChain.Add(AgentJsonContext.Default);
});

// Add CORS for frontend
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("Content-Type", "Cache-Control", "Connection");
    });
});

// Add AG-UI services
builder.Services.AddAGUI();

var app = builder.Build();

// ============================================================================
// CONFIGURATION
// ============================================================================

//string endpoint = builder.Configuration["AZURE_OPENAI_ENDPOINT"]
//    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
//    ?? throw new InvalidOperationException(
//        "AZURE_OPENAI_ENDPOINT is required. Set it in appsettings.json or as environment variable.");

//string deploymentName = builder.Configuration["AZURE_OPENAI_DEPLOYMENT_NAME"]
//    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
//    ?? "gpt-4o";

//string? apiKey = builder.Configuration["AZURE_OPENAI_API_KEY"]
//    ?? Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY");

// ============================================================================
// Get JSON serializer options
// ============================================================================

var jsonOptions = app.Services.GetRequiredService<IOptions<JsonOptions>>().Value;

// ============================================================================
// TOOL IMPLEMENTATIONS (Local Functions - must be before usage)
// ============================================================================

// Get current weather
WeatherResponse GetWeather(WeatherRequest request)
{
    // Simulated weather data - in production, call a real weather API
    var random = new Random(request.City.GetHashCode());
    var conditions = new[] { "Sunny", "Partly Cloudy", "Cloudy", "Rainy", "Stormy", "Snowy", "Foggy" };
    var condition = conditions[random.Next(conditions.Length)];

    double tempCelsius = random.Next(-10, 35);
    double temperature = request.Unit.ToLower() == "fahrenheit"
        ? tempCelsius * 9 / 5 + 32
        : tempCelsius;

    return new WeatherResponse(
        City: request.City,
        Condition: condition,
        Temperature: Math.Round(temperature, 1),
        Unit: request.Unit.ToLower() == "fahrenheit" ? "°F" : "°C",
        Humidity: random.Next(30, 90),
        Wind: $"{random.Next(5, 30)} km/h"
    );
}

// Send email (requires approval)
SendEmailResponse SendEmail(SendEmailRequest request)
{
    // Simulated email sending - in production, use a real email service
    Console.WriteLine($"[EMAIL] Sending to: {request.To}");
    Console.WriteLine($"[EMAIL] Subject: {request.Subject}");
    Console.WriteLine($"[EMAIL] Body: {request.Body}");

    return new SendEmailResponse(
        Success: true,
        MessageId: $"MSG-{Guid.NewGuid():N}"[..16].ToUpper(),
        Status: "sent",
        SentAt: DateTime.UtcNow
    );
}

// Search knowledge base
SearchResponse SearchKnowledgeBase(SearchRequest request)
{
    // Simulated search - in production, connect to a real knowledge base
    var results = new List<SearchResult>();

    // Simulated knowledge base entries
    var knowledgeBase = new Dictionary<string, List<(string Title, string Snippet, string Url)>>
    {
        ["ai"] = [
            ("Introduction to AI", "Artificial Intelligence is the simulation of human intelligence...", "https://docs.example.com/ai/intro"),
            ("Machine Learning Basics", "ML is a subset of AI that enables systems to learn...", "https://docs.example.com/ai/ml"),
            ("Deep Learning Overview", "Deep learning uses neural networks with multiple layers...", "https://docs.example.com/ai/deep-learning")
        ],
        ["azure"] = [
            ("Azure OpenAI Service", "Access powerful language models through Azure...", "https://docs.example.com/azure/openai"),
            ("Azure Cognitive Services", "AI services for vision, speech, language...", "https://docs.example.com/azure/cognitive")
        ],
        ["dotnet"] = [
            (".NET 8 Features", "New features in .NET 8 include...", "https://docs.example.com/dotnet/8"),
            ("ASP.NET Core Basics", "Build web applications with ASP.NET Core...", "https://docs.example.com/dotnet/aspnet")
        ]
    };

    var queryLower = request.Query.ToLower();
    foreach (var (key, entries) in knowledgeBase)
    {
        if (queryLower.Contains(key))
        {
            foreach (var entry in entries.Take(request.MaxResults))
            {
                results.Add(new SearchResult(entry.Title, entry.Snippet, entry.Url, 0.95 - results.Count * 0.1));
            }
        }
    }

    // Default results if no specific match
    if (results.Count == 0)
    {
        results.Add(new SearchResult(
            $"Search results for: {request.Query}",
            $"Found information related to '{request.Query}' in the knowledge base.",
            "https://docs.example.com/search?q=" + Uri.EscapeDataString(request.Query),
            0.75
        ));
    }

    return new SearchResponse(
        Query: request.Query,
        TotalResults: results.Count,
        Results: results.Take(request.MaxResults).ToArray()
    );
}

// Calculator
CalculateResponse Calculate(CalculateRequest request)
{
    try
    {
        // Basic expression evaluation using DataTable.Compute
        // In production, use a proper math parser for security
        var expression = request.Expression
            .Replace("x", "*")
            .Replace("×", "*")
            .Replace("÷", "/")
            .Replace("^", "**");

        // Simple evaluation for basic math
        var dataTable = new System.Data.DataTable();
        var result = dataTable.Compute(expression, null);

        return new CalculateResponse(
            Expression: request.Expression,
            Result: result?.ToString() ?? "undefined",
            Success: true
        );
    }
    catch (Exception ex)
    {
        return new CalculateResponse(
            Expression: request.Expression,
            Result: "Error",
            Success: false,
            Error: ex.Message
        );
    }
}

// Create task
TaskResponse CreateTask(CreateTaskRequest request)
{
    // Simulated task creation - in production, save to database
    var taskId = $"TASK-{Guid.NewGuid():N}"[..12].ToUpper();

    DateTime? dueDate = null;
    if (!string.IsNullOrEmpty(request.DueDate))
    {
        DateTime.TryParse(request.DueDate, out var parsed);
        dueDate = parsed;
    }

    Console.WriteLine($"[TASK] Created: {taskId} - {request.Title}");

    return new TaskResponse(
        Id: taskId,
        Title: request.Title,
        Description: request.Description,
        Priority: request.Priority,
        Status: "pending",
        CreatedAt: DateTime.UtcNow,
        DueDate: dueDate
    );
}

// ============================================================================
// CREATE AI AGENT WITH TOOLS
// ============================================================================

// Create tools array
AITool[] tools =
[
    AIFunctionFactory.Create(GetWeather, name: "GetWeather", description: "Get the current weather conditions for a city", serializerOptions: jsonOptions.SerializerOptions),
    //AIFunctionFactory.Create(SendEmail, name: "SendEmail", description: "Send an email to a recipient. This action requires user approval.", serializerOptions: jsonOptions.SerializerOptions),
    AIFunctionFactory.Create(SearchKnowledgeBase, name: "SearchKnowledgeBase", description: "Search the knowledge base for information", serializerOptions: jsonOptions.SerializerOptions),
    AIFunctionFactory.Create(Calculate, name: "Calculate", description: "Perform mathematical calculations", serializerOptions: jsonOptions.SerializerOptions),
    AIFunctionFactory.Create(CreateTask, name: "CreateTask", description: "Create a new task in the task management system", serializerOptions: jsonOptions.SerializerOptions),
];


IChatClient chatClient = new OpenAIClient(
    new ApiKeyCredential("myapikey"),
    new() { Endpoint = new Uri("myendpoint") })
    .GetChatClient("gpt-5-mini").AsIChatClient();

// Create the AI Agent
string agentInstructions = """
    Sei un assistente AI intelligente e disponibile, creato con Microsoft Agent Framework.
    
    Puoi aiutare gli utenti con:
    - 🌤️ Informazioni meteo (GetWeather)
    - 📧 Invio email (SendEmail) - richiede approvazione dell'utente
    - 🔍 Ricerche nella knowledge base (SearchKnowledgeBase)
    - 🧮 Calcoli matematici (Calculate)
    - ✅ Gestione attività (CreateTask)
    
    Linee guida:
    - Rispondi sempre in italiano, in modo cordiale e professionale
    - Quando usi uno strumento, spiega brevemente cosa stai facendo
    - Per le email, chiedi conferma dei dettagli prima di procedere
    - Fornisci risposte concise ma complete
    - Se non sei sicuro di qualcosa, chiedi chiarimenti
    
    Ricorda: l'invio di email richiede l'approvazione esplicita dell'utente tramite
    il sistema Human-in-the-Loop integrato nel protocollo AG-UI.
    """;

var agent = chatClient.CreateAIAgent(
    name: "AGUIAssistant",
    instructions: agentInstructions,
    tools: tools
);

// ============================================================================
// MIDDLEWARE & ENDPOINTS
// ============================================================================

// Enable CORS
app.UseCors();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new HealthResponse(
    Status: "healthy",
    Timestamp: DateTime.UtcNow,
    Version: "1.0.0"
)));

// Info endpoint
app.MapGet("/info", () => Results.Ok(new InfoResponse(
    Name: "AG-UI Agent Server",
    Version: "1.0.0",
    Framework: "Microsoft Agent Framework",
    Protocol: "AG-UI",
    Features: [
        "Agentic Chat",
        "Backend Tool Rendering",
        "Human in the Loop",
        "Shared State",
        "Predictive State Updates",
        "Server-Sent Events (SSE)"
    ],
    AvailableTools: [
        "GetWeather - Get weather conditions for a city",
        "SendEmail - Send emails (requires approval)",
        "SearchKnowledgeBase - Search information",
        "Calculate - Mathematical calculations",
        "CreateTask - Create tasks"
    ]
)));

// AG-UI endpoint (main agent endpoint)
app.MapAGUI("/api/agent", agent);

// Also map to root for compatibility
app.MapAGUI("/", agent);

// ============================================================================
// STARTUP
// ============================================================================


//Console.WriteLine($"[CONFIG] Azure OpenAI Endpoint: {endpoint}");
//Console.WriteLine($"[CONFIG] Deployment: {deploymentName}");
//Console.WriteLine($"[CONFIG] Authentication: {(string.IsNullOrEmpty(apiKey) ? "DefaultAzureCredential" : "API Key")}");
Console.WriteLine();

await app.RunAsync();

// ============================================================================
// DATA MODELS FOR TOOLS (Type declarations MUST come AFTER top-level statements)
// ============================================================================

// Weather
internal sealed record WeatherRequest(
    [property: Description("The city name")] string City,
    [property: Description("Temperature unit: celsius or fahrenheit")] string Unit = "celsius"
);

internal sealed record WeatherResponse(
    string City,
    string Condition,
    double Temperature,
    string Unit,
    int Humidity,
    string Wind
);

// Email
internal sealed record SendEmailRequest(
    [property: Description("Recipient email address")] string To,
    [property: Description("Email subject line")] string Subject,
    [property: Description("Email body content")] string Body,
    [property: Description("Optional CC recipients")] string[]? Cc = null
);

internal sealed record SendEmailResponse(
    bool Success,
    string MessageId,
    string Status,
    DateTime SentAt
);

// Search
internal sealed record SearchRequest(
    [property: Description("Search query text")] string Query,
    [property: Description("Maximum number of results")] int MaxResults = 5
);

internal sealed record SearchResult(
    string Title,
    string Snippet,
    string Url,
    double Relevance
);

internal sealed record SearchResponse(
    string Query,
    int TotalResults,
    SearchResult[] Results
);

// Calculator
internal sealed record CalculateRequest(
    [property: Description("Mathematical expression to evaluate")] string Expression
);

internal sealed record CalculateResponse(
    string Expression,
    string Result,
    bool Success,
    string? Error = null
);

// Task management
internal sealed record CreateTaskRequest(
    [property: Description("Task title")] string Title,
    [property: Description("Task description")] string? Description = null,
    [property: Description("Due date in ISO format")] string? DueDate = null,
    [property: Description("Priority: low, medium, high")] string Priority = "medium"
);

internal sealed record TaskResponse(
    string Id,
    string Title,
    string? Description,
    string Priority,
    string Status,
    DateTime CreatedAt,
    DateTime? DueDate
);

// API Response models
internal sealed record HealthResponse(string Status, DateTime Timestamp, string Version);
internal sealed record InfoResponse(
    string Name,
    string Version,
    string Framework,
    string Protocol,
    string[] Features,
    string[] AvailableTools
);

// JSON Serialization Context
[JsonSerializable(typeof(WeatherRequest))]
[JsonSerializable(typeof(WeatherResponse))]
[JsonSerializable(typeof(SendEmailRequest))]
[JsonSerializable(typeof(SendEmailResponse))]
[JsonSerializable(typeof(SearchRequest))]
[JsonSerializable(typeof(SearchResult))]
[JsonSerializable(typeof(SearchResponse))]
[JsonSerializable(typeof(CalculateRequest))]
[JsonSerializable(typeof(CalculateResponse))]
[JsonSerializable(typeof(CreateTaskRequest))]
[JsonSerializable(typeof(TaskResponse))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(InfoResponse))]
internal sealed partial class AgentJsonContext : JsonSerializerContext;