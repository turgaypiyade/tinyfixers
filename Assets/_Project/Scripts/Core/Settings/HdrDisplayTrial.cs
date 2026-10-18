using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Session-only HDR/SDR comparison. Owns only a temporary tonemapping override;
/// never edits shared profiles, Bloom, exposure, saturation or Screen.brightness.
/// </summary>
public sealed class HdrDisplayTrial : MonoBehaviour
{
    public enum DisplayState { Unavailable, Sdr, Hdr, Switching, Failed }

    private static HdrDisplayTrial instance;
    private const float RequestTimeout = 8f;
    private HDROutputSettings output;
    private Volume volume;
    private VolumeProfile profile;
    private bool requestPending;
    private bool requestedHdr;
    private bool requestFailed;
    private bool nativePending;
    private float requestDeadline;

    public DisplayState State { get; private set; } = DisplayState.Unavailable;
    public bool IsHdrActive => output != null && !Application.isEditor && output.active;
    public bool CanToggle => output != null && !Application.isEditor && output.available
                             && !nativePending && !requestPending;
    public event Action Changed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => instance = null;

    public static HdrDisplayTrial GetOrCreate()
    {
        if (instance == null)
        {
            var go = new GameObject("HDR Display Trial");
            instance = go.AddComponent<HdrDisplayTrial>();
            DontDestroyOnLoad(go);
        }
        return instance;
    }

    private void Awake()
    {
        output = HDROutputSettings.main;
        RefreshState();
    }

    public void Toggle()
    {
        RefreshState();
        if (!CanToggle) return;

        EnsureProfile();
        requestedHdr = !output.active;
        requestPending = true;
        requestFailed = false;
        requestDeadline = Time.realtimeSinceStartup + RequestTimeout;
        output.RequestHDRModeChange(requestedHdr);
        RefreshState();
    }

    private void OnEnable() => RenderPipelineManager.beginContextRendering += BeforeRendering;
    private void OnDisable()
    {
        RenderPipelineManager.beginContextRendering -= BeforeRendering;
        if (volume != null) volume.enabled = false;
    }

    // One shared monitor, no scene/board scans, coroutines or per-frame allocations.
    private void Update() => RefreshState();

    private void BeforeRendering(ScriptableRenderContext context, List<Camera> cameras)
    {
        // Match the actual output before URP evaluates camera volumes, including
        // the first HDR frame and the first frame back in SDR.
        SyncVolume(output != null && !Application.isEditor && output.active);
    }

    private void RefreshState()
    {
        bool available = output != null && !Application.isEditor && output.available;
        bool active = available && output.active;
        bool wasNativePending = nativePending;
        nativePending = available && output.HDRModeChangeRequested;

        if (!available)
        {
            requestPending = false;
            requestFailed = false;
        }
        else if (requestPending)
        {
            if (!nativePending && active == requestedHdr)
                requestPending = false;
            else if (Time.realtimeSinceStartup >= requestDeadline)
            {
                requestPending = false;
                requestFailed = true;
            }
        }
        // A slow native request may complete after the timeout.
        if (requestFailed && !nativePending && active == requestedHdr)
            requestFailed = false;

        SyncVolume(active);
        var next = !available ? DisplayState.Unavailable
            : requestFailed ? DisplayState.Failed
            : requestPending || nativePending ? DisplayState.Switching
            : active ? DisplayState.Hdr : DisplayState.Sdr;
        if (next == State && nativePending == wasNativePending) return;
        State = next;
        Changed?.Invoke();
    }

    private void SyncVolume(bool active)
    {
        if (active && volume == null) EnsureProfile();
        if (volume != null && volume.enabled != active) volume.enabled = active;
    }

    private void EnsureProfile()
    {
        if (volume != null) return;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        profile.name = "HDR Trial Tonemapping (Runtime)";
        profile.hideFlags = HideFlags.DontSave;
        var tone = profile.Add<Tonemapping>();
        tone.hideFlags = HideFlags.DontSave;
        tone.mode.Override(TonemappingMode.Neutral);
        tone.neutralHDRRangeReductionMode.Override(NeutralRangeReductionMode.BT2390);
        tone.detectBrightnessLimits.Override(true);
        tone.detectPaperWhite.Override(true);
        tone.hueShiftAmount.Override(0f);

        // Default layer is included by the active gameplay and menu cameras.
        volume = gameObject.AddComponent<Volume>();
        volume.enabled = false;
        volume.isGlobal = true;
        volume.priority = 10000f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) return;
        // Don't count time spent in the background as a failed mode switch.
        if (requestPending) requestDeadline = Time.realtimeSinceStartup + RequestTimeout;
        RefreshState();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        Changed = null;
        if (volume != null)
        {
            volume.enabled = false;
            volume.sharedProfile = null;
        }
        if (profile != null)
        {
            // VolumeProfile does not own/destroy the components added at runtime.
            for (int i = 0; i < profile.components.Count; i++) Destroy(profile.components[i]);
            Destroy(profile);
        }
        if (output != null && !Application.isEditor && output.available
            && (output.active || output.HDRModeChangeRequested))
            output.RequestHDRModeChange(false);
    }
}
