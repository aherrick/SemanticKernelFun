﻿namespace SemanticKernelFun.Models;

public class AzureAIConfig
{
    public string Endpoint { get; set; }
    public string ApiKey { get; set; }

    public string ChatDeploymentName { get; set; }
    public string TextEmbeddingDeploymentName { get; set; }
    public string WhisperDeploymentName { get; set; }
}

public class AzureSearchConfig
{
    public string Index { get; set; }

    public string Endpoint { get; set; }
    public string ApiKey { get; set; }
}

public class OllamaAIConfig
{
    public string Endpoint { get; set; }
    public string ModelName { get; set; }
}

public class LocalAIConfig
{
    public string PhiModelId { get; set; }
    public string PhiModelPath { get; set; }
    public string BgeModelPath { get; set; }
    public string BgeModelVocabPath { get; set; }
}