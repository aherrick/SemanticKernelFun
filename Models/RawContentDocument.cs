namespace SemanticKernelFun.Models;

public class RawContentDocument
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public string FileName { get; set; }
    public string Text { get; set; }
}