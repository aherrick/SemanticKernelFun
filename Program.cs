#pragma warning disable SKEXP0050, SKEXP0001

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Plugins.Core;
using MongoDB.Bson.IO;
using SemanticKernelFun.Helpers;
using Spectre.Console;

// Set up configuration and load user secrets
var config = new ConfigurationBuilder().AddUserSecrets<Program>().Build();

var azureAIConfig = config.GetSection(nameof(AzureAIConfig)).Get<AzureAIConfig>();
var azureSearchConfig = config.GetSection(nameof(AzureSearchConfig)).Get<AzureSearchConfig>();

// Define constants for menu options
const string OptionRecreateIndexDataImport = "1) Recreate Index w/ Data Import";
const string OptionSearchIndex = "2) Search Index";
const string OptionBasicChat = "3) Basic Chat";
const string OptionExit = "Exit";

var KM = KernelHelper.GetKernelMemory(azureAIConfig, azureSearchConfig);
var KernelChat = KernelHelper.GetKernelChatCompletion(azureAIConfig);

// Keep showing menu options until the user decides to exit{
while (true)
{
    // Show a selection menu with constants
    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[blue]Choose an option:[/]")
            .AddChoices(
                OptionRecreateIndexDataImport,
                OptionSearchIndex,
                OptionBasicChat,
                OptionExit
            )
    );

    // Handle the choice using constants
    switch (choice)
    {
        case OptionRecreateIndexDataImport:

            await KM.DeleteIndexAsync(azureSearchConfig.Index);

            // Prompt the user for the folder path
            string folderPath = AnsiConsole.Ask<string>(
                "[blue]Enter the folder path to read files from:[/]"
            );

            Console.WriteLine("Import Start... ");

            var files = Directory.GetFiles(folderPath);
            foreach (var file in files)
            {
                AnsiConsole.MarkupLine($"[yellow]{Path.GetFileName(file.EscapeMarkup())}[/]");

                await UtilHelper
                    .Polly()
                    .ExecuteAsync(async cancellationToken =>
                    {
                        await KM.ImportTextAsync(
                            text: await File.ReadAllTextAsync(file, cancellationToken),
                            index: azureSearchConfig.Index,
                            cancellationToken: cancellationToken
                        );
                    });
            }

            Console.WriteLine("Import Complete!");

            break;

        case OptionSearchIndex:

            while (true)
            {
                // Get a question from the user
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Question: ");
                var userInput = Console.ReadLine() ?? string.Empty;

                if (string.IsNullOrEmpty(userInput))
                {
                    break;
                }

                // Display answer text as it is being generated
                Console.ForegroundColor = ConsoleColor.Yellow;
                var result = await KM.AskAsync(userInput, index: azureSearchConfig.Index);

                // Answer
                Console.WriteLine($"\nAnswer: {result.Result}");

                // Sources
                foreach (var x in result.RelevantSources)
                {
                    Console.WriteLine(
                        $"  - {x.SourceName}  - {x.Link} [{x.Partitions.First().LastUpdate:D}]"
                    );
                }
            }

            break;

        case OptionBasicChat:

            KernelChat.Plugins.AddFromType<TimePlugin>();

            var promptExecutionSettings = new PromptExecutionSettings()
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var chatService = KernelChat.GetRequiredService<IChatCompletionService>();
            var chatHistory = new ChatHistory(
                //"""
                //You are a friendly assistant who helps users with their tasks.
                //You will complete required steps and request approval before taking any consequential actions.
                //If the user doesn't provide enough information for you to complete a task, you will keep asking questions until you have enough information to complete the task.
                //"""

                """
                   You are an AI assistant who likes to follow the rules.

                """
            //  You are ONLY to answer questions from retrieved documents.Ensure you check the documents before answering.
            //If the question cannot be found in retrieved documents, respond I don't know. Do NOT give them an answer if you couldn't retrieve it from the documents.
            );

            //
            // Basic chat
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("User > ");
                chatHistory.AddUserMessage(Console.ReadLine());

                var updates = chatService.GetStreamingChatMessageContentsAsync(
                    chatHistory,
                    promptExecutionSettings,
                    KernelChat
                );

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant > ");
                var sb = new StringBuilder();
                await foreach (var update in updates)
                {
                    sb.Append(update.Content);
                    Console.Write(update.Content);
                }

                chatHistory.AddAssistantMessage(sb.ToString());

                Console.WriteLine();
            }
        case OptionExit:
            AnsiConsole.MarkupLine("[red]Exiting...[/]");
            Environment.Exit(0); // Exit the application
            break;
    }
}