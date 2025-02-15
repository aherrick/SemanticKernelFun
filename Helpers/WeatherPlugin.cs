using System.Text;
using Microsoft.SemanticKernel;

namespace SemanticKernelFun.Helpers;

public class WeatherPlugin
{
    private static readonly string[] WeatherConditions = { "Sunny", "Rainy", "Cloudy", "Snowy" };
    private static readonly Random RandomGenerator = new();

    [KernelFunction("GetNext7DaysForecast")]
    public static Task<string> GetNext7DaysForecastAsync()
    {
        StringBuilder forecast = new();
        forecast.AppendLine("📅 **7-Day Weather Forecast**:");

        for (int i = 0; i < 7; i++)
        {
            DateTime day = DateTime.UtcNow.AddDays(i);
            string condition = WeatherConditions[RandomGenerator.Next(WeatherConditions.Length)];
            forecast.AppendLine($"- {day:dddd, MMM dd}: {condition}");
        }

        return Task.FromResult(forecast.ToString());
    }
}