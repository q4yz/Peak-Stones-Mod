using System.Collections.Generic;

namespace Stones;

/// <summary> 
/// Injects the Stones mod display names into the game's localization
/// table at runtime. Called once during <see cref="Plugin.Awake"/>
/// (see <see cref="CILocalization"/>).
/// </summary>
public static class Localization
{
    private const int LocaleCount = 13;

    private const string KeyPebble       = "NAME_PEBBLE";
    private const string KeyRock         = "NAME_ROCK";
    private const string KeyBoulder      = "NAME_BOULDER";
    private const string KeyLargeBoulder = "NAME_100-POUNDER";
    private const string KeyImpactGrenade = "NAME_IMPACT GRENADE";
    
    public static void CILocalization()
    {
        AddEntry(KeyPebble,
            "Pebble",  "Pebble",  "Pebble",  "Pebble",
            "Pebble",  "Pebble",  "Pebble",  "Pebble",
            "Pebble",  "Pebble",  "Pebble",  "Pebble", "Pebble");

        AddEntry(KeyRock,
            "Rock",    "Rock",    "Rock",    "Rock",
            "Rock",    "Rock",    "Rock",    "Rock",
            "Rock",    "Rock",    "Rock",    "Rock",    "Rock");

        AddEntry(KeyBoulder,
            "Boulder", "Boulder", "Boulder", "Boulder",
            "Boulder", "Boulder", "Boulder", "Boulder",
            "Boulder", "Boulder", "Boulder", "Boulder", "Boulder");

        AddEntry(KeyLargeBoulder,
            "100-Pounder", "100-Pounder", "100-Pounder", "100-Pounder",
            "100-Pounder", "100-Pounder", "100-Pounder", "100-Pounder",
            "100-Pounder", "100-Pounder", "100-Pounder", "100-Pounder", "100-Pounder");
        
        AddEntry(KeyImpactGrenade, 
            "Impact Grenade","Impact Grenade","Impact Grenade","Impact Grenade",
            "Impact Grenade","Impact Grenade","Impact Grenade","Impact Grenade",
            "Impact Grenade","Impact Grenade","Impact Grenade","Impact Grenade","Impact Grenade");
 
        Plugin.logger.LogInfo(
            "[Stones] Localization injection complete " +
            $"(keys: {KeyPebble}, {KeyRock}, {KeyBoulder}, {KeyLargeBoulder}).");
    }
    
    private static void AddEntry(string key, params string[] localeStrings)
    {
        if (LocalizedText.mainTable.ContainsKey(key)) return;

        if (localeStrings.Length != LocaleCount)
        {
            ModLogger.LogError(
                $"[Stones] Localization for '{key}' has " +
                $"{localeStrings.Length} entries, expected {LocaleCount}. " +
                "Skipped to avoid corrupting the locale table.");
            return;
        }

        LocalizedText.mainTable.Add(key, new List<string>(localeStrings));
    }
}