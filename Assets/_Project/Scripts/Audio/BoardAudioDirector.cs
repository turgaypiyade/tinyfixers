using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoardAudioDirector : MonoBehaviour
{
    [SerializeField] private BoardSfxProfile profile;
    [SerializeField] private AudioSource transientSource;
    [SerializeField] private AudioSource accentSource;
    [SerializeField] private bool debugLogs;

    private readonly Dictionary<string, float> lastPlayTimes = new Dictionary<string, float>(16);
    private float suppressFallsUntil = -999f;

    private struct ResolvedPlay
    {
        public bool isValid;
        public string familyKey;
        public AudioSource source;
        public BoardSfxProfile.VariantSet set;
        public float volumeMultiplier;
    }

    public void SuppressFallsFor(float seconds)
    {
        if (seconds <= 0f) return;
        suppressFallsUntil = Mathf.Max(suppressFallsUntil, Time.time + seconds);
    }

    public void ScheduleFallBatch(int tileCount, int maxDist, int resolvePass)
    {
        // Şimdilik batch; sonra landing bucket’a kolayca genişletilir
        Emit(BoardSfxRequest.Fall(tileCount, maxDist));
    }

    public void Emit(BoardSfxRequest request)
    {
        if (request.Delay > 0f)
        {
            StartCoroutine(EmitDelayed(request));
            return;
        }

        PlayNow(request);
    }

    private IEnumerator EmitDelayed(BoardSfxRequest request)
    {
        yield return new WaitForSeconds(request.Delay);
        PlayNow(request);
    }

    private void PlayNow(BoardSfxRequest request)
    {
        if (profile == null)
            return;

        if (request.Cue == BoardSfxCue.FallLanding && Time.time < suppressFallsUntil)
            return;

        ResolvedPlay resolved = Resolve(request);
        if (!resolved.isValid || resolved.set == null)
            return;

        if (resolved.set.clips == null || resolved.set.clips.Length == 0)
            return;

        if (resolved.source == null)
            return;

        float lastTime;
        if (lastPlayTimes.TryGetValue(resolved.familyKey, out lastTime))
        {
            if (Time.time - lastTime < resolved.set.minInterval)
                return;
        }

        AudioClip clip = PickRandomClip(resolved.set.clips);
        if (clip == null)
            return;

        float volume = resolved.set.volume * resolved.volumeMultiplier;
        resolved.source.PlayOneShot(clip, volume);
        lastPlayTimes[resolved.familyKey] = Time.time;

        if (request.DuckFalls || request.Priority >= 70)
            SuppressFallsFor(0.08f);

        if (debugLogs)
            Debug.Log("[BoardAudioDirector] cue=" + request.Cue + " family=" + resolved.familyKey + " volume=" + volume.ToString("0.00"));
    }

    private ResolvedPlay Resolve(BoardSfxRequest request)
    {
        ResolvedPlay r = new ResolvedPlay();
        r.isValid = true;
        r.volumeMultiplier = 1f;

        switch (request.Cue)
        {
            case BoardSfxCue.FallLanding:
            {
                r.familyKey = "fall";
                r.source = transientSource != null ? transientSource : accentSource;

                if (request.Count >= 6 || request.Intensity >= 4)
                    r.set = profile.fallHeavy;
                else if (request.Count >= 3 || request.Intensity >= 2)
                    r.set = profile.fallMedium;
                else
                    r.set = profile.fallLight;

                if (request.Count >= 6)
                    r.volumeMultiplier = 1.12f;
                else if (request.Count >= 3)
                    r.volumeMultiplier = 1.05f;

                break;
            }

            case BoardSfxCue.SpecialCreated:
            {
                r.familyKey = "create_" + request.Special;
                r.source = accentSource != null ? accentSource : transientSource;
                r.set = GetCreateSet(request.Special);
                r.volumeMultiplier = 1f + Mathf.Clamp01((request.Count - 1) * 0.12f);
                break;
            }

            case BoardSfxCue.SpecialActivated:
            {
                r.familyKey = "activate_" + request.Special;
                r.source = accentSource != null ? accentSource : transientSource;
                r.set = GetActivateSet(request.Special);
                r.volumeMultiplier = 1f + Mathf.Clamp01((request.Intensity - 1) * 0.10f);
                break;
            }

            case BoardSfxCue.ComboStart:
            {
                r.familyKey = "combo_start";
                r.source = accentSource != null ? accentSource : transientSource;
                r.set = profile.comboStart;
                r.volumeMultiplier = 1f + Mathf.Clamp01((request.Intensity - 1) * 0.10f);
                break;
            }

            case BoardSfxCue.ComboImpact:
            {
                r.familyKey = "combo_impact";
                r.source = accentSource != null ? accentSource : transientSource;
                r.set = profile.comboImpact;
                r.volumeMultiplier = 1f + Mathf.Clamp01((request.Intensity - 1) * 0.15f);
                break;
            }

            default:
            {
                r.isValid = false;
                break;
            }
        }

        return r;
    }

    private BoardSfxProfile.VariantSet GetCreateSet(TileSpecial special)
    {
        switch (special)
        {
            case TileSpecial.LineH:
            case TileSpecial.LineV:
                return profile.createLine;

            case TileSpecial.PatchBot:
                return profile.createPatchBot;

            case TileSpecial.PulseCore:
                return profile.createPulseCore;

            case TileSpecial.SystemOverride:
                return profile.createSystemOverride;
        }

        return null;
    }

    private BoardSfxProfile.VariantSet GetActivateSet(TileSpecial special)
    {
        switch (special)
        {
            case TileSpecial.LineH:
            case TileSpecial.LineV:
                return profile.activateLine;

            case TileSpecial.PatchBot:
                return profile.activatePatchBot;

            case TileSpecial.PulseCore:
                return profile.activatePulseCore;

            case TileSpecial.SystemOverride:
                return profile.activateSystemOverride;
        }

        return null;
    }

    private AudioClip PickRandomClip(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
            return null;

        if (clips.Length == 1)
            return clips[0];

        int idx = Random.Range(0, clips.Length);
        return clips[idx];
    }
}