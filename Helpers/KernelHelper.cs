using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.KernelMemory;
using Microsoft.KernelMemory.AI.OpenAI;
using Microsoft.SemanticKernel;
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
        return Kernel
            .CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: azureAIConfig.ChatDeploymentName,
                endpoint: azureAIConfig.Endpoint,
                apiKey: azureAIConfig.ApiKey
            )
            .Build();
    }
}