# SemanticKernelFun

[![Build Status](https://github.com/aherrick/SemanticKernelFun/actions/workflows/dotnet.yml/badge.svg)](https://github.com/aherrick/SemanticKernelFun/actions/workflows/dotnet.yml)
[![License](https://img.shields.io/github/license/aherrick/SemanticKernelFun)](LICENSE)

A playground for experimenting with [Microsoft Semantic Kernel](https://github.com/microsoft/semantic-kernel), featuring AI agents, plugins, and various AI service integrations.

## 🚀 Features

- **Multiple AI Provider Support**: Azure OpenAI, Ollama, GitHub Models, and Local ONNX models
- **Semantic Kernel Plugins**: Custom plugins for trip planning, inventory management, transfer orders, and more
- **Vector Search Integration**: Azure AI Search and Qdrant support
- **AI Agents**: Pre-built agents for various tasks and workflows
- **Resilience Patterns**: Polly integration for robust API calls

## 📋 Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or higher
- An AI service provider (Azure OpenAI, Ollama, or GitHub Models)
- Optional: Azure AI Search or Qdrant for vector search capabilities

## ⚙️ Configuration

### User Secrets Setup

This project uses .NET User Secrets to store sensitive configuration. Set up your secrets using:

```bash
dotnet user-secrets init
```

Then configure your AI services:

```json
{
  "AzureAIConfig": {
    "Endpoint": "https://YOUR-AI.openai.azure.com/",
    "ApiKey": "",
    "ChatModelName": "gpt-4o",
    "TextEmbeddingModelName": "text-embedding-ada-002",
    "WhisperModelName": "whisper"
  },
  "AzureSearchConfig": {
    "Index": "default",
    "Endpoint": "https://YOUR-AI-SEARCH.search.windows.net",
    "ApiKey": ""
  },
  "OllamaAIConfig": {
    "Endpoint": "http://localhost:11434/",
    "ChatModelName": "llama3.3",
    "TextEmbeddingModelName": "nomic-embed-text"
  },
  "GithubAIConfig": {
    "Token": ""
  },
  "LocalAIConfig": {
    "PhiModelId": "Phi-3-mini-4k-instruct-onnx",
    "PhiModelPath": "c:\\models\\Phi-3-mini-4k-instruct-onnx\\cpu_and_mobile\\cpu-int4-rtn-block-32",
    "BgeModelPath": "c:\\models\\bge-micro-v2\\onnx\\model.onnx",
    "BgeModelVocabPath": "c:\\models\\bge-micro-v2\\vocab.txt"
  },
  "QdrantClientConfig": {
    "Endpoint": "YOUR-AI.cloud.qdrant.io",
    "ApiKey": ""
  }
}
```

### Local Models (Optional)

If you want to use local ONNX models, download them using:

**Phi-3 Model:**
```bash
git clone https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx
```

**BGE Embedding Model:**
```bash
git clone https://huggingface.co/TaylorAI/bge-micro-v2
```

## 🏃 Getting Started

1. **Clone the repository**
   ```bash
   git clone https://github.com/aherrick/SemanticKernelFun.git
   cd SemanticKernelFun
   ```

2. **Configure your secrets** (see Configuration section above)

3. **Build and run**
   ```bash
   dotnet build
   dotnet run
   ```

## 🤝 Contributing

Contributions are welcome! Feel free to submit issues and pull requests.

## 📄 License

This project is licensed under the terms specified in the [LICENSE](LICENSE) file.

## 🔗 Resources

- [Semantic Kernel Documentation](https://learn.microsoft.com/en-us/semantic-kernel/)
- [Azure OpenAI Service](https://azure.microsoft.com/en-us/products/ai-services/openai-service)
- [Ollama](https://ollama.ai/)
- [Polly Resilience Library](https://github.com/App-vNext/Polly)