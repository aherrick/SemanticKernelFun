using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SemanticKernelFun.Plugins.TripPlanner;

public class TimeTellerPlugin // <------------ Time teller plugin. An expert on time, peak and off-peak periods
{
    [KernelFunction]
    [Description("This function retrieves the current time.")]
    [return: Description("The current time.")]
    public string GetCurrentTime() => DateTime.Now.ToString("F");

    [KernelFunction]
    [Description("This function checks if the current time is off-peak.")]
    [return: Description("True if the current time is off-peak; otherwise, false.")]
    public bool IsOffPeak() => DateTime.Now.Hour < 7 || DateTime.Now.Hour >= 21;
}