using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using System.Net;


// Configuration - Replace these with your Azure OpenAI settings
//var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
//    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT environment variable not set");
//var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
//    ?? "gpt-5-mini";

IChatClient chatClient = new OpenAIClient(
    new ApiKeyCredential("myapikey"), 
    new() { Endpoint = new Uri("myendpoint") })
    .GetChatClient("gpt-5-mini-2").AsIChatClient();

AIAgent agent = chatClient.CreateAIAgent(
    instructions: "You are a helpful assistant that provides clear and concise answers.",
    name: "SimpleAssistant",
    description: "A basic AI assistant for demonstration purposes");

Console.WriteLine("Agent created successfully!");
Console.WriteLine($"Agent Name: {agent.Name}");
Console.WriteLine($"Agent Description: {agent.Description}");
Console.WriteLine($"Agent ID: {agent.Id}\n");

// Example 1: Simple single-turn interaction
Console.WriteLine("--- Example 1: Single-turn conversation ---");
var response = await agent.RunAsync("Parlami di devromagna");
//Console.WriteLine("--- Example 1: Single-turn conversation ---");
//var response = await agent.RunAsync("What is the capital of France?");
Console.WriteLine($"User: What is the capital of France?");
Console.WriteLine($"Agent: {response}\n");

// Example 2: Streaming response
Console.WriteLine("--- Example 2: Streaming response ---");
Console.Write("Agent: ");
await foreach (var update in agent.RunStreamingAsync("Tell me a short joke about programming."))
{
    if (update.Contents.Count > 0)
    {
        Console.Write(update.Contents[0].ToString());
    }
}
Console.WriteLine("\n");

// Example 3: Multi-turn conversation with thread
Console.WriteLine("--- Example 3: Multi-turn conversation with context ---");
var thread = agent.GetNewThread();

var firstResponse = await agent.RunAsync("I'm thinking of learning a new programming language.", thread);
Console.WriteLine($"User: I'm thinking of learning a new programming language.");
Console.WriteLine($"Agent: {firstResponse}\n");

var secondResponse = await agent.RunAsync("What are the pros and cons of the first option you mentioned?", thread);
Console.WriteLine($"User: What are the pros and cons of the first option you mentioned?");
Console.WriteLine($"Agent: {secondResponse}\n");

Console.WriteLine("Examples completed!");
