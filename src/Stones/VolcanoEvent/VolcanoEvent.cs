using System.Collections;
using Photon.Pun;
using UnityEngine;
using ExitGames.Client.Photon;

namespace Stones;

/// <summary>
/// Coroutine runner for the Volcano Event weather storm. Yields back to the
/// host MonoBehaviour between phases so the caller can chain post-event
/// cleanup (e.g. resetting <c>isRaining</c>) once the iterator completes.
/// </summary>
public static class VolcanoEvent
{
    // --- FADE TIMINGS ---
    private const float FadeInDuration  = 5f;
    private const float FadeOutDuration = 5f;

    // --- SHAKE + BUILDUP ---
    private const float ShakeDuration  = 2f;
    private const float ShakeMagnitude = 2f;
    private const float BuildupDelay   = 5f;

    // --- RAIN ---
    private const float RainRadiusXZ    = 10f;
    private const float RainHeightOffset = 70f;

    // --- STORM TARGET VALUES ---
    private static readonly Color StormAmbient      = new Color(0.35f, 0.08f, 0.08f, 1f);
    private static readonly Color StormFogColor     = new Color(0.45f, 0.05f, 0.05f, 1f);
    private const  float         StormFogDensity   = 0.16f;
    private static readonly Color StormSunColor     = new Color(1.0f,  0.45f, 0.15f, 1f);
    private const  float         StormSunIntensity = 1.5f;

    private static VolcanoVisuals? visualEnforcer;

    private struct AtmosphereState
    {
        public Color OriginalAmbient;
        public Color OriginalFogColor;
        public float OriginalFogDensity;
        public Color OriginalSunColor;
        public float OriginalSunIntensity;
        public bool  OriginalFog;
    }

