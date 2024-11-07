#pragma warning disable SKEXP0050, SKEXP0001, SKEXP0070, AOAI001

using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI.Chat;
using Microsoft.Extensions.AI;
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
using MoreRAGFun.Models;
using OpenAI.Chat;
using SemanticKernelFun.Data;
using SemanticKernelFun.Models;
using Spectre.Console;

namespace SemanticKernelFun.Helpers;

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
            Console.Write($"You > ");
            var question = Console.ReadLine().Trim();
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
            Console.Write("AI > ");

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

        await RefreshDocumentIndex(KM, azureSearchConfig.Index);

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

    private static async Task RefreshDocumentIndex(IKernelMemory KM, string index)
    {
        await KM.DeleteIndexAsync(index);

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
                        index: index,
                        cancellationToken: cancellationToken
                    );
                });
        }

        Console.WriteLine("Import Complete!");
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

    public static async Task AzureAIRAGSearchChatDataSource(
        AzureAIConfig azureAIConfig,
        AzureSearchConfig azureSearchConfig
    )
    {
        var KM = KernelHelper.GetKernelMemory(azureAIConfig, azureSearchConfig);

        await RefreshDocumentIndex(KM, azureSearchConfig.Index);

        ChatCompletionOptions options = new();
        options.AddDataSource(KernelHelper.GetAzureSearchChatDataSource(azureSearchConfig));

        var chatClient = KernelHelper.GetAzureOpenAIClient(azureAIConfig);

        while (true)
        {
            Console.Write("Enter your question: ");
            var userQuestion = Console.ReadLine();

            var chatUpdates = chatClient.CompleteChatStreamingAsync(
                [new UserChatMessage(userQuestion)],
                options
            );
            ChatMessageContext onYourDataContext = null;

            await foreach (var chatUpdate in chatUpdates)
            {
                if (chatUpdate.Role.HasValue)
                {
                    Console.WriteLine($"{chatUpdate.Role}: ");
                }
                foreach (var contentPart in chatUpdate.ContentUpdate)
                {
                    Console.Write(contentPart.Text);
                }
                onYourDataContext = chatUpdate.GetMessageContext();
            }
            Console.WriteLine();
            if (onYourDataContext?.Intent is not null)
            {
                Console.WriteLine($"Intent: {onYourDataContext.Intent}");
            }
            foreach (ChatCitation citation in onYourDataContext?.Citations ?? [])
            {
                Console.Write($"Citation: {citation.Content}");
            }
        }
    }

    public static async Task ChatToolRecipe(AzureAIConfig azureAIConfig)
    {
        var chatClient = KernelHelper.GetAzureOpenAIClient(azureAIConfig);

        ChatTool tool = ChatTool.CreateFunctionTool(
            "describe_recipe",
            null,
            BinaryData.FromObjectAsJson(
                new
                {
                    Type = "object",
                    Properties = new
                    {
                        AltText = new
                        {
                            Type = "string",
                            Description = "Very short description of what is in the recipe, to be included as alt text for screen readers."
                        },
                        Title = new { Type = "string", Description = "Short title of the recipe" }
                        //  Subject = new
                        //  {
                        //      Type = "string",
                        //      Enum = photoTypes,
                        //      Description = "Categorise the subject of the photo. If it shows one or more students or teachers, say 'people'. If it shows student work, for example artwork or an exercise book, " +
                        //"say 'student work'. For anything else, say 'other'."
                        //  }
                    },
                    Required = new[]
                    {
                        "AltText",
                        "Title" /*, "Subject"*/
                    }
                },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            )
        );

        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(
                "You are a helpful assistant who describes recipes. Be very brief, answering in a few words only. "
            //+ "The photographs are for use in an Academy newsletter, and may include students, staff, or student work. When referring to people, "
            //+ "use educational terms like 'teacher' and 'student'. Use the article topic to put the description in context. Use British English spelling."
            ),
            new UserChatMessage(
                ChatMessageContentPart.CreateTextPart(
                    """

                    Ingredients
                    For the soup:

                    3 tablespoons olive oil
                    2 medium carrots, thickly sliced
                    1 large onion, coarsely chopped
                    1 rib celery, coarsely chopped
                    1 clove garlic, finely chopped
                    3 sprigs fresh oregano
                    1/4 teaspoon salt
                    Black pepper, to taste
                    2 (15-ounce) cans cannellini beans, or other small white beans, drained and rinsed
                    5 cups chicken stock or vegetable stock
                    4 cups baby kale or baby spinach, stems removed if tough
                    1 tablespoon chopped fresh oregano, for garnish
                    Olive oil, to serve
                    Extra grated Parmesan, to serve
                    For the parmesan toasts:

                    1/2 baguette, thinly sliced
                    Olive oil
                    1/2 cup grated Parmesan

                    Method
                    Cook the vegetables:
                    In a soup pot, heat the olive oil. When it is hot, add the carrots, onion, celery, garlic, fresh oregano sprigs, salt, and pepper. Cook, stirring often, for 10 minutes until the vegetables look softened and the onions turn translucent.

                    Tuscan Bean Soup
                    Sheryl Julian
                    Prepare the beans:
                    On a plate, mash 1/2 cup of the beans with a fork or potato masher. Add them to the vegetables in the pot. Cook, stirring, for 2 minutes.

                    Tuscan Bean Soup
                    Sheryl Julian
                    Tuscan Bean Soup
                    Sheryl Julian
                    Simmer the soup:
                    Add the remaining beans to the pot and stir well. Stir in the chicken stock and bring to a boil. Lower the heat, partially cover with the lid placed askew, and simmer for 20 minutes, or until the carrots are tender and the liquid is flavorful.

                    Discard the oregano sprigs; the leaves will have fallen into the soup. Add additional salt and pepper to taste.

                    While the soup simmers, make the Parmesan toasts:
                    Toast the bread until lightly golden on both sides. While the toast is still hot from the toaster, sprinkle with olive oil and cheese. If you have a toaster oven, return them to the toaster for 1 minute to melt the cheese; otherwise, arrange the toasts in a skillet over medium heat, cover, and warm for about 1 minute or until the cheese has melted.

                    Add the greens to the soup:
                    Add the kale or spinach to the pot and simmer for another 2 minutes, or just until the greens wilt.

                    Serve the soup:
                    Ladle the soup into bowls, sprinkle with oregano and more olive oil, if you like. Serve with Parmesan toasts and extra Parmesan for sprinkling.

                    """
                )
            //ChatMessageContentPart.CreateImageMessageContentPart(
            //    photoUri,
            //    ImageChatMessageContentPartDetail.Low
            //)
            )
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0,
            // EndUserId = identifier,
            Tools = { tool }
        };

        var response = await chatClient.CompleteChatAsync(messages, options);

        var result = response.Value.ToolCalls[0].FunctionArguments.ToString();

        Console.WriteLine(result);
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