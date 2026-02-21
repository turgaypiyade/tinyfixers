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

    public float GetPlaybackDuration(int targetCount)
    {
        int safeTargetCount = Mathf.Max(0, targetCount);
        if (safeTargetCount <= 0)
            return 0f;

        return ((safeTargetCount - 1) * Mathf.Max(0f, spawnJitter)) + Mathf.Max(0f, destroyDelay);
    }

    public void PlayEmitterLightning(Vector3 emitterWorldPos, List<Vector3> targetWorldPositions)
    {
        Debug.Log($"[LightningSpawner] called targets={targetWorldPositions?.Count ?? -1} prefab={(lightningPrefab ? lightningPrefab.name : "NULL")}");

        if (targetWorldPositions == null || targetWorldPositions.Count == 0)
        {
            Debug.Log("[Lightning][Spawner] PlayEmitterLightning called with no targets.");
            return;
        }

        var targetsCopy = new List<Vector3>(targetWorldPositions.Count);
        for (int i = 0; i < targetWorldPositions.Count; i++)
            targetsCopy.Add(targetWorldPositions[i]);

        Debug.Log($"[Lightning][Spawner] Spawn start origin={emitterWorldPos} targets={targetsCopy.Count} playback={GetPlaybackDuration(targetsCopy.Count):0.000}s");
        StartCoroutine(CoPlay(emitterWorldPos, targetsCopy));
    }

    private IEnumerator CoPlay(Vector3 emitterWorldPos, List<Vector3> targets)
    {
        Vector3 current = emitterWorldPos;

        for (int i = 0; i < targets.Count; i++)
        {
            var start = current;
            var end = targets[i];

            Debug.Log($"[Lightning][Spawner] Beam {i + 1}/{targets.Count} start={start} end={end} dist={Vector3.Distance(start, end)}");

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
            beam.Dump(" FROM_SPAWNER");

            current = end; // ✅ zincir: bir sonraki beam buradan başlar

            float delay = useChain ? chainStepDelay : spawnJitter;
            if (delay > 0f)
                yield return new WaitForSeconds(delay);
        }

        yield return new WaitForSeconds(destroyDelay);
    }

}
