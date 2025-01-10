# SemanticKernelFun


![dotnet](https://github.com/aherrick/SemanticKernelFun/actions/workflows/dotnet.yml/badge.svg)

 User Secrets:
 
 ```

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

Local Phi Model:
```
git clone https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-onnx
```
Local Bge Model:
```
git clone https://huggingface.co/TaylorAI/bge-micro-v2
```