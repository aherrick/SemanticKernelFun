using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SemanticKernelFun.Plugins.InventoryPlanner;

public class InventoryAgentPlugin
{
    // Mock repository
    private readonly Dictionary<string, int> _inventory = new();

    [KernelFunction("AddItem")]
    [Description("Add items to the inventory")]
    public string AddItem([Description("Item Name non pluralized")] string item, int quantity)
    {
        if (!_inventory.ContainsKey(item))
        {
            _inventory[item] = 0; // Initialize if not exists
        }

        _inventory[item] += quantity;

        return $"[AddItem] Added {quantity} of {item}. Total: {_inventory[item]}";
    }

    [KernelFunction("RemoveItem")]
    [Description("Remove items from the inventory")]
    public string RemoveItem([Description("Item Name non pluralized")] string item, int quantity)
    {
        if (!_inventory.TryGetValue(item, out int value))
        {
            return $"[RemoveItem] {item} not found in inventory.";
        }

        // Avoid negative stock
        if (value < quantity)
        {
            return $"[RemoveItem] Cannot remove {quantity} of {item}. Only {value} units available.";
        }

        _inventory[item] -= quantity;

        return $"[RemoveItem] Removed {quantity} of {item}. Remaining: {_inventory[item]}";
    }

    [KernelFunction("CheckStock")]
    [Description("Check the inventory for a given item")]
    public string CheckStock([Description("Item Name non pluralized")] string item)
    {
        if (_inventory.TryGetValue(item, out var quantity))
        {
            return $"[CheckStock] {item} has {quantity} units in stock.";
        }

        return $"[CheckStock] {item} not found in inventory.";
    }
}