# Simple AIAgent Example

This project demonstrates the basic usage of the Microsoft Agent Framework to create and run a simple AI agent.

## Features

- Creating an AIAgent from an Azure OpenAI chat client
- Single-turn conversation
- Streaming responses
- Multi-turn conversation with context preservation using AgentThread

## Prerequisites

- .NET 10.0 or later
- Azure OpenAI endpoint and deployment
- Azure CLI credential configured (or modify to use API key)

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

## Key Concepts

### AIAgent
The core abstraction for AI agents. You can create one by converting any `IChatClient` using the `AsAIAgent()` extension method.

### AgentThread
Maintains conversation history and context across multiple turns.

### Streaming
Agents support both regular and streaming responses for real-time interaction.

## Code Overview

```csharp
// Create chat client
var chatClient = new AzureOpenAIClient(
    new Uri(endpoint),
    new AzureCliCredential())
    .GetChatClient(deploymentName);

// Convert to AIAgent
var agent = chatClient.AsAIAgent(
    instructions: "You are a helpful assistant",
    name: "SimpleAssistant");

// Run agent
var response = await agent.RunAsync("Hello!");

// Multi-turn with context
var thread = await agent.GetNewThreadAsync();
await agent.RunAsync("First message", thread);
await agent.RunAsync("Follow-up", thread);
```
