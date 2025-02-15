namespace SemanticKernelFun.Models;

public class SalesEntry(string product, string date, int sales, string weather)
{
    public string Product { get; } = product;
    public string Date { get; } = date;
    public int Sales { get; } = sales;
    public string Weather { get; } = weather;
}