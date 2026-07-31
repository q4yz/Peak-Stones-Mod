namespace Stones;

public static class ModLogger
{
    
    private static bool ShouldLog(LogLevel level)
    {
        if (!StonesConfig.EnableDebugLogging.Value)
            return false;

        return StonesConfig.MinLogLevel.Value <= level;
    }
    
    public static void LogDebug(object message)
    {
        if (StonesConfig.MinLogLevel.Value <= LogLevel.Debug)
        {
            Plugin.logger.LogDebug(message);
        }
    }

    public static void LogInfo(object message)
    {
        if ( StonesConfig.MinLogLevel.Value <= LogLevel.Info)
        {
            Plugin.logger.LogInfo(message);
        }
    }

    public static void LogWarning(object message)
    {
        if (StonesConfig.MinLogLevel.Value <= LogLevel.Warning)
        {
            Plugin.logger.LogWarning(message);
        }
    }

    public static void LogError(object message)
    {
        if (StonesConfig.MinLogLevel.Value <= LogLevel.Error)
        {
            Plugin.logger.LogError(message);
        }
    }
}