    public static IEnumerator Run()
    {
        float startTime = Time.time;
        Plugin.logger.LogInfo($"[Volcano] === EVENT START === Current Time: {startTime:F2}");

        // 1. SPATIAL ENFORCER CREATION
        Plugin.logger.LogInfo("[Volcano] Creating VolcanoVisualEnforcer GameObject...");
        GameObject enforcerObject = new GameObject("VolcanoVisualEnforcer");
        visualEnforcer = enforcerObject.AddComponent<VolcanoVisuals>();
        
        Plugin.logger.LogInfo("[Volcano] Setting up AudioSource for Au_Fire_Loop...");
        AudioSource fireAudio = enforcerObject.AddComponent<AudioSource>();
        fireAudio.spatialBlend = 0f; // 2D sound, heard globally
        fireAudio.loop = true;
        fireAudio.volume = 0.1f;     // Adjust volume as needed
        
        AudioSource explosionAudio = enforcerObject.AddComponent<AudioSource>();
        explosionAudio.spatialBlend = 0f; // 2D sound, heard globally
        explosionAudio.loop = false;
        explosionAudio.volume = 0.4f;     // Adjust volume as needed

        // Load the clip directly from your mod's PEAK bundle.
        // Ensure 'Plugin.peakBundle' points to your loaded bundle instance.
        AudioClip fireClip = Plugin.peakBundle.LoadAsset<AudioClip>("Au_Fire_Loop");
        AudioClip explosionClip = Plugin.peakBundle.LoadAsset<AudioClip>("Au_Explosion_Debris");
        
        if (fireClip != null)
        {
            fireAudio.clip = fireClip;
            fireAudio.Play();
            Plugin.logger.LogInfo("[Volcano] Successfully loaded and playing Au_Fire_Loop from bundle.");
        }
        if (explosionClip != null)
        {
            explosionAudio.clip = explosionClip;
            explosionAudio.Play();
            Plugin.logger.LogInfo("[Volcano] Successfully loaded and playing Au_Explosion_Debris from bundle.");
        }
       
        
        Light? sun = FindMainDirectionalLight();
        visualEnforcer.sun = sun;
        visualEnforcer.enforceEnvironment = true;

        Plugin.logger.LogInfo($"[Volcano] Enforcer attached and initialized. Sun reference: {(sun != null ? sun.name : "null")}");

        AtmosphereState state = CaptureAtmosphere(sun);
        Plugin.logger.LogInfo($"[Volcano] Captured original atmosphere state (Fog enabled: {state.OriginalFog}, Fog Density: {state.OriginalFogDensity:F3})");

        // --- 1. FADE IN ---
        Plugin.logger.LogInfo($"[Volcano] Phase 1 (Fade In) starting. Expected duration: {FadeInDuration}s");
        yield return FadeEnvironment(sun, StormAmbient, StormFogColor, StormFogDensity, StormSunColor, StormSunIntensity, FadeInDuration);
        Plugin.logger.LogInfo($"[Volcano] Phase 1 completed. (Elapsed: {Time.time - startTime:F2}s)");

        // --- 2. CAMERA SHAKE ---
        float shakeStart = Time.time;
        Plugin.logger.LogInfo($"[Volcano] Phase 2 (Camera Shake) starting. Duration: {ShakeDuration}s, Magnitude: {ShakeMagnitude}");
        yield return ShakeCamera(ShakeDuration, ShakeMagnitude);
        Plugin.logger.LogInfo($"[Volcano] Phase 2 completed. (Phase Duration: {Time.time - shakeStart:F2}s)");

        // --- 3. BUILDUP DELAY ---
        float delayStart = Time.time;
        Plugin.logger.LogInfo($"[Volcano] Phase 3 (Buildup Delay) starting. Holding for {BuildupDelay}s...");
        yield return new WaitForSeconds(BuildupDelay); 
        Plugin.logger.LogInfo($"[Volcano] Phase 3 completed. (Phase Duration: {Time.time - delayStart:F2}s)");

        // --- 4. STONE RAIN ---
        float rainStart = Time.time;
        Plugin.logger.LogInfo($"[Volcano] Phase 4 (Stone Rain) starting. Target drops: {StonesConfig.VolcanoMaxStones.Value}");

        float dropRate = 0.8f;
        try
        {
            dropRate = StonesConfig.StoneRainDropRate.Value;
        }
        catch (System.Exception e)
        {
            Plugin.logger.LogError($"[Volcano] Error reading StonesConfig.StoneRainDropRate: {e.Message}");
        }

        Plugin.logger.LogInfo($"[Volcano] Drop Interval: {dropRate}s");
        if (dropRate <= 0.1f)
        {
            dropRate = 0.5f;
            Plugin.logger.LogWarning($"[Volcano] Drop rate was under safe threshold! Forcing to {dropRate}s.");
        }
        
        int burstCount = Mathf.Max(1, StonesConfig.VulcanStoneBurstCount.Value);
        int ItemsToDrop = StonesConfig.VolcanoMaxStones.Value;

        for (int i = 0; i < ItemsToDrop; i+= burstCount)
        {
            SpawnVulcanStoneBurst();

            yield return new WaitForSeconds(dropRate);
        }
        Plugin.logger.LogInfo($"[Volcano] Phase 4 completed. (Phase Duration: {Time.time - rainStart:F2}s)");

        // --- 5. FADE OUT ---
        float fadeOutStart = Time.time;
        Plugin.logger.LogInfo($"[Volcano] Phase 5 (Fade Out) starting. Expected duration: {FadeOutDuration}s");
        yield return FadeEnvironment(sun, state.OriginalAmbient, state.OriginalFogColor, state.OriginalFogDensity, state.OriginalSunColor, state.OriginalSunIntensity, FadeOutDuration);
        Plugin.logger.LogInfo($"[Volcano] Phase 5 completed. (Phase Duration: {Time.time - fadeOutStart:F2}s)");

        // --- 6. CLEANUP ---
        Plugin.logger.LogInfo("[Volcano] Cleaning up enforcer object...");
        if (enforcerObject != null)
        {
            Object.Destroy(enforcerObject);
            visualEnforcer = null;
        }
        
        RenderSettings.fog = state.OriginalFog;
        Plugin.logger.LogInfo($"[Volcano] === EVENT COMPLETE === Total Time Elapsed: {Time.time - startTime:F2}s");
    }

    private static Vector3 ComputeBurstSpawnPosition(Vector3 playerCenter)
    {
        float offsetX = Random.Range(-6f, 6f);
        float offsetZ = Random.Range(-6f, 6f);
    
        // Add the offset to whichever player's center was passed in
        return playerCenter + new Vector3(offsetX, 18f, offsetZ);
    }
    
    private static void SpawnVulcanStoneBurst()
    {
        
        if (!PhotonNetwork.IsMasterClient) { return; }

        int burstCount = Mathf.Max(1, StonesConfig.VulcanStoneBurstCount.Value);
    
        // Find every player currently in the scene
        Player[] allPlayers = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);

