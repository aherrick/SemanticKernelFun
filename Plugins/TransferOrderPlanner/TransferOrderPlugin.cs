using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace SemanticKernelFun.Plugins.TransferOrderPlanner;

public class TransferOrderPlugin(Kernel kernel)
{
    public async Task<string> GenerateTransferOrders(
        Dictionary<string, Dictionary<string, int>> inventoryData,
        Dictionary<string, int> demandData,
        Dictionary<string, Dictionary<string, double>> transportCosts
    )
    {
        // Create summaries for inventory, demand, and transportation costs
        var inventorySummary = string.Join(
            "\n",
            inventoryData.Select(location =>
                $"{location.Key}: {string.Join(", ", location.Value.Select(item => $"{item.Key} ({item.Value})"))}"
            )
        );

        var demandSummary = string.Join(
            "\n",
            demandData.Select(item => $"{item.Key}: {item.Value}")
        );

        var costSummary = string.Join(
            "\n",
            transportCosts.Select(from =>
                $"{from.Key}: {string.Join(", ", from.Value.Select(to => $"{to.Key} ({to.Value})"))}"
            )
        );

        // Prompt the AI to generate transfer orders in JSON format
        var prompt =
            $@"
                The current inventory levels across locations are as follows:
                {inventorySummary}

                The current demand forecast for items is as follows:
                {demandSummary}

                The transportation costs between locations are as follows:
                {costSummary}

                Based on the inventory, demand, and transportation costs, suggest transfer orders to balance stock and minimize overall costs.
                Return the result in a JSON array with the following structure:
                [
                    {{
                        ""source"": ""SourceLocation"",
                        ""destination"": ""DestinationLocation"",
                        ""item"": ""Item"",
                        ""quantity"": Quantity,
                        ""cost"": TransportationCost
                    }}
                ]
                ";

        var completion = await kernel
            .GetRequiredService<IChatCompletionService>()
            .GetChatMessageContentAsync(prompt);

        return completion.ToString();
    }
}