using System.Collections;
using Photon.Pun;
using UnityEngine;

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

    

    private struct AtmosphereState
    {
        public Color OriginalAmbient;
        public Color OriginalFogColor;
        public float OriginalFogDensity;
        public Color OriginalSunColor;
        public float OriginalSunIntensity;
        public bool  OriginalFog;
    }
    
    private static VolcanoVisuals? _visualEnforcer;
    private static AtmosphereState? _originalState;

    public static IEnumerator Run()
    {
        float startTime = Time.time;
        ModLogger.LogInfo($"[Volcano] === EVENT START === Current Time: {startTime:F2}");
        
        GameObject enforcerObject = CreateVisualEnforcer();
        SetupAudio(enforcerObject);
        
       
        Light? sun = FindMainDirectionalLight();
        _visualEnforcer!.sun = sun;
        _visualEnforcer.enforceEnvironment = true;
        _originalState = CaptureAtmosphere(sun);
        
        yield return ExecutePhase1_FadeIn(sun);
        yield return ExecutePhase2_CameraShake();
        yield return ExecutePhase3_Delay();
        yield return ExecutePhase4_StoneRain();
        yield return ExecutePhase5_FadeOut(sun, _originalState);
        
        ExecutePhase6_Cleanup();

        ModLogger.LogInfo($"[Volcano] === EVENT COMPLETE === Total Time Elapsed: {Time.time - startTime:F2}s");
    }

    private static IEnumerator ExecutePhase1_FadeIn(Light? sun)
    {
        ModLogger.LogInfo($"[Volcano] Phase 1 (Fade In) starting. Expected duration: {FadeInDuration}s");
        yield return FadeEnvironment(sun, StormAmbient, StormFogColor, StormFogDensity, StormSunColor, StormSunIntensity, FadeInDuration);
        ModLogger.LogInfo("[Volcano] Phase 1 completed.");
    }

    private static IEnumerator ExecutePhase2_CameraShake()
    {
        ModLogger.LogInfo($"[Volcano] Phase 2 (Camera Shake) starting. Duration: {ShakeDuration}s");
        yield return ShakeCamera(ShakeDuration, ShakeMagnitude);
        ModLogger.LogInfo("[Volcano] Phase 2 completed.");
    }

    private static IEnumerator ExecutePhase3_Delay()
    {
        ModLogger.LogInfo($"[Volcano] Phase 3 (Buildup Delay) starting. Holding for {BuildupDelay}s...");
        yield return new WaitForSeconds(BuildupDelay); 
        ModLogger.LogInfo("[Volcano] Phase 3 completed.");
    }

    private static IEnumerator ExecutePhase4_StoneRain()
    {
        ModLogger.LogInfo($"[Volcano] Phase 4 (Stone Rain) starting.");

        float dropRate = GetSafeDropRate();
        int burstCount = Mathf.Max(1, StonesConfig.VulcanStoneBurstCount.Value);
        int itemsToDrop = StonesConfig.VolcanoMaxStones.Value;

        for (int i = 0; i < itemsToDrop; i += burstCount)
        {
            SpawnVulcanStoneBurst();
            yield return new WaitForSeconds(dropRate);
        }
        ModLogger.LogInfo("[Volcano] Phase 4 completed.");
    }

    private static IEnumerator ExecutePhase5_FadeOut(Light? sun, AtmosphereState? state)
    {
        if (state is not AtmosphereState validState)
        {
            yield break;
        }
        ModLogger.LogInfo($"[Volcano] Phase 5 (Fade Out) starting. Expected duration: {FadeOutDuration}s");
        yield return FadeEnvironment(sun, validState.OriginalAmbient, validState.OriginalFogColor, validState.OriginalFogDensity, validState.OriginalSunColor, validState.OriginalSunIntensity, FadeOutDuration);
        ModLogger.LogInfo("[Volcano] Phase 5 completed.");
    }

    private static void ExecutePhase6_Cleanup()
    {
        ModLogger.LogInfo("[Volcano] Phase 6 (Cleanup) starting...");
        VulcanManager.EnsureInstance().StopVulcanOutbreak();
    }
    
    public static void CleanupVisuals()
    {
        ModLogger.LogInfo("[Volcano] Cleaning up visuals...");

        if (_visualEnforcer != null)
        {
            Object.Destroy(_visualEnforcer);
            _visualEnforcer = null; 
        }

        if (_originalState != null)
        {
            RenderSettings.fog = _originalState.Value.OriginalFog;
        }
    }

    private static GameObject CreateVisualEnforcer()
    {
        ModLogger.LogInfo("[Volcano] Creating VolcanoVisualEnforcer GameObject...");
        GameObject enforcerObject = new GameObject("VolcanoVisualEnforcer");
        _visualEnforcer = enforcerObject.AddComponent<VolcanoVisuals>();
        return enforcerObject;
    }

    private static void SetupAudio(GameObject enforcerObject)
    {
        ModLogger.LogInfo("[Volcano] Setting up AudioSources...");
        
        AudioSource fireAudio = enforcerObject.AddComponent<AudioSource>();
        fireAudio.spatialBlend = 0f; 
        fireAudio.loop = true;
        fireAudio.volume = 0.1f;     
        
        AudioSource explosionAudio = enforcerObject.AddComponent<AudioSource>();
        explosionAudio.spatialBlend = 0f; 
        explosionAudio.loop = false;
        explosionAudio.volume = 0.4f;     

        AudioClip fireClip = Plugin.peakBundle.LoadAsset<AudioClip>("Au_Fire_Loop");
        AudioClip explosionClip = Plugin.peakBundle.LoadAsset<AudioClip>("Au_Explosion_Debris");
        
        if (fireClip != null)
        {
            fireAudio.clip = fireClip;
            fireAudio.Play();
        }
        else
        {
            ModLogger.LogError("[Volcano] FAILED to load Au_Fire_Loop! Check asset name or bundle.");
        }

        if (explosionClip != null)
        {
            explosionAudio.clip = explosionClip;
            explosionAudio.Play();
        }
        else
        {
            ModLogger.LogError("[Volcano] FAILED to load Au_Explosion_Debris! Check asset name or bundle.");
        }
    }

    private static float GetSafeDropRate()
    {
        float dropRate = 0.8f;
        try
        {
            dropRate = StonesConfig.StoneRainDropRate.Value;
        }
        catch (System.Exception e)
        {
            ModLogger.LogError($"[Volcano] Error reading StonesConfig.StoneRainDropRate: {e.Message}");
        }

        if (dropRate <= 0.1f)
        {
            dropRate = 0.5f;
            ModLogger.LogWarning($"[Volcano] Drop rate was under safe threshold! Forcing to {dropRate}s.");
        }
        return dropRate;
    }

    private static Vector3 ComputeBurstSpawnPosition(Vector3 playerCenter)
    {
        float offsetX = Random.Range(-6f, 6f);
        float offsetZ = Random.Range(-6f, 6f);
        return playerCenter + new Vector3(offsetX, 18f, offsetZ);
    }
    
    private static void SpawnVulcanStoneBurst()
    {
        if (!PhotonNetwork.IsMasterClient) return;

        int burstCount = Mathf.Max(1, StonesConfig.VulcanStoneBurstCount.Value);
        Player[] allPlayers = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);

        foreach (Player p in allPlayers)
        {
            if (p == null || p.character == null) continue;

            for (int i = 0; i < burstCount; i++)
            {
                Vector3 spawnPosition = ComputeBurstSpawnPosition(p.character.Center);
                GameObject? stone = ItemSpawnHelper.SpawnRandomStormStone(spawnPosition, Random.rotation);
                
                if (stone == null)
                {
                    ModLogger.LogWarning($"[Vulcan] Burst stone {i + 1}/{burstCount} failed to spawn.");
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

        if (_visualEnforcer == null)
        {
            ModLogger.LogError("[Volcano] visualEnforcer is NULL inside FadeEnvironment! Aborting fade step.");
            yield break;
        }
        
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            
            _visualEnforcer.ambient = Color.Lerp(startAmbient, targetAmbient, t);
            _visualEnforcer.fogColor = Color.Lerp(startFogColor, targetFogColor, t);
            _visualEnforcer.fogDensity = Mathf.Lerp(startFogDensity, targetFogDensity, t);
            _visualEnforcer.sunColor = Color.Lerp(startSunColor, targetSunColor, t);
            _visualEnforcer.sunIntensity = Mathf.Lerp(startSunIntensity, targetSunIntensity, t);
            
            elapsed += Time.deltaTime;
            yield return null;
        }
        _visualEnforcer.ambient = targetAmbient;
        _visualEnforcer.fogColor = targetFogColor;
        _visualEnforcer.fogDensity = targetFogDensity;
        _visualEnforcer.sunColor = targetSunColor;
        _visualEnforcer.sunIntensity = targetSunIntensity;
    }

    private static IEnumerator ShakeCamera(float duration, float magnitude)
    {
        if (_visualEnforcer == null)
        {
            yield break;
        }
        
        _visualEnforcer.shakeMagnitude = magnitude;
        _visualEnforcer.isShaking = true;
        
        yield return new WaitForSeconds(duration);
        
        _visualEnforcer.isShaking = false;
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
        return best;
    }
}