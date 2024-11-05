#pragma warning disable SKEXP0050, SKEXP0001, SKEXP0070

using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Memory;
using SemanticKernelFun.Helpers;
using SemanticKernelFun.Models;
using Spectre.Console;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;

namespace SemanticKernelFun;

public static class Processor
{
    public static async Task LocalAIRAG(LocalAIConfig localAIConfig)
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOnnxRuntimeGenAIChatCompletion(
            modelId: localAIConfig.PhiModelId,
            modelPath: localAIConfig.PhiModelPath
        );
        builder.AddBertOnnxTextEmbeddingGeneration(
            onnxModelPath: localAIConfig.BgeModelPath,
            vocabPath: localAIConfig.BgeModelVocabPath
        );

        //.AddLocalTextEmbeddingGeneration(); // this seems to have an issue, see https://github.com/dotnet-smartcomponents/smartcomponents/issues/75
        var kernel = builder.Build();

        var embeddingGenerator = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        var memoryBuilder = new MemoryBuilder();

        memoryBuilder
            .WithMemoryStore(new VolatileMemoryStore())
            .WithTextEmbeddingGeneration(embeddingGenerator);

        var memory = memoryBuilder.Build();

        string collectionName = "AndrewHerrickFacts";

        var tasks = Facts
            .GetFacts()
            .Select(f =>
                memory.SaveInformationAsync(
                    collection: collectionName,
                    id: f.Id,
                    text: f.Text,
                    description: f.Description,
                    additionalMetadata: f.Metadata,
                    kernel: kernel
                )
            );

        await Task.WhenAll(tasks);

        kernel.ImportPluginFromObject(new TextMemoryPlugin(memory));

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"You > ");
            var question = Console.ReadLine()!.Trim();
            if (question.Equals("exit", StringComparison.OrdinalIgnoreCase))
                break;

            var prompt =
                @"
        Question: {{$input}}
        Answer the question using the memory content: {{Recall}}";

            OpenAIPromptExecutionSettings openAIPromptExecutionSettings =
                new()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                    Temperature = 1,
                    MaxTokens = 200
                };

            var response = kernel.InvokePromptStreamingAsync(
                promptTemplate: prompt,
                arguments: new KernelArguments(openAIPromptExecutionSettings)
                {
                    { "collection", collectionName },
                    { "input", question },
                }
            );

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("AI > ");

            string combinedResponse = string.Empty;
            await foreach (var message in response)
            {
                Console.Write(message);
                combinedResponse += message;
            }

            Console.WriteLine();
        }
    }

    public static async Task LocalAIChat(LocalAIConfig localAIConfig)
    {
        var builderLocalChat = Kernel.CreateBuilder();

        builderLocalChat.AddOnnxRuntimeGenAIChatCompletion("phi-3", localAIConfig.PhiModelPath);
        await AIChat(builderLocalChat.Build());
    }

    public static async Task AzureAIRAG(
        AzureAIConfig azureAIConfig,
        AzureSearchConfig azureSearchConfig
    )
    {
        var KM = KernelHelper.GetKernelMemory(azureAIConfig, azureSearchConfig);

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
    }

    public static async Task AzureAIChat(AzureAIConfig azureAIConfig)
    {
        var KernelChat = KernelHelper.GetKernelChatCompletion(azureAIConfig);

        await AIChat(KernelChat);
    }

    private static async Task AIChat(Kernel kernel)
    {
        kernel.Plugins.AddFromType<TimePlugin>();

        var promptExecutionSettings = new PromptExecutionSettings()
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
        };

        var chatService = kernel.GetRequiredService<IChatCompletionService>();
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
            Console.Write("User > ");
            chatHistory.AddUserMessage(Console.ReadLine());

            var updates = chatService.GetStreamingChatMessageContentsAsync(
                chatHistory,
                promptExecutionSettings,
                kernel
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
    }
}