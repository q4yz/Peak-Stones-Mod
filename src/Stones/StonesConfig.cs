using BepInEx.Configuration;

namespace Stones;


public enum LogLevel
{
    Debug = 0,
    Info = 1,
    Warning = 2,
    Error = 3,
    None = 4 // Use this to completely disable logging
}

/// <summary>
/// Centralized configuration for the Stones mod. All <see cref="ConfigEntry{T}"/>
/// properties live here and are populated by <see cref="Bind"/>, which is
/// called from <see cref="Plugin.Awake"/>.
/// </summary>
public static class StonesConfig
{
    public static ConfigEntry<bool> EnableDebugLogging{ get; private set; } = null!;
    
    public static ConfigEntry<bool> EnableDebug{ get; private set; } = null!;
    
    public static ConfigEntry<LogLevel> MinLogLevel{ get; private set; } = null!;
    public static ConfigEntry<int> MaxStones { get; private set; } = null!;
    
    public static ConfigEntry<bool> EnableGrenades { get; private set; } = null!;


    // --- 2. Events ---
    public static ConfigEntry<int> VolcanoMaxStones { get; private set; } = null!;
    public static ConfigEntry<bool> EnableVolcanoEvent { get; private set; } = null!;
    public static ConfigEntry<float> VulcanOutbreakChance { get; private set; } = null!;
    public static ConfigEntry<int> VulcanStoneBurstCount { get; private set; } = null!;

    public static ConfigEntry<float> StoneRainDropRate { get; private set; } = null!;

   
    
    public static void Bind(ConfigFile config)
    {
        
        EnableDebugLogging = config.Bind(
            "Debug", "EnableLogging", true, 
            "If true, enables detailed debug logging in the console.");
        
        
        EnableDebug = config.Bind(
            "Debug", "EnableDebug", true, 
            "If true, enables debug commands.");
        
        MinLogLevel = config.Bind(
            "Debug", "MinimumLogLevel", LogLevel.None,
            "Minimum severity level of logs to display in the console. Options: Debug, Info, Warning, Error, None.");
        
        MaxStones = config.Bind(
            "1. Spawning", "MaxStones", 700,
            "The maximum number of items allowed in the world.");

        EnableVolcanoEvent = config.Bind(
            "2. Events", "EnableVolcanoEvent", true,
            "Set to true to allow the volcanic outbreak hijack.");
        
        VolcanoMaxStones = config.Bind(
            "2. Events", "VolcanoMaxStones", 30,
            "The maximum number of items allowed in a volcanic outbreak.");
        
        VulcanOutbreakChance = config.Bind(
            "2. Events", "VulcanOutbreakChance", 1.0f,
            "Chance for a normal storm start to become a volcanic outbreak.");
        
        VulcanStoneBurstCount = config.Bind(
            "2. Events", "VulcanStoneBurstCount", 5,
            "How many stones to burst into the sky when the outbreak starts.");
        
        StoneRainDropRate = config.Bind(
            "2. Events", "StoneRainDropRate", 2f,
            "Legacy setting kept for compatibility with the old coroutine path.");
        
        EnableGrenades = config.Bind(
            "1. Spawning", "EnableGrenades", true, 
            "If true, grenades are allowed to spawn in chests/world.");
       
    }
}
