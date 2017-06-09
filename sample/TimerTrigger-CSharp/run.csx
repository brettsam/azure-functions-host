public static async Task Run(TimerInfo timerInfo, TraceWriter log)
{
    log.Info("C# Timer trigger function executed.");
    await Task.Delay(45000);
}