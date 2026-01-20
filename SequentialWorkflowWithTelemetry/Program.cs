using Azure.AI.OpenAI;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.ClientModel;
using System.Diagnostics;
using System.Net;

Console.WriteLine("=== Sequential Workflow with OpenTelemetry Example ===\n");

//// Configuration
//var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
//    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable not set");
//var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
//    ?? "gpt-4o-mini";

//Console.WriteLine($"Connecting to: {endpoint}");
//Console.WriteLine($"Using deployment: {deploymentName}\n");

// Configure OpenTelemetry
var serviceName = "SequentialWorkflowDemo";
var serviceVersion = "1.0.0";

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault()
        .AddService(serviceName: serviceName, serviceVersion: serviceVersion))
    .AddSource("Microsoft.Agents.*") 
    .AddSource(serviceName)            
    .AddHttpClientInstrumentation()    
    //.AddConsoleExporter()              
    .AddOtlpExporter()              
    .Build();

var activitySource = new ActivitySource(serviceName);

Console.WriteLine("OpenTelemetry configured successfully!\n");
//using var instrumentedChatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
//    .GetChatClient(deploymentName)
//        .AsIChatClient() // Converts a native OpenAI SDK ChatClient into a Microsoft.Extensions.AI.IChatClient
//        .AsBuilder()
//        .UseFunctionInvocation()
//        .UseOpenTelemetry(sourceName: SourceName, configure: (cfg) => cfg.EnableSensitiveData = true) // enable telemetry at the chat client level
//        .Build();

IChatClient chatClient = new OpenAIClient(
    new ApiKeyCredential("myapikey"),
    new() { Endpoint = new Uri("myendpoint") })
    .GetChatClient("gpt-5-mini").AsIChatClient()
         .AsBuilder().UseOpenTelemetry(sourceName: serviceName, configure: (cfg) => cfg.EnableSensitiveData = true).Build()
        ;

// Create a sequential workflow with three translation agents
// English -> French -> Spanish -> English
Console.WriteLine("Creating sequential translation workflow...\n");

using var initActivity = activitySource.StartActivity("INIT");
using var workflowActivity = activitySource.StartActivity("CreateWorkflow");

var toFrenchAgent = chatClient.CreateAIAgent(
    instructions: "You are a translation assistant. Translate the given text to French. Only output the translation, no explanations.",
    name: "EnglishToFrenchTranslator",
    description: "Translates English text to French").AsBuilder()
    .UseOpenTelemetry(serviceName, configure: (cfg) => cfg.EnableSensitiveData = true) // enable telemetry at the agent level
    .Build(); 

var toSpanishAgent = chatClient.CreateAIAgent(
    instructions: "You are a translation assistant. Translate the given text to Spanish. Only output the translation, no explanations.",
    name: "FrenchToSpanishTranslator",
    description: "Translates French text to Spanish").AsBuilder()
    .UseOpenTelemetry(serviceName, configure: (cfg) => cfg.EnableSensitiveData = true) // enable telemetry at the agent level
    .Build();

var toEnglishAgent = chatClient.CreateAIAgent(
    instructions: "You are a translation assistant. Translate the given text to English. Only output the translation, no explanations.",
    name: "SpanishToEnglishTranslator",
    description: "Translates Spanish text to English").AsBuilder()
    .UseOpenTelemetry(serviceName, configure: (cfg) => cfg.EnableSensitiveData = true) // enable telemetry at the agent level
    .Build();

// Build sequential workflow
var workflow = new WorkflowBuilder(toFrenchAgent)
    .AddEdge(toFrenchAgent, toSpanishAgent)
    .AddEdge(toSpanishAgent, toEnglishAgent)
    .WithOutputFrom(toEnglishAgent)
    .Build();

workflowActivity?.Stop();

Console.WriteLine("Workflow created:");
Console.WriteLine("  1. English -> French");
Console.WriteLine("  2. French -> Spanish");
Console.WriteLine("  3. Spanish -> English\n");

// Execute the workflow with telemetry tracking
var inputText = "Hello, how are you today? I hope you are having a wonderful day!";
Console.WriteLine($"Input text: \"{inputText}\"\n");
Console.WriteLine("Executing workflow with OpenTelemetry instrumentation...\n");

using var executionActivity = activitySource.StartActivity("ExecuteWorkflow");
executionActivity?.SetTag("input.text", inputText);
executionActivity?.SetTag("workflow.stages", 3);

try
{
    await using var run = await InProcessExecution.StreamAsync(
        workflow,
        new ChatMessage(ChatRole.User, inputText));

    // Send turn token to start processing
    await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

    var stageNumber = 1;
    var lastOutput = inputText;

    // Watch the workflow execution events
    await foreach (var evt in run.WatchStreamAsync())
    {
        using var eventActivity = activitySource.StartActivity($"ProcessEvent.{evt.GetType().Name}");

        //if (evt is AgentResponseUpdateEvent updateEvent)
        //{
        //    eventActivity?.SetTag("executor.id", updateEvent.ExecutorId);
        //    eventActivity?.SetTag("stage.number", stageNumber);

        //    if (updateEvent.Data?.Contents != null && updateEvent.Data.Contents.Count > 0)
        //    {
        //        var content = updateEvent.Data.Contents[0].ToString();
        //        if (!string.IsNullOrWhiteSpace(content))
        //        {
        //            lastOutput = content;
        //            Console.WriteLine($"[Stage {stageNumber}] {updateEvent.ExecutorId}:");
        //            Console.WriteLine($"  Output: \"{content}\"\n");

        //            eventActivity?.SetTag("output.text", content);
        //            eventActivity?.SetTag("output.length", content.Length);
        //        }
        //    }
        //}
         if (evt is ExecutorCompletedEvent completedEvent)
        {
            eventActivity?.SetTag("executor.id", completedEvent.ExecutorId);
            eventActivity?.SetTag("executor.completed", true);

            Console.WriteLine($"[Stage {stageNumber}] Completed: {completedEvent.ExecutorId}\n");
            stageNumber++;
        }
        else
        {
            Console.WriteLine($"[Event] {evt.GetType().Name} received.");
        }
    }

    //executionActivity?.SetTag("workflow.completed", true);
    //executionActivity?.SetTag("final.output", lastOutput);

    Console.WriteLine("=== Workflow Execution Complete ===");
    Console.WriteLine($"Final output: \"{lastOutput}\"");
    Console.WriteLine($"\nNotice: The text went through three translations (English -> French -> Spanish -> English)");
    Console.WriteLine("and might have slight variations due to translation nuances.\n");
}
catch (Exception ex)
{
    //executionActivity?.SetTag("workflow.error", true);
    //executionActivity?.SetTag("error.message", ex.Message);
    //executionActivity?.AddException(ex);

    Console.WriteLine($"Error executing workflow: {ex.Message}");
    throw;
}

Console.WriteLine("OpenTelemetry traces have been exported to console.");
Console.WriteLine("In production, you would export to OTLP endpoint (Jaeger, Zipkin, etc.)");
