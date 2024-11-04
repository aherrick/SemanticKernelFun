using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SemanticKernelFun.Helpers;

public class AzureAIConfig
{
    public string Endpoint { get; set; }
    public string ApiKey { get; set; }

    public string ChatDeploymentName { get; set; }
    public string TextEmbeddingDeploymentName { get; set; }
}

public class AzureSearchConfig
{
    public string Index { get; set; }

    public string Endpoint { get; set; }
    public string ApiKey { get; set; }
}