using Microsoft.Extensions.Configuration;
using SemanticKernelFun.Helpers;
using SemanticKernelFun.Models;
using Spectre.Console;

Console.OutputEncoding = System.Text.Encoding.UTF8;

// Load configuration and secrets
var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var azureAIConfig = config.GetSection(nameof(AzureAIConfig)).Get<AzureAIConfig>();
var azureSearchConfig = config.GetSection(nameof(AzureSearchConfig)).Get<AzureSearchConfig>();
var localAIConfig = config.GetSection(nameof(LocalAIConfig)).Get<LocalAIConfig>();
var ollamaAIConfig = config.GetSection(nameof(OllamaAIConfig)).Get<OllamaAIConfig>();
var qdrantClientConfig = config.GetSection(nameof(QdrantClientConfig)).Get<QdrantClientConfig>();
var githubAIConfig = config.GetSection(nameof(GithubAIConfig)).Get<GithubAIConfig>();

// Menu option constants
const string OptionAzureChat = "Chat Basic (Azure)";
const string OptionAzureRAG = "RAG Basic (Azure)";
const string OptionAzureRAGVectorStore = "RAG Vector Store (Azure)";
const string OptionAzureRAGSearchDataSource = "RAG Search Data Source (Azure)";
const string OptionLocalChat = "Chat Basic (Local)";
const string OptionLocalRAG = "RAG Basic (Local)";
const string OptionImageDescription = "Image Description (Azure)";
const string OptionChatToolRecipe = "Chat Tool Recipe (Azure)";
const string OptionAgentChat = "Agent Chat (Azure)";
const string OptionAgentChatRapBattle = "Agent Chat Rap Battle (Azure)";
const string OptionAgentSloganOrchestration = "Agent Orchestration - Slogan Workshop";
const string OptionAgentTriageOrchestration = "Agent Orchestration - Support Triage";
const string OptionSpeechToText = "Speech To Text (Azure)";
const string OptionAzureAITools = "Azure AI Tools";
const string OptionOllamaChat = "Ollama Chat (Local)";
const string OptionQdrantAILocalRagChat = "Qdrant AI Local Rag Chat";
const string OptionGithubInferenceChat = "Github Inference Chat";
const string OptionOllamaMemoryLocal = "Ollama Memory (Local)";
const string OptionSalesDataAI = "Sales Data AI";
const string OptionTripPlanner = "Trip Planner";
const string OptionTransferOrderPlanner = "Transfer Order Planner";
const string OptionExit = "[red bold]Exit (Close the application)[/]";

// Main interactive menu loop
while (true)
{
    var menuOptions = new[]
    {
        // Azure Options
        OptionAzureChat,
        OptionAzureRAG,
        OptionAzureRAGVectorStore,
        OptionAzureRAGSearchDataSource,
        OptionSpeechToText,
        OptionImageDescription,
        OptionChatToolRecipe,
        OptionAzureAITools,
        // Local Options
        OptionLocalChat,
        OptionLocalRAG,
        OptionOllamaChat,
        OptionOllamaMemoryLocal,
        // RAG & Vector
        OptionQdrantAILocalRagChat,
        OptionSalesDataAI,
        // Multi-Agent Orchestration
        OptionAgentChat,
        OptionAgentChatRapBattle,
        OptionAgentSloganOrchestration,
        OptionAgentTriageOrchestration,
        // Specialized Tools
        OptionGithubInferenceChat,
        OptionTripPlanner,
        OptionTransferOrderPlanner,
        // Exit
        OptionExit,
    };

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[blue]Choose an option:[/]")
            .PageSize(menuOptions.Length) // Show all items without scrolling
            .AddChoices(menuOptions)
    );

    switch (choice)
    {
        // Azure
        case OptionAzureChat:
            await AIProcessor.AzureAIChat(azureAIConfig);
            break;

        case OptionAzureRAG:
            await AIProcessor.AzureAIRAG(azureAIConfig, azureSearchConfig);
            break;

        case OptionAzureRAGVectorStore:
            await AIProcessor.AzureAIRAGVectorStore(azureAIConfig, azureSearchConfig);
            break;

        case OptionAzureRAGSearchDataSource:
            await AIProcessor.AzureAIRAGSearchChatDataSource(azureAIConfig, azureSearchConfig);
            break;

        case OptionSpeechToText:
            await AIProcessor.SpeachToTextChat(azureAIConfig);
            break;

        case OptionImageDescription:
            await AIProcessor.ImageDescription(azureAIConfig);
            break;

        case OptionChatToolRecipe:
            await AIProcessor.ChatToolRecipe(azureAIConfig);
            break;

        case OptionAzureAITools:
            await AIProcessor.AzureAITools(azureAIConfig);
            break;

        // Local
        case OptionLocalChat:
            await AIProcessor.LocalAIChat(localAIConfig);
            break;

        case OptionLocalRAG:
            await AIProcessor.LocalAIRAG(localAIConfig);
            break;

        case OptionOllamaChat:
            await AIProcessor.OllamaChat(ollamaAIConfig);
            break;

        case OptionOllamaMemoryLocal:
            await AIProcessor.OllamaMemoryLocal(ollamaAIConfig);
            break;

        // Vector / RAG
        case OptionQdrantAILocalRagChat:
            await AIProcessor.QdrantAILocalRag(qdrantClientConfig, ollamaAIConfig);
            break;

        case OptionSalesDataAI:
            await AIProcessor.SalesDataAI(azureAIConfig);
            break;

        // Agent-based Orchestration
        case OptionAgentChat:
            await AIProcessor.AgentChat(azureAIConfig);
            break;

        case OptionAgentChatRapBattle:
            await AIProcessor.AgentChatRapBattle(azureAIConfig);
            break;

        case OptionAgentSloganOrchestration:
            await AIProcessor.RunMultiAgentSloganOrchestration(azureAIConfig);
            break;

        case OptionAgentTriageOrchestration:
            await AIProcessor.RunMultiAgentTriageOrchestration(azureAIConfig);
            break;

        // Specialized Use Cases
        case OptionGithubInferenceChat:
            await AIProcessor.GithubInferenceChat(githubAIConfig);
            break;

        case OptionTripPlanner:
            await AIProcessor.TripPlanner(azureAIConfig);
            break;

        case OptionTransferOrderPlanner:
            await AIProcessor.TransferOrderPlanner(azureAIConfig);
            break;

        // Exit
        case OptionExit:
            AnsiConsole.MarkupLine("[red]Exiting...[/]");
            Environment.Exit(0);
            break;
    }
}