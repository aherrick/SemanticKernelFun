using Polly;
using Polly.Retry;

namespace SemanticKernelFun.Helpers;

public static class PollyHelper
{
    public static ResiliencePipeline Retry()
    {
        var retryOptions = new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            BackoffType = DelayBackoffType.Exponential,
            Delay = TimeSpan.FromSeconds(2),
            DelayGenerator = async (arg) =>
            {
                Console.WriteLine(arg);

                return await Task.FromResult(TimeSpan.FromSeconds(10)); //
            },
            UseJitter = true,
            OnRetry = args =>
            {
                Console.WriteLine(
                    $"OnRetry, Attempt: {args.AttemptNumber + 1}, Delay: {args.RetryDelay}"
                );
                return default;
            }
        };

        return new ResiliencePipelineBuilder().AddRetry(retryOptions).Build();
    }
}