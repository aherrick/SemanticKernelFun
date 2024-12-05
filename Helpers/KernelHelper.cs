#pragma warning disable SKEXP0001, SKEXP0010, SKEXP0020, AOAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

using Azure;
using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Chat;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using MoreRAGFun.Models;
using OpenAI.Chat;
using SemanticKernelFun.Models;

namespace SemanticKernelFun.Helpers;

public static class KernelHelper
{
    public static IKernelMemory GetKernelMemory(
        AzureAIConfig azureAIConfig,
        AzureSearchConfig azureSearchConfig
    )
    {
        return new KernelMemoryBuilder()
            .WithAzureOpenAITextGeneration(
                new AzureOpenAIConfig
                {
                    APIType = AzureOpenAIConfig.APITypes.ChatCompletion,
                    Deployment = azureAIConfig.ChatDeploymentName,
                    Endpoint = azureAIConfig.Endpoint,
                    APIKey = azureAIConfig.ApiKey,
                    Auth = AzureOpenAIConfig.AuthTypes.APIKey
                }
            )
            .WithAzureOpenAITextEmbeddingGeneration(
                new AzureOpenAIConfig()
                {
                    APIType = AzureOpenAIConfig.APITypes.EmbeddingGeneration,
                    Deployment = azureAIConfig.TextEmbeddingDeploymentName,
                    Endpoint = azureAIConfig.Endpoint,
                    APIKey = azureAIConfig.ApiKey,
                    Auth = AzureOpenAIConfig.AuthTypes.APIKey
                },
                textTokenizer: new GPT4Tokenizer()
            )
            .WithAzureAISearchMemoryDb(
                new AzureAISearchConfig()
                {
                    Endpoint = azureSearchConfig.Endpoint,
                    APIKey = azureSearchConfig.ApiKey,
                    Auth = AzureAISearchConfig.AuthTypes.APIKey,
                    UseHybridSearch = true
                }
            )
            .WithSearchClientConfig(
                new SearchClientConfig
                {
                    MaxMatchesCount = 2,
                    Temperature = 0,
                    TopP = 0
                }
            )
            // TODO look into:

            //        .WithSearchClientConfig(
            //    new()
            //    {
            //        EmptyAnswer =
            //            "I'm sorry, I haven't found any relevant information that can be used to answer your question",
            //        MaxMatchesCount = 25,
            //        AnswerTokens = 800
            //    }
            //)
            //.WithCustomTextPartitioningOptions(
            //    new()
            //    {
            //        MaxTokensPerParagraph = 1000,
            //        MaxTokensPerLine = 300,
            //        OverlappingTokens = 100
            //    }
            //)
            //// Customize the pipeline to automatically delete files generated during the ingestion process.
            ////.With(new KernelMemoryConfig
            ////{
            ////    DataIngestion = new KernelMemoryConfig.DataIngestionConfig
            ////    {
            ////        //MemoryDbUpsertBatchSize = 32,
            ////        DefaultSteps = [.. Constants.DefaultPipeline, Constants.PipelineStepsDeleteGeneratedFiles]
            ////    }
            ////})
            //.WithSimpleFileStorage(Path.Combine(Directory.GetCurrentDirectory(), "dbs", "fs"))
            //.WithSimpleVectorDb(Path.Combine(Directory.GetCurrentDirectory(), "dbs", "vdb"))
            // Configure the asynchronous memory.
            // .WithSimpleQueuesPipeline(Path.Combine(Directory.GetCurrentDirectory(), "dbs", "qp"))

            .Build<MemoryServerless>();
    }

    public static Kernel GetKernelChatCompletion(AzureAIConfig azureAIConfig)
    {
        return GetKernelBuilderChatCompletion(azureAIConfig).Build();
    }

    private static IKernelBuilder GetKernelBuilderChatCompletion(AzureAIConfig azureAIConfig)
    {
        return Kernel
            .CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: azureAIConfig.ChatDeploymentName,
                endpoint: azureAIConfig.Endpoint,
                apiKey: azureAIConfig.ApiKey
            );
    }

    public static Kernel GetKernelVectorStore(
        AzureAIConfig azureAIConfig,
        AzureSearchConfig azureSearchConfig
    )
    {
        var kernelBuilder = GetKernelBuilderChatCompletion(azureAIConfig);

        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
            deploymentName: azureAIConfig.TextEmbeddingDeploymentName,
            endpoint: azureAIConfig.Endpoint,
            apiKey: azureAIConfig.ApiKey
        );

        kernelBuilder.AddAzureAISearchVectorStoreRecordCollection<TextSnippet<string>>(
            azureSearchConfig.Index,
            new Uri(azureSearchConfig.Endpoint),
            new AzureKeyCredential(azureSearchConfig.ApiKey)
        );

        kernelBuilder.AddVectorStoreTextSearch<TextSnippet<string>>(
            new TextSearchStringMapper((result) => (result as TextSnippet<string>).Text),
            new TextSearchResultMapper(
                (result) =>
                {
                    // Create a mapping from the Vector Store data type to the data type returned by the Text Search.
                    // This text search will ultimately be used in a plugin and this TextSearchResult will be returned to the prompt template
                    // when the plugin is invoked from the prompt template.
                    var castResult = result as TextSnippet<string>;
                    return new TextSearchResult(value: castResult.Text)
                    {
                        Name = castResult.ReferenceDescription,
                        Link = castResult.ReferenceLink
                    };
                }
            )
        );

        var kernel = kernelBuilder.Build();

        return kernel;
    }

    public static AzureSearchChatDataSource GetAzureSearchChatDataSource(
        AzureSearchConfig azureSearchConfig
    )
    {
        return new AzureSearchChatDataSource()
        {
            Endpoint = new Uri(azureSearchConfig.Endpoint),
            IndexName = azureSearchConfig.Index,
            Authentication = DataSourceAuthentication.FromApiKey(azureSearchConfig.ApiKey),
            InScope = true
        };
    }

    public static ChatClient GetAzureOpenAIClient(AzureAIConfig azureAIConfig)
    {
        var azureClient = new AzureOpenAIClient(
            new Uri(azureAIConfig.Endpoint),
            new System.ClientModel.ApiKeyCredential(azureAIConfig.ApiKey)
        );

        return azureClient.GetChatClient(azureAIConfig.ChatDeploymentName);
    }
}