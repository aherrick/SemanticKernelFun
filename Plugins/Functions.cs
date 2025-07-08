using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SemanticKernelFun.Plugins;

// PLUGINS
public class TicketPlugin
{
    [KernelFunction, Description("Creates a support ticket for unresolved issues")]
    public string CreateTicket(string issue, string department) =>
        $"📩 Ticket created in {department} for '{issue}' (ID: {Guid.NewGuid()})";
}

public class KnowledgePlugin
{
    [KernelFunction, Description("Returns the company policy for a topic and department")]
    public string GetPolicy(string topic, string department) =>
        $"📚 Policy for '{topic}' in {department}: [Sample policy text here]";
}