using Microsoft.Extensions.Configuration;
using SemanticKernelFun;
using SemanticKernelFun.Helpers;
using Spectre.Console;

// Set up configuration and load user secrets
var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var azureAIConfig = config.GetSection(nameof(AzureAIConfig)).Get<AzureAIConfig>();
var azureSearchConfig = config.GetSection(nameof(AzureSearchConfig)).Get<AzureSearchConfig>();
var localAIConfig = config.GetSection(nameof(LocalAIConfig)).Get<LocalAIConfig>();

// Define constants for menu options
const string OptionAzureRAG = "RAG (Azure)";
const string OptionAzureChat = "Chat (Azure)";
const string OptionLocalRAG = "RAG (Local)";
const string OptionLocalChat = "Chat (Local)";
const string OptionExit = "Exit";

// Keep showing menu options until the user decides to exit{
while (true)
{
    // Show a selection menu with constants
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[blue]Choose an option:[/]")
            .AddChoices(
                OptionAzureChat,
                OptionAzureRAG,
                OptionLocalChat,
                OptionLocalRAG,
                OptionExit
            )
    );

    // Handle the choice using constants
    switch (choice)
    {
        case OptionAzureChat:

            await Processor.AzureAIChat(azureAIConfig);
            break;

        case OptionAzureRAG:

            await Processor.AzureAIRAG(azureAIConfig, azureSearchConfig);
            break;

        case OptionLocalChat:

            await Processor.LocalAIChat(localAIConfig);
            break;

        case OptionLocalRAG:

            await Processor.LocalAIRAG(localAIConfig);
            break;

        case OptionExit:
            AnsiConsole.MarkupLine("[red]Exiting...[/]");
            Environment.Exit(0); // Exit the application
            break;
    }
}