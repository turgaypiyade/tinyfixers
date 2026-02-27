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

    private IEnumerator CoPlay(Vector3 emitterWorldPos, List<Vector3> targets)
    {
        Vector3 current = emitterWorldPos;

        for (int i = 0; i < targets.Count; i++)
        {
            var start = current;
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

            current = end; // ✅ zincir: bir sonraki beam buradan başlar

            float delay = GetStepDelay();
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
        }

        yield return new WaitForSeconds(destroyDelay);
    }

}
