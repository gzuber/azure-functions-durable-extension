#r "Microsoft.Azure.WebJobs.Extensions.DurableTask"
#r "Microsoft.Extensions.Logging"

#load "..\shared\MonitorRequest.csx"

using System.Threading;

public static async Task Run(IDurableOrchestrationContext monitorContext, ILogger log)
{
    MonitorRequest input = monitorContext.GetInput<MonitorRequest>();
    if (!monitorContext.IsReplaying) { log.LogInformation($"Received monitor request. Location: {input?.Location}. Phone: {input?.Phone}."); }

    VerifyRequest(input);

    DateTime endTime = monitorContext.CurrentUtcDateTime.AddHours(6);
    if (!monitorContext.IsReplaying) { log.LogInformation($"Instantiating monitor for {input.Location}. Expires: {endTime}."); }

    while (monitorContext.CurrentUtcDateTime < endTime)
    {
        // Check the weather
        if (!monitorContext.IsReplaying) { log.LogInformation($"Checking current weather conditions for {input.Location} at {monitorContext.CurrentUtcDateTime}."); }

        bool isClear = await monitorContext.CallActivityAsync<bool>("E3_GetIsClear", input.Location);

        if (isClear)
        {
            // It's not raining! Or snowing. Or misting. Tell our user to take advantage of it.
            if (!monitorContext.IsReplaying) { log.LogInformation($"Detected clear weather for {input.Location}. Notifying {input.Phone}."); }

            await monitorContext.CallActivityAsync("E3_SendGoodWeatherAlert", input.Phone);
            break;
        }
        else
        {
            // Wait for the next checkpoint
            var nextCheckpoint = monitorContext.CurrentUtcDateTime.AddMinutes(30);
            if (!monitorContext.IsReplaying) { log.LogInformation($"Next check for {input.Location} at {nextCheckpoint}."); }

            await monitorContext.CreateTimer(nextCheckpoint, CancellationToken.None);
        }
    }

    log.LogInformation("Monitor expiring.");
}

private static void VerifyRequest(MonitorRequest request)
{
    if (request == null)
    {
        throw new ArgumentNullException(nameof(request), "An input object is required.");
    }

    if (request.Location == null)
    {
        throw new ArgumentNullException(nameof(request.Location), "A location input is required.");
    }

    if (string.IsNullOrEmpty(request.Phone))
    {
        throw new ArgumentNullException(nameof(request.Phone), "A phone number input is required.");
    }
}