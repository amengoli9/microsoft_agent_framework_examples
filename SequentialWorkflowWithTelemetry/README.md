# Sequential Workflow with OpenTelemetry

This project demonstrates a sequential workflow using the Microsoft Agent Framework with full OpenTelemetry instrumentation for observability.

## Features

- Sequential multi-agent workflow (3-stage translation pipeline)
- OpenTelemetry tracing for complete workflow observability
- Activity tracking for each workflow stage
- Console exporter for immediate visibility
- Support for OTLP export to external observability platforms

## Workflow Architecture

The example implements a sequential translation pipeline:

```
English Text
    ↓
┌─────────────────────────┐
│ English → French Agent  │
└─────────────────────────┘
    ↓
┌─────────────────────────┐
│ French → Spanish Agent  │
└─────────────────────────┘
    ↓
┌─────────────────────────┐
│ Spanish → English Agent │
└─────────────────────────┘
    ↓
English Text (round-trip)
```

## OpenTelemetry Instrumentation

### Traces Captured

1. **Workflow Creation** - Timing and configuration of the workflow setup
2. **Workflow Execution** - Overall execution time and completion status
3. **Individual Stage Processing** - Each agent's execution with input/output
4. **HTTP Instrumentation** - Automatic tracking of Azure OpenAI API calls
5. **Error Tracking** - Exception capture and error states

### Trace Attributes

Each activity includes relevant attributes:
- `input.text` - Input text for processing
- `output.text` - Output from each stage
- `executor.id` - Identifier of the executing agent
- `stage.number` - Sequential stage number
- `workflow.stages` - Total number of stages
- `workflow.completed` - Completion status
- `error.message` - Error details if applicable

## Prerequisites

- .NET 10.0 or later
- Azure OpenAI endpoint and deployment
- Azure CLI credential configured

## Configuration

Set the following environment variables:

```bash
export AZURE_OPENAI_ENDPOINT="https://your-resource.openai.azure.com/"
export AZURE_OPENAI_DEPLOYMENT_NAME="gpt-4o-mini"
```

Or on Windows:
```cmd
set AZURE_OPENAI_ENDPOINT=https://your-resource.openai.azure.com/
set AZURE_OPENAI_DEPLOYMENT_NAME=gpt-4o-mini
```

## Running the Example

```bash
dotnet run
```

## Output

The application will display:
1. Workflow configuration details
2. Each stage's translation output
3. OpenTelemetry trace data in the console
4. Final round-trip translation result

## Extending to Production Observability

To export traces to production observability platforms, replace the console exporter:

### Jaeger
```csharp
.AddOtlpExporter(options =>
{
    options.Endpoint = new Uri("http://localhost:4317");
})
```

### Application Insights
```csharp
.AddAzureMonitorTraceExporter(options =>
{
    options.ConnectionString = "your-connection-string";
})
```

### Custom OTLP Endpoint
```csharp
.AddOtlpExporter(options =>
{
    options.Endpoint = new Uri("https://your-otlp-endpoint");
    options.Protocol = OtlpExportProtocol.Grpc;
})
```

## Key Concepts

### WorkflowBuilder
Creates sequential workflows by defining edges between executors (agents).

```csharp
var workflow = new WorkflowBuilder(firstAgent)
    .AddEdge(firstAgent, secondAgent)
    .AddEdge(secondAgent, thirdAgent)
    .WithOutputFrom(thirdAgent)
    .Build();
```

### InProcessExecution
Executes workflows and provides streaming event access.

```csharp
await using var run = await InProcessExecution.StreamAsync(workflow, message);
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
```

### WorkflowEvent
Events emitted during workflow execution:
- `AgentResponseUpdateEvent` - Agent responses/updates
- `ExecutorCompletedEvent` - Stage completion notifications

### OpenTelemetry ActivitySource
Creates and manages distributed tracing activities.

```csharp
var activitySource = new ActivitySource("ServiceName");
using var activity = activitySource.StartActivity("OperationName");
activity?.SetTag("key", "value");
```

## Code Structure

```csharp
// 1. Configure OpenTelemetry
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("Microsoft.Agents.*")
    .AddConsoleExporter()
    .Build();

// 2. Create sequential workflow
var workflow = new WorkflowBuilder(agent1)
    .AddEdge(agent1, agent2)
    .AddEdge(agent2, agent3)
    .Build();

// 3. Execute with telemetry
using var activity = activitySource.StartActivity("ExecuteWorkflow");
await using var run = await InProcessExecution.StreamAsync(workflow, message);
await foreach (var evt in run.WatchStreamAsync())
{
    // Process events with telemetry tracking
}
```

## Benefits of OpenTelemetry Integration

1. **End-to-end Visibility** - Track workflow execution from start to finish
2. **Performance Monitoring** - Identify bottlenecks in agent processing
3. **Error Tracking** - Capture and diagnose failures
4. **Distributed Tracing** - Follow requests across multiple services
5. **Production Debugging** - Troubleshoot issues in live environments
