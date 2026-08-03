using BepInEx.Configuration;

namespace Stones;


public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
}

/// <summary>
/// Centralized configuration for the Stones mod. All <see cref="ConfigEntry{T}"/>
/// properties live here and are populated by <see cref="Bind"/>, which is
/// called from <see cref="Plugin.Awake"/>.
/// </summary>
public static class StonesConfig
{
    public static ConfigEntry<LogLevel> MinLogLevel{ get; private set; } = null!;
    
    public static ConfigEntry<int> MaxStones { get; private set; } = null!;
    public static ConfigEntry<bool> EnableGrenades { get; private set; } = null!;

    
    public static ConfigEntry<int> VolcanoMaxStones { get; private set; } = null!;
    public static ConfigEntry<bool> EnableVolcanoEvent { get; private set; } = null!;
    public static ConfigEntry<float> VulcanOutbreakChance { get; private set; } = null!;
    public static ConfigEntry<int> VulcanStoneBurstCount { get; private set; } = null!;
    public static ConfigEntry<float> StoneRainDropRate { get; private set; } = null!;

    
    public static void Bind(ConfigFile config)
    {
        
        MaxStones = config.Bind(
            "1. Spawning", "Max Stones", 700,
            "The maximum number of items allowed in the world.");

        EnableVolcanoEvent = config.Bind(
            "2. Events", "Enable Volcano Event", true,
            "Set to true to allow the volcanic outbreak hijack.");
        
        VolcanoMaxStones = config.Bind(
            "2. Events", "Volcano Max Stones", 30,
            "The maximum number of items allowed in a volcanic outbreak.");
        
        VulcanOutbreakChance = config.Bind(
            "2. Events", "Vulcan Outbreak Chance", 1.0f,
            new ConfigDescription(
                "Chance for a normal storm start to become a volcanic outbreak. (0.0 = 0%, 0.5 = 50%, 1.0 = 100%)", 
                new AcceptableValueRange<float>(0.0f, 1.0f)
            ));
        
        VulcanStoneBurstCount = config.Bind(
            "2. Events", "Vulcan Stone Burst Count", 5,
            "How many stones to burst into the sky when the outbreak starts.");
        
        StoneRainDropRate = config.Bind(
            "2. Events", "Stone Rain Drop Rate", 2f,
            "Legacy setting kept for compatibility with the old coroutine path.");
        
        EnableGrenades = config.Bind(
            "1. Spawning", "Enable Grenades", true, 
            "If true, grenades are allowed to spawn in chests/world.");
        
        MinLogLevel = config.Bind(
            "Logging", "Minimum Log Level", LogLevel.Error,
            "Minimum severity level of logs to display in the console. Options: Debug, Info, Warning, Error, None.");
       
    }
}
