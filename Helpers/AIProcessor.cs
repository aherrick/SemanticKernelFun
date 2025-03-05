#pragma warning disable SKEXP0050, SKEXP0001, SKEXP0070, AOAI001, SKEXP0110, SKEXP0010, SKEXP0060, SKEXP0101, SKEXP0101, SKEXP0110

using System.ClientModel;
using System.Text;
using System.Text.Json;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.VectorData;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.KernelMemory.AI.Ollama;
using Microsoft.KernelMemory.DocumentStorage.DevTools;
using Microsoft.KernelMemory.FileSystem.DevTools;
using Microsoft.KernelMemory.MemoryStorage.DevTools;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using Microsoft.SemanticKernel.AudioToText;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Planning;
using Microsoft.SemanticKernel.Planning.Handlebars;
using Microsoft.SemanticKernel.Plugins.Core;
using Microsoft.SemanticKernel.Plugins.Memory;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Microsoft.SemanticKernel.Text;
using MoreRAGFun.Models;
using NAudio.Wave;
using OpenAI;
using OpenAI.Chat;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SemanticKernelFun.Data;
using SemanticKernelFun.Models;
using SemanticKernelFun.Plugins.InventoryPlanner;
using SemanticKernelFun.Plugins.TransferOrderPlanner;
using SemanticKernelFun.Plugins.TripPlanner;
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

            OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
                Temperature = 1,
                MaxTokens = 200,
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
                { ".gif", "image/gif" },
            };

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(
                [
                    new Microsoft.SemanticKernel.TextContent("What’s in this image?"),
                    new Microsoft.SemanticKernel.ImageContent(
                        imageData,
                        mimeTypes[Path.GetExtension(file)]
                    ),
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
        string[] requiredFields =
        [
            "AltText",
            "Title", /*, "Subject"*/
        ];
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
                            Description = "Very short description of what is in the recipe, to be included as alt text for screen readers.",
                        },
                        Title = new { Type = "string", Description = "Short title of the recipe" },
                        //  Subject = new
                        //  {
                        //      Type = "string",
                        //      Enum = photoTypes,
                        //      Description = "Categorise the subject of the photo. If it shows one or more students or teachers, say 'people'. If it shows student work, for example artwork or an exercise book, " +
                        //"say 'student work'. For anything else, say 'other'."
                        //  }
                    },
                    Required = requiredFields,
                },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }
            )
        );
        var messages = new List<OpenAI.Chat.ChatMessage>
        {
            new SystemChatMessage(
                "You are a helpful assistant. Be very brief, answering in a few words only."
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
            ),
        };

        var options = new ChatCompletionOptions
        {
            Temperature = 0,
            // EndUserId = identifier,
            Tools = { tool },
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
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
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
                                ),
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
                arguments: new KernelArguments() { { "question", question } },
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

    // https://github.com/rwjdk/AiTalk2024/tree/main/src/AgentGroupChat
    public static async Task AgentChat(AzureAIConfig azureAIConfig)
    {
        var kernel = KernelHelper.GetKernelChatCompletion(azureAIConfig);

        var storyTeller = new ChatCompletionAgent
        {
            Name = "StoryTeller",
            Kernel = kernel,
            Instructions =
                "You are a StoryTeller that tell short 100 words stories about dragons. "
                + "Mention the word Dragon as much as possible. "
                + "If you see one of your stories are Censored you get angry and refuse to tell more stories (give a long answer why this is unfair and include the words 'NO MORE STORIES').",
        };

        var reviewer = new ChatCompletionAgent
        {
            Name = "Reviewer",
            Kernel = kernel,
            Instructions =
                "You are a Surfer Dude Critic of Dragon stories. you like to use emojii a lot so include a bunch in your response. You're totally gnarly. You Rate the quality of stories! Review length a couple of sentences and always include a score of 1-10. Be crticial. If the story does not include anything about a Dragon then say 'whatever man!'",
        };

        var censor = new ChatCompletionAgent
        {
            Name = "Censor",
            Kernel = kernel,
            Instructions =
                "Check if the StoryTeller told a story and if so Repeat the last story but replace the word 'Dragon' and all derivatives with the word '<CENSORED>'!. Do not write your own stories.",
        };

        var groupChat = new AgentGroupChat(storyTeller, reviewer, censor)
        {
            ExecutionSettings = new AgentGroupChatSettings
            {
                SelectionStrategy = new SequentialSelectionStrategy { InitialAgent = storyTeller },
                TerminationStrategy = new RegexTerminationStrategy("NO MORE STORIES"),
            },
        };

        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("Meet our chat-participants");
        Console.WriteLine(
            "- John is our StoryTeller; he love telling stories about dragons... But it a bit edgy if people mess with his stories"
        );
        Console.WriteLine(
            "- Wayne is our Reviewer... When he does not surf 🏄 he rate dragon stories"
        );
        Console.WriteLine(
            "- Mr. Smith is a Censor... His biggest goal in life if to censor stories... Especially about dragons!"
        );
        Console.WriteLine(
            "Press any key to see how these three get along if you drop them into a group-chat..."
        );
        Console.ReadKey();
        Console.Clear();

        Console.Write("What should the story be about (other than dragons of course...): ");
        var question = Console.ReadLine() ?? "";
        groupChat.AddChatMessage(
            new Microsoft.SemanticKernel.ChatMessageContent(
                AuthorRole.User,
                "tell a story about: " + question
            )
        );

        IAsyncEnumerable<StreamingChatMessageContent> response = groupChat.InvokeStreamingAsync();
        string speaker = string.Empty;
        await foreach (var chunk in response)
        {
            if (speaker != chunk.AuthorName)
            {
                if (!string.IsNullOrWhiteSpace(speaker))
                {
                    Console.WriteLine();
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadKey();
                }

                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("***");
                Console.ForegroundColor = ConsoleColor.Green;
                speaker = chunk.AuthorName ?? "Unknown";
                Console.WriteLine(speaker + ":");
                Console.ForegroundColor = ConsoleColor.White;
            }

            Console.Write(chunk.Content ?? "");
        }

        Console.WriteLine();
        Console.WriteLine("THE END");
        Console.WriteLine();
    }

    public static async Task AgentChatRapBattle(AzureAIConfig azureAIConfig)
    {
        var KernelChat = KernelHelper.GetKernelChatCompletion(azureAIConfig);

        string rapMCName = "RapMCName";
        string rapMCInstructions =
            "You are a rap MC and your role is to review the rap lyrics in a rap battle and give it a score. Participants in the content will be given a topic and they will need to create a hip hop version of it. You can use the Advanced RAG plugin to get the information you need about the given topic. You're going to give to the each rap lyrics a score between 1 and 10. You must score them separately. The rapper who gets the higher score wins. You can search for information or rate the lyrics. You aren't allowed to write lyrics on your own and join the rap battle.";

        string eminemName = "Eminem";
        string eminemInstructions =
            "You are a rapper and you rap in the stlye of Eminem. You are participating to a rap battle. You will be given a topic and you will need to create the lyrics and rap about it.";

        string jayZName = "JayZ";
        string jayZInstructions =
            "You are a rapper and you rap in the stlye of Jay-Z. You are participating to a rap battle. You will be given a topic and you will need to create the lyrics and rap about it.";

#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
        ChatCompletionAgent rapMCAgent = new()
        {
            Name = rapMCName,
            Instructions = rapMCInstructions,
            Kernel = KernelChat,
        };

        ChatCompletionAgent eminemAgent = new()
        {
            Name = eminemName,
            Instructions = eminemInstructions,
            Kernel = KernelChat,
        };

        ChatCompletionAgent jayZAgent = new()
        {
            Name = jayZName,
            Instructions = jayZInstructions,
            Kernel = KernelChat,
        };

        KernelFunction terminateFunction = KernelFunctionFactory.CreateFromPrompt(
            $$$"""
                A rap battle is completed once all the participants have created lyrics for the given topic, a score is given and a winner is determined.
                Determine if the rap battle is completed.  If so, respond with a single word: yes.

                History:
                {{$history}}
            """
        );

        KernelFunction selectionFunction = KernelFunctionFactory.CreateFromPrompt(
            $$$"""
            Your job is to determine which participant takes the next turn in a conversation according to the action of the most recent participant.
            State only the name of the participant to take the next turn.

            Choose only from these participants:
            - {{{rapMCName}}}
            - {{{eminemName}}}
            - {{{jayZName}}}

            Always follow these steps when selecting the next participant:
            1) After user input, it is {{{rapMCName}}}'s turn to get information about the given topic.
            2) After {{{rapMCName}}} replies, it's {{{eminemName}}}'s turn to create rap lyrics based on the results returned by {{{rapMCName}}}.
            3) After {{{eminemName}}} replies, it's {{{jayZName}}}'s turn to create rap lyrics based on the results returned by {{{rapMCName}}}.
            4) After {{{jayZName}}} replies, it's {{{rapMCName}}}'s turn to review the rap lyrics and give it a score.
            5) {{{rapMCName}}} will declare the winner based on who got the higher score.

            History:
            {{$history}}
            """
        );

        var chat = new AgentGroupChat(rapMCAgent, eminemAgent, jayZAgent)
        {
            ExecutionSettings = new()
            {
                TerminationStrategy = new KernelFunctionTerminationStrategy(
                    terminateFunction,
                    KernelChat
                )
                {
                    Agents = [rapMCAgent],
                    ResultParser = (result) =>
                        result
                            .GetValue<string>()
                            ?.Contains("yes", StringComparison.OrdinalIgnoreCase) ?? false,
                    HistoryVariableName = "history",
                    MaximumIterations = 10,
                },
                SelectionStrategy = new KernelFunctionSelectionStrategy(
                    selectionFunction,
                    KernelChat
                )
                {
                    AgentsVariableName = "agents",
                    HistoryVariableName = "history",
                },
            },
        };

        Console.WriteLine("Enter your topic to rap about!");
        Console.WriteLine("> ");
        var prompt = Console.ReadLine() ?? "";

        chat.AddChatMessage(
            new Microsoft.SemanticKernel.ChatMessageContent(AuthorRole.User, prompt)
        );
        await foreach (var content in chat.InvokeAsync())
        {
            Console.WriteLine();
            Console.WriteLine(
                $"# {content.Role} - {content.AuthorName ?? "*"}: '{content.Content}'"
            );
            Console.WriteLine();
        }

        Console.WriteLine($"# IS COMPLETE: {chat.IsComplete}");
    }

    public static async Task SpeachToTextChat(AzureAIConfig azureAIConfig)
    {
        var kernel = KernelHelper
            .GetKernelBuilderChatCompletion(azureAIConfig)
            .AddAzureOpenAIAudioToText(
                deploymentName: azureAIConfig.WhisperModelName,
                endpoint: azureAIConfig.Endpoint,
                apiKey: azureAIConfig.ApiKey
            )
            .Build();

        var agent = new ChatCompletionAgent
        {
            Name = "MyAgent",
            Kernel = kernel,
            Instructions = "You are nice AI",
            Arguments = new KernelArguments(
                new AzureOpenAIPromptExecutionSettings { Temperature = 1 }
            ),
        };

        Console.WriteLine("Press any key to start recording mode...");
        Console.ReadKey();
        Console.WriteLine("Listening for your question... Press any key to stop.");
        var waveFormat = new WaveFormat(44100, 1);
        MemoryStream stream = new();
        await using (var waveStream = new WaveFileWriter(stream, waveFormat))
        {
            using var waveIn = new WaveInEvent();
            waveIn.WaveFormat = waveFormat;

            waveIn.DataAvailable += (_, eventArgs) =>
            {
                waveStream.Write(eventArgs.Buffer, 0, eventArgs.BytesRecorded);
            };

            waveIn.StartRecording();
            Console.ReadKey();
        }

        IAudioToTextService audioService = kernel.GetRequiredService<IAudioToTextService>();

        var audioContent = new Microsoft.SemanticKernel.AudioContent(
            stream.ToArray().AsMemory(),
            "audio/wav"
        );
        Microsoft.SemanticKernel.TextContent questionAsText =
            await audioService.GetTextContentAsync(audioContent);
        var question = questionAsText.Text!;
        Console.WriteLine("Question: " + question);

        var history = new ChatHistory();

        history.AddUserMessage(question);

        await foreach (var response in agent.InvokeStreamingAsync(history))
        {
            foreach (var content in response.Content ?? "")
            {
                Console.Write(content);
            }
        }

        Console.WriteLine();
    }

    public static async Task OllamaChat(OllamaAIConfig ollamaAIConfig)
    {
        IChatClient client = new OllamaChatClient(
            new Uri(ollamaAIConfig.Endpoint),
            ollamaAIConfig.ChatModelName
        );

        Console.Write("> ");
        var question = Console.ReadLine() ?? "";

        Console.WriteLine();

        await foreach (var update in client.GetStreamingResponseAsync(question))
        {
            Console.Write(update);
        }
    }

    public static async Task QdrantAILocalRag(
        QdrantClientConfig qdrantClientConfig,
        OllamaAIConfig ollamaAIConfig
    )
    {
        QdrantClient qClient = new(
            host: qdrantClientConfig.Endpoint,
            https: true,
            apiKey: qdrantClientConfig.ApiKey
        );

        OllamaEmbeddingGenerator textEmbeddingGenerator = new(
            new Uri(ollamaAIConfig.Endpoint),
            ollamaAIConfig.TextEmbeddingModelName
        );

        OllamaChatClient chatClient = new(
            new Uri(ollamaAIConfig.Endpoint),
            ollamaAIConfig.ChatModelName
        );

        Console.WriteLine($"Loading data...");
        var zeldaRecords = new List<ZeldaRecord>();

        // Add locations
        string locationsRaw = File.ReadAllText("SampleDocuments/zelda-locations.json");
        zeldaRecords.AddRange(JsonSerializer.Deserialize<List<ZeldaRecord>>(locationsRaw));

        // Add bosses
        string bossesRaw = File.ReadAllText("SampleDocuments/zelda-bosses.json");
        zeldaRecords.AddRange(JsonSerializer.Deserialize<List<ZeldaRecord>>(bossesRaw));

        // Add characters
        string charactersRaw = File.ReadAllText("SampleDocuments/zelda-characters.json");
        zeldaRecords.AddRange(JsonSerializer.Deserialize<List<ZeldaRecord>>(charactersRaw));

        // Add dungeons
        string dungeonsRaw = File.ReadAllText("SampleDocuments/zelda-dungeons.json");
        zeldaRecords.AddRange(JsonSerializer.Deserialize<List<ZeldaRecord>>(dungeonsRaw));

        // Add games
        string gamesRaw = File.ReadAllText("SampleDocuments/zelda-games.json");
        zeldaRecords.AddRange(JsonSerializer.Deserialize<List<ZeldaRecord>>(gamesRaw));

        // Create qdrant collection
        var qdrantRecords = new List<PointStruct>();

        for (int i = 0; i < zeldaRecords.Count; i++)
        {
            var item = zeldaRecords[i];

            // Generate embedding for the record
            item.Embedding = (
                await textEmbeddingGenerator.GenerateAsync([item.Name + ": " + item.Description])
            )[0]
                .Vector.ToArray();

            // Add the record and its embedding to the list for database insertion
            qdrantRecords.Add(
                new PointStruct
                {
                    Id = new PointId((uint)(i + 1)), // Use loop index + 1 as the unique ID
                    Vectors = item.Embedding,
                    Payload = { ["name"] = item.Name, ["description"] = item.Description },
                }
            );
        }

        await qClient.DeleteCollectionAsync("zelda-database");

        // Create the db collection
        await qClient.CreateCollectionAsync(
            "zelda-database",
            new VectorParams { Size = 768, Distance = Distance.Cosine }
        );

        // Insert the records into the database
        await qClient.UpsertAsync("zelda-database", qdrantRecords);

        Console.WriteLine(
            "Ask a question. This bot is grounded in Zelda data due to RAG, so it's good at those topics."
        );

        while (true)
        {
            Console.WriteLine();

            // Create chat history
            List<Microsoft.Extensions.AI.ChatMessage> chatHistory = [];

            // Get user prompt
            var userPrompt = Console.ReadLine();

            //    who are the bosses
            //give me some locations

            // Create an embedding version of the prompt
            var promptEmbedding = (await textEmbeddingGenerator.GenerateAsync([userPrompt]))[0]
                .Vector.ToArray();

            // Run a vector search using the prompt embedding
            var returnedLocations = await qClient.QueryAsync(
                collectionName: "zelda-database",
                query: promptEmbedding,
                limit: 25
            );

            // Use this for grounded chat
            // Add the returned records from the vector search to the prompt
            var builder = new StringBuilder();
            foreach (var location in returnedLocations)
            {
                builder.AppendLine(
                    $"{location.Payload["name"].StringValue}: {location.Payload["description"].StringValue}."
                );
            }

            // Assemble the full prompt to the chat AI model using instructions,
            // the original user prompt, and the retrieved relevant data
            chatHistory.Add(
                new Microsoft.Extensions.AI.ChatMessage(
                    ChatRole.User,
                    @$"Your are an intelligent, cheerful assistant who prioritizes answers to user questions using the data in this conversation.
                If you do not know the answer, say 'I don't know.'.
                Answer the following question:

                [Question]
                {userPrompt}

                Prioritize the following data to answer the question:
                [Data]
                {builder}
    "
                )
            );

            // Stream the AI response and add to chat history
            Console.WriteLine("AI Response:");
            await foreach (var item in chatClient.GetStreamingResponseAsync(chatHistory))
            {
                Console.Write(item.Text);
            }
            Console.WriteLine();
        }
    }

    public static async Task InventoryPlanner(AzureAIConfig azureAIConfig, PlannerType plannerType)
    {
        var KernelChat = KernelHelper.GetKernelChatCompletion(azureAIConfig);

        KernelChat.Plugins.AddFromType<InventoryAgentPlugin>();

        // Provide a natural language goal
        var userGoal =
            "Add 5 iphone 15s and 10 dell laptops to inventory, then remove 2 of those phones and check stock of laptops and phones.";

        Console.WriteLine("User Goal:");
        Console.WriteLine(userGoal);

        if (plannerType == PlannerType.Stepwise)
        {
            var planner = new FunctionCallingStepwisePlanner();
            var result = await planner.ExecuteAsync(KernelChat, userGoal);

            foreach (var line in result.ChatHistory)
            {
                Console.WriteLine(line);
            }

            Console.WriteLine(result.FinalAnswer);
        }
        else if (plannerType == PlannerType.Handlebars)
        {
            var handlebar = new HandlebarsPlanner();
            var handlebarPlan = await handlebar.CreatePlanAsync(KernelChat, userGoal);

            Console.WriteLine(handlebarPlan.Prompt);
            Console.WriteLine(handlebarPlan);

            var handlebarResult = await handlebarPlan.InvokeAsync(KernelChat);

            Console.WriteLine(handlebarResult);
        }
    }

    public static async Task TripPlanner(AzureAIConfig azureAIConfig)
    {
        var KernelChat = KernelHelper.GetKernelChatCompletion(azureAIConfig);

        KernelChat.Plugins.AddFromType<TripPlannerPlugin>(); // <----- This is anew fellow on this Part 3 - TripPlanner. Let's add it to the Kernel
        KernelChat.Plugins.AddFromType<TimeTellerPlugin>(); // <----- This is the same fellow plugin from Part 2
        KernelChat.Plugins.AddFromType<ElectricCarPlugin>(); // <----- This is the same fellow plugin from Part 2
        KernelChat.Plugins.AddFromType<WeatherForecasterPlugin>(); // <----- New plugin. We don't want to end up in beach with rain, right?

        IChatCompletionService chatCompletionService =
            KernelChat.GetRequiredService<IChatCompletionService>();

        ChatHistory chatMessages = new(
            """
You are a friendly assistant who likes to follow the rules. You will complete required steps
and request approval before taking any consequential actions. If the user doesn't provide
enough information for you to complete a task, you will keep asking questions until you have
enough information to complete the task.
"""
        );

        while (true)
        {
            Console.Write("User > ");
            chatMessages.AddUserMessage(Console.ReadLine()!);

            OpenAIPromptExecutionSettings settings = new()
            {
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions,
            };
            var result = chatCompletionService.GetStreamingChatMessageContentsAsync(
                chatMessages,
                executionSettings: settings,
                kernel: KernelChat
            );

            Console.Write("Assistant > ");
            // Stream the results
            string fullMessage = "";
            await foreach (var content in result)
            {
                Console.Write(content.Content);
                fullMessage += content.Content;
            }
            Console.WriteLine("\n--------------------------------------------------------------");

            // Add the message from the agent to the chat history
            chatMessages.AddAssistantMessage(fullMessage);
        }
    }

    public static async Task TransferOrderPlanner(AzureAIConfig azureAIConfig)
    {
        var KernelChat = KernelHelper.GetKernelChatCompletion(azureAIConfig);

        // Initialize the TransferOrderPlugin
        var transferOrderPlugin = new TransferOrderPlugin(KernelChat);

        // Inventory data
        var inventoryData = new Dictionary<string, Dictionary<string, int>>
        {
            {
                "Warehouse A",
                new Dictionary<string, int>
                {
                    { "Item1", 200 },
                    { "Item2", 150 },
                    { "Item3", 50 },
                }
            },
            {
                "Warehouse B",
                new Dictionary<string, int>
                {
                    { "Item1", 50 },
                    { "Item2", 75 },
                    { "Item3", 30 },
                }
            },
            {
                "Store C",
                new Dictionary<string, int>
                {
                    { "Item1", 20 },
                    { "Item2", 10 },
                    { "Item3", 5 },
                }
            },
            {
                "Store D",
                new Dictionary<string, int>
                {
                    { "Item1", 15 },
                    { "Item2", 5 },
                    { "Item3", 10 },
                }
            },
        };

        // Demand data
        var demandData = new Dictionary<string, int>
        {
            { "Item1", 250 },
            { "Item2", 200 },
            { "Item3", 100 },
        };

        // Transportation costs
        var transportCosts = new Dictionary<string, Dictionary<string, double>>
        {
            {
                "Warehouse A",
                new Dictionary<string, double>
                {
                    { "Warehouse B", 20 },
                    { "Store C", 15 },
                    { "Store D", 25 },
                }
            },
            {
                "Warehouse B",
                new Dictionary<string, double>
                {
                    { "Warehouse A", 20 },
                    { "Store C", 10 },
                    { "Store D", 20 },
                }
            },
            {
                "Store C",
                new Dictionary<string, double>
                {
                    { "Warehouse A", 15 },
                    { "Warehouse B", 10 },
                    { "Store D", 5 },
                }
            },
            {
                "Store D",
                new Dictionary<string, double>
                {
                    { "Warehouse A", 25 },
                    { "Warehouse B", 20 },
                    { "Store C", 5 },
                }
            },
        };

        // Generate transfer orders
        var transferOrders = await transferOrderPlugin.GenerateTransferOrders(
            inventoryData,
            demandData,
            transportCosts
        );

        Console.WriteLine(transferOrders);
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

    public static async Task OllamaMemoryLocal(OllamaAIConfig ollamaAIConfig)
    {
        var config = new OllamaConfig
        {
            Endpoint = ollamaAIConfig.Endpoint,
            TextModel = new OllamaModelConfig(ollamaAIConfig.ChatModelName),
            EmbeddingModel = new OllamaModelConfig(ollamaAIConfig.TextEmbeddingModelName),
        };

        var memory = new KernelMemoryBuilder()
            .WithOllamaTextGeneration(config, new GPT4oTokenizer())
            .WithOllamaTextEmbeddingGeneration(config, new GPT4oTokenizer())
            .WithSimpleFileStorage(
                new SimpleFileStorageConfig { StorageType = FileSystemTypes.Disk }
            ) // todo?
            .WithSimpleVectorDb(new SimpleVectorDbConfig { StorageType = FileSystemTypes.Disk }) // todo?
            .Build<MemoryServerless>();

        var facts = new[]
        {
            (
                "timing1",
                "Clinic opens in morning from 10AM to 1PM, Mondays, Tuesdays, Wednesdays, Thursdays, Fridays and Saturdays"
            ),
            (
                "timing2",
                "Clinic opens in evening from 6PM to 8PM, Mondays, Tuesdays and Wednesdays only, for the rest of the week clinic is off in evening"
            ),
            ("timing3", "Clinic is off on Sunday"),
        };

        foreach (var fact in facts)
        {
            if (!await memory.IsDocumentReadyAsync(fact.Item1, index: "clinic"))
            {
                await memory.ImportTextAsync(fact.Item2, documentId: fact.Item1, index: "clinic");
            }
        }

        var answer = memory.AskStreamingAsync("what time is the clinic open?", index: "clinic");
        await foreach (var result in answer)
        {
            Console.Write(result.ToString());
        }
    }

    public static async Task SalesDataAI(AzureAIConfig azureAIConfig)
    {
        var memory = new KernelMemoryBuilder()
            .WithAzureOpenAITextGeneration(
                new AzureOpenAIConfig
                {
                    APIType = AzureOpenAIConfig.APITypes.ChatCompletion,
                    Deployment = azureAIConfig.ChatModelName,
                    Endpoint = azureAIConfig.Endpoint,
                    APIKey = azureAIConfig.ApiKey,
                    Auth = AzureOpenAIConfig.AuthTypes.APIKey,
                }
            )
            .WithAzureOpenAITextEmbeddingGeneration(
                new AzureOpenAIConfig()
                {
                    APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
                    Deployment = azureAIConfig.TextEmbeddingModelName,
                    Endpoint = azureAIConfig.Endpoint,
                    APIKey = azureAIConfig.ApiKey,
                    Auth = AzureOpenAIConfig.AuthTypes.APIKey,
                },
                textTokenizer: new GPT4Tokenizer()
            )
            .WithSimpleFileStorage()
            .WithSimpleVectorDb()
            .Build<MemoryServerless>();

        var salesData = new List<SalesEntry>
        {
            new("Laptop", "2024-02-01", 120, "Sunny"),
            new("Laptop", "2024-02-02", 85, "Rainy"),
            new("Laptop", "2024-02-03", 95, "Cloudy"),
            new("Smartphone", "2024-02-01", 200, "Sunny"),
            new("Smartphone", "2024-02-02", 150, "Rainy"),
            new("Smartphone", "2024-02-03", 180, "Cloudy"),
        };

        foreach (var entry in salesData)
        {
            string dataText =
                $"Product: {entry.Product}, Date: {entry.Date}, Sales: {entry.Sales}, Weather: {entry.Weather}";
            await memory.ImportTextAsync(dataText, index: "sales-data");
        }

        var response = await memory.AskAsync("Laptop sales on Rainy days", index: "sales-data");

        var kernel = KernelHelper.GetKernelChatCompletion(azureAIConfig);

        kernel.Plugins.AddFromType<WeatherPlugin>();

        var prompt =
            @"
            You are an AI supply chain forecaster.
            Given past sales data and weather conditions, predict future demand.

            Relevant Past Sales Data:
            {{$sales_data}}

            Provide a forecast for the next 7 days and suggest restocking strategies. Use the local weather to assist in your prediction.
        ";

        var function = kernel.CreateFunctionFromPrompt(
            prompt,
            new PromptExecutionSettings() { FunctionChoiceBehavior = FunctionChoiceBehavior.Auto() }
        );

        var res = await function.InvokeAsync(
            kernel,
            new KernelArguments { ["sales_data"] = response.Result }
        );

        Console.WriteLine("\n🔮 AI Demand Forecast: \n" + res.GetValue<string>());
    }

    public static async Task GithubInferenceChat(GithubAIConfig githubAIConfig)
    {
        var openAIOptions = new OpenAIClientOptions()
        {
            Endpoint = new Uri("https://models.inference.ai.azure.com"),
        };

        var client = new ChatClient(
            "gpt-4o",
            new ApiKeyCredential(githubAIConfig.Token),
            openAIOptions
        );

        List<OpenAI.Chat.ChatMessage> messages =
        [
            new UserChatMessage("Can you explain the basics of machine learning?"),
        ];

        await foreach (var item in client.CompleteChatStreamingAsync(messages))
        {
            foreach (ChatMessageContentPart contentPart in item.ContentUpdate)
            {
                Console.Write(contentPart.Text);
            }
        }
    }

    public static async Task AzureAITools(AzureAIConfig azureAIConfig)
    {
        AzureOpenAIClient client = new(
            new Uri(azureAIConfig.Endpoint),
            new ApiKeyCredential(azureAIConfig.ApiKey)
        );

        ChatClient chatClient = client.GetChatClient(azureAIConfig.ChatModelName);

        List<OpenAI.Chat.ChatMessage> conversationMessages =
        [
            new SystemChatMessage(
                @"You are an assistant that helps people answer questions using details of the weather in their location. (City and State).
            You are limited to American cities only. Keep your responses clear and concise."
            ),
            new UserChatMessage("weather indy"),
        ];

        ChatTool getWeatherForecastTool = ChatTool.CreateFunctionTool(
            functionName: "GetForecast",
            functionDescription: "Get the current and upcoming weather forecast for a given city and state",
            functionParameters: BinaryData.FromString(
                @"
            {
                ""type"": ""object"",
                ""properties"": {
                    ""city"": {
                        ""type"": ""string"",
                        ""description"": ""The city to get the weather for. eg: Miami or New York""
                    },
                    ""state"": {
                        ""type"": ""string"",
                        ""description"": ""The state the city is in. eg: FL or Florida or NY or New York""
                    }
                },
                ""required"": [ ""city"", ""state"" ]
            }
            "
            )
        );

        ChatCompletionOptions options = new() { Tools = { getWeatherForecastTool } };

        OpenAI.Chat.ChatCompletion completion = await chatClient.CompleteChatAsync(
            conversationMessages,
            options
        );

        if (completion.ToolCalls.Count > 0)
        {
            // This is very important. If you don't add the completion to the conversation messages,
            // OpenAI will not be able to know which tools calls were made and will reject the next prompt.
            conversationMessages.Add(new AssistantChatMessage(completion));

            foreach (var toolCall in completion.ToolCalls)
            {
                if (toolCall.FunctionName == "GetForecast")
                {
                    using JsonDocument argumentsDocument = JsonDocument.Parse(
                        toolCall.FunctionArguments
                    );

                    conversationMessages.Add(
                        new ToolChatMessage(toolCall.Id, "raining cats and dogs") //api call or whatever here
                    );
                }
            }

            OpenAI.Chat.ChatCompletion finalCompletion = await chatClient.CompleteChatAsync(
                conversationMessages,
                options
            );

            Console.WriteLine(finalCompletion.Content.First().Text);
        }
    }
}