        // Loop through every single player
        foreach (Player p in allPlayers)
        {
            // Skip if the player is dead or missing a character
            if (p == null || p.character == null) continue;

            // Spawn the burst amount for this specific player
            for (int i = 0; i < burstCount; i++)
            {
                // Pass this specific player's center to your calculator
                Vector3 spawnPosition = ComputeBurstSpawnPosition(p.character.Center);

                GameObject? stone = ItemSpawnHelper.SpawnRandomStormStone(spawnPosition, Random.rotation);
                if (stone == null)
                {
                    Plugin.logger.LogWarning($"[Vulcan] Burst stone {i + 1}/{burstCount} failed to spawn.");
                    continue;
                }
            }
        }
    }

   

    private static AtmosphereState CaptureAtmosphere(Light? sun)
    {
        return new AtmosphereState
        {
            OriginalAmbient      = RenderSettings.ambientLight,
            OriginalFogColor     = RenderSettings.fogColor,
            OriginalFogDensity   = RenderSettings.fogDensity,
            OriginalSunColor     = sun != null ? sun.color : Color.white,
            OriginalSunIntensity = sun != null ? sun.intensity : 1f,
            OriginalFog          = RenderSettings.fog,
        };
    }

    private static IEnumerator FadeEnvironment(
        Light? sun, Color targetAmbient, Color targetFogColor, float targetFogDensity,
        Color targetSunColor, float targetSunIntensity, float duration)
    {
        Color startAmbient = RenderSettings.ambientLight;
        Color startFogColor = RenderSettings.fogColor;
        float startFogDensity = RenderSettings.fogDensity;
        Color startSunColor = sun != null ? sun.color : Color.white;
        float startSunIntensity = sun != null ? sun.intensity : 1f;

        if (visualEnforcer == null)
        {
            ModLogger.LogError("[Volcano] visualEnforcer is NULL inside FadeEnvironment! Aborting fade step.");
            yield break;
        }

        ModLogger.LogInfo($"[Volcano] FadeEnvironment started over {duration:F1}s.");

        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            
            visualEnforcer.ambient = Color.Lerp(startAmbient, targetAmbient, t);
            visualEnforcer.fogColor = Color.Lerp(startFogColor, targetFogColor, t);
            visualEnforcer.fogDensity = Mathf.Lerp(startFogDensity, targetFogDensity, t);
            visualEnforcer.sunColor = Color.Lerp(startSunColor, targetSunColor, t);
            visualEnforcer.sunIntensity = Mathf.Lerp(startSunIntensity, targetSunIntensity, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        visualEnforcer.ambient = targetAmbient;
        visualEnforcer.fogColor = targetFogColor;
        visualEnforcer.fogDensity = targetFogDensity;
        visualEnforcer.sunColor = targetSunColor;
        visualEnforcer.sunIntensity = targetSunIntensity;

        ModLogger.LogInfo("[Volcano] FadeEnvironment target values locked in.");
    }

    private static IEnumerator ShakeCamera(float duration, float magnitude)
    {
        if (visualEnforcer == null)
        {
            ModLogger.LogError("[Volcano] visualEnforcer is NULL inside ShakeCamera! Aborting shake step.");
            yield break;
        }

        ModLogger.LogInfo($"[Volcano] Enabling camera shake on visualEnforcer (mag={magnitude}, duration={duration}s)");
        visualEnforcer.shakeMagnitude = magnitude;
        visualEnforcer.isShaking = true;
        
        yield return new WaitForSeconds(duration);
        
        ModLogger.LogInfo("[Volcano] Disabling camera shake on visualEnforcer.");
        visualEnforcer.isShaking = false;
    }

    private static Light? FindMainDirectionalLight()
    {
        var lights = Object.FindObjectsByType<Light>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        Light? best = null;
        float bestIntensity = float.NegativeInfinity;
        foreach (Light l in lights)
        {
            if (l.type != LightType.Directional) continue;
            if (!l.enabled) continue;
            if (l.intensity > bestIntensity)
            {
                best = l;
                bestIntensity = l.intensity;
            }
        }

        if (best == null)
        {
            ModLogger.LogWarning("[Volcano] No active directional light found in scene.");
            return best;
        }
        ModLogger.LogInfo($"[Volcano] Found primary directional light: '{best.name}' (Intensity: {best.intensity})");
        return best;
    }
}