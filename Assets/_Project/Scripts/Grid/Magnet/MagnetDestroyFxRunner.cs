using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class MagnetDestroyFxRunner : MonoBehaviour
{
    public void Play(
        List<RectTransform> shards,
        List<Image> shardImages,
        List<Vector2> shardVelocities,
        List<float> shardSpins,
        List<RectTransform> flashRts,
        List<Image> flashImages,
        float duration,
        float gravity)
    {
        StartCoroutine(Run(shards, shardImages, shardVelocities, shardSpins, flashRts, flashImages, duration, gravity));
    }

    private IEnumerator Run(
        List<RectTransform> shards,
        List<Image> shardImages,
        List<Vector2> shardVelocities,
        List<float> shardSpins,
        List<RectTransform> flashRts,
        List<Image> flashImages,
        float duration,
        float gravity)
    {
        float shardDuration = Mathf.Max(0.1f, duration);
        float shardHold = Mathf.Min(0.28f, shardDuration * 0.35f);
        const float flashDuration = 0.7f;
        float t = 0f;

        while (t < shardDuration)
        {
            float dt = Time.deltaTime;
            t += dt;

            for (int i = 0; i < shards.Count; i++)
            {
                var rt = shards[i];
                if (rt == null)
                    continue;

                Vector2 velocity = shardVelocities[i];
                velocity.y -= gravity * dt;
                shardVelocities[i] = velocity;
                rt.anchoredPosition += velocity * dt;
                rt.localRotation *= Quaternion.Euler(0f, 0f, shardSpins[i] * dt);

                var img = shardImages[i];
                if (img != null)
                {
                    Color c = img.color;
                    float fadeK = Mathf.Clamp01((t - shardHold) / Mathf.Max(0.01f, shardDuration - shardHold));
                    c.a = Mathf.Lerp(1f, 0f, fadeK);
                    img.color = c;
                }
            }

            float flashK = Mathf.Clamp01(t / flashDuration);
            float flashScale = Mathf.Lerp(0.65f, 2.4f, 1f - Mathf.Pow(1f - flashK, 2f));
            for (int i = 0; i < flashRts.Count; i++)
            {
                var rt = flashRts[i];
                if (rt == null)
                    continue;

                rt.localScale = Vector3.one * flashScale;
                var img = flashImages[i];
                if (img != null)
                {
                    Color c = img.color;
                    c.a = Mathf.Lerp(1f, 0f, flashK);
                    img.color = c;
                }
            }

            yield return null;
        }

        for (int i = 0; i < shards.Count; i++)
        {
            if (shards[i] != null)
                Destroy(shards[i].gameObject);
        }

        for (int i = 0; i < flashRts.Count; i++)
        {
            if (flashRts[i] != null)
                Destroy(flashRts[i].gameObject);
        }

        Destroy(gameObject);
    }
}
