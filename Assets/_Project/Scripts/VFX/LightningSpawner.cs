using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

public class LightningSpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LightningBeam lightningPrefab;
    [SerializeField] private RectTransform vfxRoot; // UI root

    [Header("Timing")]
    [SerializeField] private float spawnJitter = 0.02f;
    [SerializeField] private float destroyDelay = 0.18f;

    [Header("Chain Lightning")]
    [SerializeField] private bool useChain = true;
    [SerializeField] private float chainStepDelay = 0.04f;

    private static string V3(Vector3 v) => $"({v.x:0.###},{v.y:0.###},{v.z:0.###})";

    public float GetStepDelay()
    {
        return Mathf.Max(0f, useChain ? chainStepDelay : spawnJitter);
    }

    public float GetPlaybackDuration(int targetCount)
    {
        int safeTargetCount = Mathf.Max(0, targetCount);
        if (safeTargetCount <= 0)
            return 0f;

        float stepDelay = GetStepDelay();
        return ((safeTargetCount - 1) * stepDelay) + Mathf.Max(0f, destroyDelay);
    }

    public void PlayEmitterLightning(Vector3 emitterWorldPos, List<Vector3> targetWorldPositions, Action<int> onTargetBeamSpawned = null)
    {
        if (targetWorldPositions == null || targetWorldPositions.Count == 0)
            return;

        Debug.Log($"[LightningSpawn.PlayEmitterLightning] emitter={V3(emitterWorldPos)} targets={targetWorldPositions.Count} root={(vfxRoot ? vfxRoot.name : "NULL")}");

        var targetsCopy = new List<Vector3>(targetWorldPositions.Count);
        for (int i = 0; i < targetWorldPositions.Count; i++)
            targetsCopy.Add(targetWorldPositions[i]);

        StartCoroutine(CoPlay(emitterWorldPos, targetsCopy, onTargetBeamSpawned));
    }

    public LightningBeam BeginPersistentLightning(Func<Vector3> startWorldProvider, Func<Vector3> endWorldProvider, Color color)
    {
        var beam = CreateBeamInstance();
        if (beam == null)
            return null;

        beam.InitPersistent(startWorldProvider, endWorldProvider, color);
        return beam;
    }

    public void PlayLineSweepSteps(List<Vector3> stepWorldPositions)
    {
        if (stepWorldPositions == null || stepWorldPositions.Count == 0)
            return;

        Debug.Log($"[LightningSpawn.PlayLineSweepSteps] steps={stepWorldPositions.Count} root={(vfxRoot ? vfxRoot.name : "NULL")}");
        StartCoroutine(CoPlayLineSweepSteps(stepWorldPositions));
    }

    private IEnumerator CoPlayLineSweepSteps(List<Vector3> steps)
    {
        Vector3 prev = steps[0];

        for (int i = 1; i < steps.Count; i++)
        {
            Vector3 cur = steps[i];

            Debug.Log($"[LightningSpawn.Step] prev={V3(prev)} cur={V3(cur)}");

            var beam = CreateBeamInstance();
            if (beam == null)
                yield break;
            beam.Init(prev, cur);

            prev = cur;

            float delay = GetStepDelay();
            if (delay > 0f) yield return new WaitForSeconds(delay);
            else yield return null;
        }

        yield return new WaitForSeconds(destroyDelay);
    }

    public void PlayLineSweep(Vector3 lineStartWorldPos, Vector3 lineEndWorldPos)
    {
        Debug.Log($"[LightningSpawn.PlayLineSweep] start={V3(lineStartWorldPos)} end={V3(lineEndWorldPos)} root={(vfxRoot ? vfxRoot.name : "NULL")}");
        StartCoroutine(CoPlayLineSweep(lineStartWorldPos, lineEndWorldPos));
    }

    private IEnumerator CoPlayLineSweep(Vector3 lineStartWorldPos, Vector3 lineEndWorldPos)
    {
        var beam = CreateBeamInstance();
        if (beam == null)
            yield break;
        beam.Init(lineStartWorldPos, lineEndWorldPos);

        yield return new WaitForSeconds(destroyDelay);
    }
    private IEnumerator CoPlay(Vector3 emitterWorldPos, List<Vector3> targets, Action<int> onTargetBeamSpawned)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var start = emitterWorldPos;
            var end = targets[i];

            Debug.Log($"[LightningSpawn.CoPlay] index={i} start={V3(start)} end={V3(end)}");

            var beam = CreateBeamInstance();
            if (beam == null)
                yield break;
            beam.Init(start, end);
            onTargetBeamSpawned?.Invoke(i);

            float delay = GetStepDelay();
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
        }

        yield return new WaitForSeconds(destroyDelay);
    }

    private LightningBeam CreateBeamInstance()
    {
        if (lightningPrefab == null || vfxRoot == null)
            return null;

        var beam = Instantiate(lightningPrefab, vfxRoot);
        beam.transform.localPosition = Vector3.zero;
        beam.transform.localRotation = Quaternion.identity;

        var s = vfxRoot.lossyScale;
        beam.transform.localScale = new Vector3(
            1f / Mathf.Max(0.0001f, s.x),
            1f / Mathf.Max(0.0001f, s.y),
            1f / Mathf.Max(0.0001f, s.z)
        );

        var line = beam.GetComponent<LineRenderer>();
        if (line != null)
            line.useWorldSpace = true;

        return beam;
    }
}
