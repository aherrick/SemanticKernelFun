namespace SemanticKernelFun.Models;

public class AzureAIConfig
{
    public string Endpoint { get; set; }
    public string ApiKey { get; set; }

    public string ChatModelName { get; set; }
    public string TextEmbeddingModelName { get; set; }
    public string WhisperModelName { get; set; }
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
    public string ChatModelName { get; set; }
    public string TextEmbeddingModelName { get; set; }
}

public class LocalAIConfig
{
    public string PhiModelId { get; set; }
    public string PhiModelPath { get; set; }
    public string BgeModelPath { get; set; }
    public string BgeModelVocabPath { get; set; }
}

public class GithubAIConfig
{
    public string Token { get; set; }
}

public class QdrantClientConfig
{
    public string Endpoint { get; set; }
    public string ApiKey { get; set; }
}