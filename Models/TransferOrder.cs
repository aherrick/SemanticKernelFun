namespace SemanticKernelFun.Models;

public class TransferOrder
{
    public string Source { get; set; }
    public string Destination { get; set; }
    public string Item { get; set; }
    public int Quantity { get; set; }
    public double Cost { get; set; }
}