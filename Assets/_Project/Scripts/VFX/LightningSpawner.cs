using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LightningSpawner : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private LightningBeam lightningPrefab;
    [SerializeField] private RectTransform vfxRoot; // UI root

    [Header("Timing")]
    [SerializeField] private float spawnJitter = 0.02f;   // aynı anda değil, hafif dağılsın
    [SerializeField] private float destroyDelay = 0.18f;  // yıldırım görülsün diye

    [Header("Chain Lightning")]
    [SerializeField] private bool useChain = true;
    [SerializeField] private float chainStepDelay = 0.04f; // 0.03–0.06 dene

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

    public void PlayEmitterLightning(Vector3 emitterWorldPos, List<Vector3> targetWorldPositions)
    {
        if (targetWorldPositions == null || targetWorldPositions.Count == 0)
        {
            return;
        }

        var targetsCopy = new List<Vector3>(targetWorldPositions.Count);
        for (int i = 0; i < targetWorldPositions.Count; i++)
            targetsCopy.Add(targetWorldPositions[i]);

        StartCoroutine(CoPlay(emitterWorldPos, targetsCopy));
    }

    public void PlayLineSweepSteps(List<Vector3> stepWorldPositions)
    {
        if (stepWorldPositions == null || stepWorldPositions.Count == 0)
            return;

        StartCoroutine(CoPlayLineSweepSteps(stepWorldPositions));
    }

    private IEnumerator CoPlayLineSweepSteps(List<Vector3> steps)
    {
        // Hücre hücre “kısa beam” veya “hit flash” oynatacağız.
        // En basit: emitterWorldPos gibi bir başlangıç istemiyorsan,
        // her step’te küçük bir beam segment (prev→current) çiz.

        Vector3 prev = steps[0];

        for (int i = 1; i < steps.Count; i++)
        {
            Vector3 cur = steps[i];

            var beam = Instantiate(lightningPrefab, vfxRoot);
            beam.transform.localPosition = Vector3.zero;
            beam.transform.localRotation = Quaternion.identity;

            var s = vfxRoot.lossyScale;
            beam.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, s.x),
                1f / Mathf.Max(0.0001f, s.y),
                1f / Mathf.Max(0.0001f, s.z)
            );

            beam.GetComponent<LineRenderer>().useWorldSpace = true;
            beam.Init(prev, cur);

            prev = cur;

            float delay = GetStepDelay();      // chainStepDelay kullan
            if (delay > 0f) yield return new WaitForSeconds(delay);
            else yield return null;
        }

        yield return new WaitForSeconds(destroyDelay);
    }
     public void PlayLineSweep(Vector3 lineStartWorldPos, Vector3 lineEndWorldPos)
    {
        StartCoroutine(CoPlayLineSweep(lineStartWorldPos, lineEndWorldPos));
    }

    private IEnumerator CoPlayLineSweep(Vector3 lineStartWorldPos, Vector3 lineEndWorldPos)
    {
        var beam = Instantiate(lightningPrefab, vfxRoot);
        beam.transform.localPosition = Vector3.zero;
        beam.transform.localRotation = Quaternion.identity;

        var s = vfxRoot.lossyScale;
        beam.transform.localScale = new Vector3(
            1f / Mathf.Max(0.0001f, s.x),
            1f / Mathf.Max(0.0001f, s.y),
            1f / Mathf.Max(0.0001f, s.z)
        );

        beam.GetComponent<LineRenderer>().useWorldSpace = true;
        beam.Init(lineStartWorldPos, lineEndWorldPos);

        yield return new WaitForSeconds(destroyDelay);
    }

    private IEnumerator CoPlay(Vector3 emitterWorldPos, List<Vector3> targets)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            var start = emitterWorldPos;
            var end = targets[i];

            var beam = Instantiate(lightningPrefab, vfxRoot);
            beam.transform.localPosition = Vector3.zero;
            beam.transform.localRotation = Quaternion.identity;

            // Canvas/RectTransform scale'ini iptal et:
            var s = vfxRoot.lossyScale;
            beam.transform.localScale = new Vector3(
                1f / Mathf.Max(0.0001f, s.x),
                1f / Mathf.Max(0.0001f, s.y),
                1f / Mathf.Max(0.0001f, s.z)
            );

            beam.GetComponent<LineRenderer>().useWorldSpace = true;

            beam.Init(start, end);

            float delay = GetStepDelay();
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
        } 

        yield return new WaitForSeconds(destroyDelay);
    }

}
