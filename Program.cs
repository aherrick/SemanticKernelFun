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
const string OptionSpeechToText = "Speech To Text (Azure)";
const string OptionMsftExtensionAIChat = "Microsoft Extension AI Chat (Azure)";
const string OptionOllamaChat = "Ollama Chat (Local)";

const string OptionExit = "Exit";

// Keep showing menu options until the user decides to exit
while (true)
{
    // Show a selection menu with constants
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[blue]Choose an option:[/]")
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
                OptionSpeechToText,
                OptionMsftExtensionAIChat,
                OptionOllamaChat,

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

        case OptionSpeechToText:

            await AIProcessor.SpeachToTextChat(azureAIConfig);
            break;

        case OptionMsftExtensionAIChat:

            await AIProcessor.MsftExtensionAIChat(azureAIConfig);
            break;

        case OptionOllamaChat:

            await AIProcessor.OllamaChat(ollamaAIConfig);
            break;

        case OptionExit:
            AnsiConsole.MarkupLine("[red]Exiting...[/]");
            Environment.Exit(0); // Exit the application
            break;
    }
}