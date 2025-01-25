using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace SemanticKernelFun.Plugins.TripPlanner;

public class WeatherForecasterPlugin // <------------ Weather plugin. An expert on weather. Can tell the weather at a given destination
{
    [KernelFunction]
    [Description("This function retrieves weather at given destination.")]
    [return: Description("Weather at given destination.")]
    public string GetTodaysWeather(
        [Description("The destination to retrieve the weather for.")] string destination
    )
    {
        // <--------- This is where you would call a fancy weather API to get the weather for the given <<destination>>.
        // We are just simulating a random weather here.
        string[] weatherPatterns = { "Sunny", "Cloudy", "Windy", "Rainy", "Snowy" };
        Random rand = new Random();
        return weatherPatterns[rand.Next(weatherPatterns.Length)];
    }
}