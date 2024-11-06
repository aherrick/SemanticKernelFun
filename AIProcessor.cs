#pragma warning disable SKEXP0050, SKEXP0001, SKEXP0070

using System.Net;
using System.Text;
using System.Text.Json;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.KernelMemory;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Microsoft.SemanticKernel.Text;
using MongoDB.Bson.IO;
using MoreRAGFun.Models;
using SemanticKernelFun.Data;
using SemanticKernelFun.Helpers;
using SemanticKernelFun.Models;
using Spectre.Console;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.Graphics;

namespace SemanticKernelFun;

public static class AIProcessor
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

            await PollyHelper
                .Retry()
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

    public static async Task ImageDescription(AzureAIConfig azureAIConfig)
    {
        // Prompt the user for the folder path
        string folderPath = AnsiConsole.Ask<string>(
            "[blue]Enter the folder path to read images from:[/]"
        );

        var searchPatterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif" };

        var files = searchPatterns.SelectMany(pattern => Directory.GetFiles(folderPath, pattern));

        var chatCompletionService = KernelHelper
            .GetKernelChatCompletion(azureAIConfig)
            .GetRequiredService<IChatCompletionService>();

        foreach (var file in files)
        {
            byte[] imageData = File.ReadAllBytes(file);
            var mimeTypes = new Dictionary<string, string>
            {
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".png", "image/png" },
                { ".gif", "image/gif" }
            };

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(
                [
                    new Microsoft.SemanticKernel.TextContent("What’s in this image?"),
                    new Microsoft.SemanticKernel.ImageContent(
                        imageData,
                        mimeTypes[Path.GetExtension(file)]
                    )
                ]
            );

            await PollyHelper
                .Retry()
                .ExecuteAsync(async cancellationToken =>
                {
                    var result = await chatCompletionService.GetChatMessageContentsAsync(
                        chatHistory,
                        cancellationToken: cancellationToken
                    );
                    var textFromImage = string.Join("\n", result.Select(x => x.Content));

                    Console.WriteLine($"{Path.GetFileName(file)}: {textFromImage}");
                });
        }
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

    public static async Task AzureAIRAGVectorStore(
        AzureAIConfig azureAIConfig,
        AzureSearchConfig azureSearchConfig
    )
    {
        var kernel = KernelHelper.GetKernelVectorStore(azureAIConfig, azureSearchConfig);

        // Prompt the user for the folder path

        var folderPath = AnsiConsole.Prompt(
            new TextPrompt<string>(
                "[blue]Enter the folder path to read files from (Enter to skip):[/]"
            ).AllowEmpty()
        );

        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            Console.WriteLine("Ingesting Documents...");

            var textEmbeddingGenerationService =
                kernel.Services.GetRequiredService<ITextEmbeddingGenerationService>();

            var vectorStoreRecordService = kernel.GetRequiredService<
                IVectorStoreRecordCollection<string, TextSnippet<string>>
            >();

            // just delete and readd for
            await vectorStoreRecordService.DeleteCollectionAsync();
            await vectorStoreRecordService.CreateCollectionIfNotExistsAsync();

            var files = await FileProcessor.ProcessFiles(folderPath);

            var batches = files.Chunk(10);

            // Process each batch of content items for images
            foreach (var batch in batches)
            {
                // [2] Chunk (split into shorter strings on natural boundaries)

                var records = new List<TextSnippet<string>>();
                foreach (var rcd in batch)
                {
                    var paragraphs = TextChunker.SplitPlainTextParagraphs([rcd.Text], 2000);

                    foreach (var paragraph in paragraphs)
                    {
                        records.Add(
                            new TextSnippet<string>()
                            {
                                Key = rcd.Id,
                                Text = paragraph,
                                ReferenceDescription = rcd.FileName,
                                ReferenceLink = rcd.Id,
                                TextEmbedding = await GetEmbeddings(
                                    paragraph,
                                    textEmbeddingGenerationService
                                )
                            }
                        );
                    }
                }

                var upsertedKeys = vectorStoreRecordService.UpsertBatchAsync(records);
                await foreach (var key in upsertedKeys.ConfigureAwait(false))
                {
                    Console.WriteLine($"Upserted record '{key}' into VectorDB");
                }

                await Task.Delay(10_000);
            }

            Console.WriteLine("Ingesting Complete.");
        }

        var vectorStoreSearchService = kernel.GetRequiredService<
            VectorStoreTextSearch<TextSnippet<string>>
        >();

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Assistant > Press enter with no prompt to exit.");

        // Add a search plugin to the kernel which we will use in the template below
        // to do a vector search for related information to the user query.
        kernel.Plugins.Add(vectorStoreSearchService.CreateWithGetTextSearchResults("SearchPlugin"));

        // Start the chat loop.
        while (true)
        {
            // Prompt the user for a question.
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(
                $"Assistant > What would you like to know from the loaded documents?"
            );

            // Read the user question.
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("User > ");
            var question = Console.ReadLine();

            // Exit the application if the user didn't type anything.
            if (string.IsNullOrWhiteSpace(question))
            {
                break;
            }

            // Invoke the LLM with a template that uses the search plugin to
            // 1. get related information to the user query from the vector store
            // 2. add the information to the LLM prompt.
            var response = kernel.InvokePromptStreamingAsync(
                promptTemplate: """
                Please use this information to answer the question:
                {{#with (SearchPlugin-GetTextSearchResults question)}}
                  {{#each this}}
                    Name: {{Name}}
                    Value: {{Value}}
                    Link: {{Link}}
                    -----------------
                  {{/each}}
                {{/with}}

                Include citations to the relevant information where it is referenced in the response.

                Question: {{question}}
                """,
                arguments: new KernelArguments() { { "question", question }, },
                templateFormat: "handlebars",
                promptTemplateFactory: new HandlebarsPromptTemplateFactory()
            );

            // Stream the LLM response to the console with error handling.
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nAssistant > ");

            try
            {
                await foreach (var message in response)
                {
                    // Console.WriteLine(JsonSerializer.Serialize(message.Metadata));

                    Console.Write(message);
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Call to LLM failed with error: {ex}");
            }
        }
    }

    private static async Task<ReadOnlyMemory<float>> GetEmbeddings(
        string text,
        ITextEmbeddingGenerationService textEmbeddingGenerationService
    )
    {
        return await PollyHelper
            .Retry()
            .ExecuteAsync(async cancellationToken =>
            {
                return await textEmbeddingGenerationService.GenerateEmbeddingAsync(
                    text,
                    cancellationToken: cancellationToken
                );
            });
    }
}