using UnityEngine;

namespace Stones;

public class VolcanoVisuals : MonoBehaviour
{
    public Light? sun;
    public Color ambient;
    public Color fogColor;
    public Color sunColor;
    public float fogDensity;
    public float sunIntensity;
    
    private bool _enforceEnvironment = false;
    public bool enforceEnvironment
    {
        get => _enforceEnvironment;
        set
        {
            if (_enforceEnvironment != value)
            {
                ModLogger.LogInfo($"[VolcanoVisuals] enforceEnvironment changed: {_enforceEnvironment} -> {value}");
            }
            _enforceEnvironment = value;
        }
    }

    private bool _isShaking = false;
    public bool isShaking
    {
        get => _isShaking;
        set
        {
            if (_isShaking != value)
            {
                ModLogger.LogInfo($"[VolcanoVisuals] isShaking changed: {_isShaking} -> {value} (Magnitude: {shakeMagnitude})");
            }
            _isShaking = value;
        }
    }

    public float shakeMagnitude = 0f;

    private Camera? targetCam;
    private Vector3 shakeBasePosition;
    private bool shakeBaseCaptured;

    private void Start()
    {
        EnsureCameraAssigned();

        if (sun == null)
        {
            ModLogger.LogWarning("[VolcanoVisuals] Warning: Sun light reference is null. Environment sun color/intensity will not be enforced.");
        }
    }

    private void LateUpdate()
    {
        if (enforceEnvironment)
        {
            ApplyEnvironmentalVisuals();
        }

        if (isShaking)
        {
            ApplyCameraShake();
        }
        else if (shakeBaseCaptured)
        {
            RestoreCameraPosition();
        }
    }

    private void OnDestroy()
    {
        ModLogger.LogInfo("[VolcanoVisuals] Component destroyed / cleaned up.");
    }

    private void ApplyEnvironmentalVisuals()
    {
        RenderSettings.ambientLight = ambient;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;

        if (sun != null)
        {
            sun.color = sunColor;
            sun.intensity = sunIntensity;
        }

        Shader.SetGlobalColor("SkyTopColor", new Color(0.2f, 0.0f, 0.0f));     // Dark red/black
        Shader.SetGlobalColor("SkyMidColor", new Color(0.8f, 0.3f, 0.0f));     // Orange
        Shader.SetGlobalColor("SkyBottomColor", new Color(1.0f, 0.2f, 0.0f));  // Bright red
        Shader.SetGlobalFloat("GlobalWind", 1.0f);                             // Force ash/wind effect
    }

    private void ApplyCameraShake()
    {
        if (!EnsureCameraAssigned()) 
        {
            return;
        }

        if (!shakeBaseCaptured)
        {
            CaptureBaseCameraPosition();
        }
        
        float x = Random.Range(-1f, 1f) * shakeMagnitude;
        float y = Random.Range(-1f, 1f) * shakeMagnitude;
        
        targetCam!.transform.localPosition = shakeBasePosition + new Vector3(x, y, 0f);
    }

    private void CaptureBaseCameraPosition()
    {
        shakeBasePosition = targetCam!.transform.localPosition;
        shakeBaseCaptured = true;
        ModLogger.LogInfo($"[VolcanoVisuals] Captured base camera position for shake: {shakeBasePosition}");
    }

    private void RestoreCameraPosition()
    {
        if (targetCam != null)
        {
            targetCam.transform.localPosition = shakeBasePosition;
            ModLogger.LogInfo("[VolcanoVisuals] Shake ended. Camera position restored to base.");
        }
        shakeBaseCaptured = false;
    }
    
    private bool EnsureCameraAssigned()
    {
        if (targetCam != null) 
        {
            return true;
        }

        targetCam = Camera.main ?? Object.FindAnyObjectByType<Camera>();
        
        if (targetCam != null)
        {
            ModLogger.LogInfo($"[VolcanoVisuals] Target camera acquired: '{targetCam.name}'");
            return true;
        }

        ModLogger.LogWarning("[VolcanoVisuals] Cannot apply camera effects: targetCam is null!");
        return false;
    }
}