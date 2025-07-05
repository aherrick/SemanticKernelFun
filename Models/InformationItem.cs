using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Data;

namespace SemanticKernelFun.Models;

/// <summary>
/// Information item to represent the embedding data stored in the memory
/// </summary>
internal sealed class InformationItem
{
    [VectorStoreKey]
    [TextSearchResultName]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    [TextSearchResultValue]
    public string Text { get; set; } = string.Empty;

    [VectorStoreVector(Dimensions: 384)]
    public string Embedding => this.Text;
}