using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Ödül/hediye başlıklarının ortak girişi (kullanıcı 2026-11-18, Royal Kingdom/Match referansı): harfler sırayla doğar,
/// önce dar-uzun, sonra ezilip yerine oturur ("domino" hissi). Yalnız giriş süresince vertex günceller, sonra durur
/// (sürekli animasyon yok). TMP metni ayarlandıktan SONRA çağır.
/// </summary>
public static class TitleLetterPop
{
    private const float LetterStagger = 0.045f;
    private const float LetterDuration = 0.32f;

    // Harfler sırayla: yatayda dar + dikeyde uzun doğar, ters yönde ezilip yerine oturur (domino hissi).
    public static IEnumerator Play(TMP_Text text)
    {
        text.ForceMeshUpdate();
        var info = text.textInfo;
        int count = info.characterCount;
        var source = new Vector3[info.meshInfo.Length][];
        for (int m = 0; m < info.meshInfo.Length; m++) source[m] = (Vector3[])info.meshInfo[m].vertices.Clone();

        float total = (count - 1) * LetterStagger + LetterDuration;
        for (float t = 0f; ; t += Time.unscaledDeltaTime)
        {
            bool done = t >= total;
            for (int i = 0; i < count; i++)
            {
                var c = info.characterInfo[i];
                if (!c.isVisible) continue;
                int m = c.materialReferenceIndex, v = c.vertexIndex;
                var src = source[m];
                var dst = info.meshInfo[m].vertices;
                float k = done ? 1f : Mathf.Clamp01((t - i * LetterStagger) / LetterDuration);
                Vector2 scale = LetterScale(k);
                Vector3 pivot = new Vector3((src[v].x + src[v + 2].x) * 0.5f, c.baseLine, 0f);
                for (int j = 0; j < 4; j++)
                {
                    Vector3 p = src[v + j] - pivot;
                    dst[v + j] = pivot + new Vector3(p.x * scale.x, p.y * scale.y, 0f);
                }
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
            if (done) yield break;
            yield return null;
        }
    }

    // 0 → görünmez, ~0.45 → dar/uzun (1.0x, 1.35y)... ~0.7 → ezik (1.2x, 0.8y), 1 → (1,1).
    private static Vector2 LetterScale(float k)
    {
        if (k <= 0f) return Vector2.zero;
        if (k < 0.45f) { float a = k / 0.45f; return new Vector2(Mathf.Lerp(0.2f, 0.9f, a), Mathf.Lerp(0.2f, 1.35f, a)); }
        if (k < 0.7f) { float a = (k - 0.45f) / 0.25f; return new Vector2(Mathf.Lerp(0.9f, 1.2f, a), Mathf.Lerp(1.35f, 0.8f, a)); }
        float b = (k - 0.7f) / 0.3f;
        return new Vector2(Mathf.Lerp(1.2f, 1f, b), Mathf.Lerp(0.8f, 1f, b));
    }

}
