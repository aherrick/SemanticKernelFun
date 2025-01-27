using Microsoft.Extensions.Configuration;
using SemanticKernelFun.Helpers;
using SemanticKernelFun.Models;
using Spectre.Console;

// Set up configuration and load user secrets
var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var azureAIConfig = config.GetSection(nameof(AzureAIConfig)).Get<AzureAIConfig>();
var azureSearchConfig = config.GetSection(nameof(AzureSearchConfig)).Get<AzureSearchConfig>();
var localAIConfig = config.GetSection(nameof(LocalAIConfig)).Get<LocalAIConfig>();
var ollamaAIConfig = config.GetSection(nameof(OllamaAIConfig)).Get<OllamaAIConfig>();
var qdrantClientConfig = config.GetSection(nameof(QdrantClientConfig)).Get<QdrantClientConfig>();

// Define constants for menu options
const string OptionAzureRAG = "RAG Basic (Azure)";
const string OptionAzureRAGVectorStore = "RAG Vector Store (Azure)";
const string OptionAzureRAGSearchDataSource = "RAG Search Data Source (Azure)";

const string OptionAzureChat = "Chat Basic (Azure)";
const string OptionLocalRAG = "RAG Basic (Local)";
const string OptionLocalChat = "Chat Basic (Local)";
const string OptionImageDescription = "Image Description (Azure)";
const string OptionChatToolRecipe = "Chat Tool Recipe (Azure)";
const string OptionAgentChat = "Agent Chat (Azure)";
const string OptionAgentChatRapBattle = "Agent Chat Rap Battle (Azure)";

const string OptionSpeechToText = "Speech To Text (Azure)";
const string OptionAzureAITools = "Azure AI Tools";
const string OptionOllamaChat = "Ollama Chat (Local)";
const string OptionQdrantAILocalRagChat = "Qdrant AI Local Rag Chat";
const string OptionInventoryPlannerStepwise = "Inventory Planner Stepwise";
const string OptionInventoryPlannerHandlebars = "Inventory Planner Handlebars";

const string OptionTripPlanner = "Trip Planner";
const string OptionTransferOrderPlanner = "Transfer Order Planner";

const string OptionExit = "[red bold]Exit (Close the application)[/]"; // Change the color of the exit option to red and bold

// Keep showing menu options until the user decides to exit
while (true)
{
    // Prompt the user to choose an option
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[blue]Choose an option:[/]")
            .PageSize(int.MaxValue) // Adjust the number to display more options
            .AddChoices(
                OptionAzureChat,
                OptionAzureRAGVectorStore,
                OptionAzureRAG,
                OptionAzureRAGSearchDataSource,
                OptionLocalChat,
                OptionLocalRAG,
                OptionImageDescription,
                OptionChatToolRecipe,
                OptionAgentChat,
                OptionAgentChatRapBattle,
                OptionSpeechToText,
                OptionAzureAITools,
                OptionOllamaChat,
                OptionQdrantAILocalRagChat,
                OptionInventoryPlannerStepwise,
                OptionInventoryPlannerHandlebars,
                OptionTripPlanner,
                OptionTransferOrderPlanner,
                OptionExit
            )
    );

    // Handle the choice using constants
    switch (choice)
    {
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

        case OptionLocalChat:

            await AIProcessor.LocalAIChat(localAIConfig);
            break;

        case OptionLocalRAG:

            await AIProcessor.LocalAIRAG(localAIConfig);
            break;

        case OptionImageDescription:

            await AIProcessor.ImageDescription(azureAIConfig);
            break;

        case OptionChatToolRecipe:

            await AIProcessor.ChatToolRecipe(azureAIConfig);
            break;

        case OptionAgentChat:

            await AIProcessor.AgentChat(azureAIConfig);
            break;

        case OptionAgentChatRapBattle:
            await AIProcessor.AgentChatRapBattle(azureAIConfig);
            break;

        case OptionSpeechToText:

            await AIProcessor.SpeachToTextChat(azureAIConfig);
            break;

        case OptionAzureAITools:

            await AIProcessor.AzureAITools(azureAIConfig);
            break;

        case OptionOllamaChat:

            await AIProcessor.OllamaChat(ollamaAIConfig);
            break;

        case OptionQdrantAILocalRagChat:

            await AIProcessor.QdrantAILocalRag(qdrantClientConfig, ollamaAIConfig);
            break;

        case OptionInventoryPlannerStepwise:
            await AIProcessor.InventoryPlanner(azureAIConfig, PlannerType.Stepwise);
            break;

        case OptionInventoryPlannerHandlebars:
            await AIProcessor.InventoryPlanner(azureAIConfig, PlannerType.Handlebars);
            break;

        case OptionTripPlanner:
            await AIProcessor.TripPlanner(azureAIConfig);
            break;

        case OptionTransferOrderPlanner:
            await AIProcessor.TransferOrderPlanner(azureAIConfig);
            break;

        case OptionExit:
            AnsiConsole.MarkupLine("[red]Exiting...[/]");
            Environment.Exit(0); // Exit the application
            break;
    }
}