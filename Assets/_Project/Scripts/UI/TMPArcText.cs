using TMPro;
using UnityEngine;

/// <summary>
/// TMP yazısını yay üzerinde çizer (kurdele başlıkları). arcDegrees = uçlardaki eğim açısı:
/// negatif → yay aşağı sarkar (ortası alçak, uçlar yukarı kalkar; kurdele gibi), pozitif → kubbe.
/// Her karakter yayın teğetine döner. Metin/boyut değişince yeniden bükülür.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public sealed class TMPArcText : MonoBehaviour
{
    [Range(-60f, 60f)] public float arcDegrees = -15f;

    private TMP_Text text;
    private bool warping;
    private float appliedArc = float.NaN;

    private void Awake() => text = GetComponent<TMP_Text>();

    // TMP mesh'i her yeniden kurduğunda (metin, autosize, gradient…) bükme kaybolur → olayla yeniden bük.
    private void OnEnable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        if (text != null) text.havePropertiesChanged = true;
    }

    private void OnDisable() => TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

    private void OnTextChanged(Object changed)
    {
        if (!warping && changed == text)
            Warp();
    }

    // Inspector'da açı değiştirilince.
    private void LateUpdate()
    {
        if (text != null && !Mathf.Approximately(arcDegrees, appliedArc))
            Warp();
    }

    private void Warp()
    {
        if (text == null) return;
        appliedArc = arcDegrees;
        warping = true;
        try { text.ForceMeshUpdate(); }
        finally { warping = false; }
        var info = text.textInfo;
        if (info.characterCount == 0 || Mathf.Approximately(arcDegrees, 0f))
            return;

        // Satırın yarı genişliği ↔ uçtaki açı: halfWidth = R·sin(|arc|).
        float minX = float.MaxValue, maxX = float.MinValue;
        for (int i = 0; i < info.characterCount; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;
            minX = Mathf.Min(minX, c.bottomLeft.x);
            maxX = Mathf.Max(maxX, c.topRight.x);
        }
        if (minX >= maxX) return;

        float centerX = (minX + maxX) * 0.5f;
        float halfWidth = (maxX - minX) * 0.5f;
        float arcRad = Mathf.Abs(arcDegrees) * Mathf.Deg2Rad;
        float radius = halfWidth / Mathf.Max(0.0001f, Mathf.Sin(arcRad));
        float sag = arcDegrees < 0f ? 1f : -1f;   // aşağı sarkma: uçlar yukarı

        for (int i = 0; i < info.characterCount; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;

            int mat = c.materialReferenceIndex;
            int v0 = c.vertexIndex;
            var verts = info.meshInfo[mat].vertices;

            Vector3 mid = (verts[v0] + verts[v0 + 2]) * 0.5f;
            float x = Mathf.Clamp(mid.x - centerX, -radius, radius);
            float angle = Mathf.Asin(x / radius);                  // yay üzerindeki konum açısı
            float lift = sag * radius * (1f - Mathf.Cos(angle));   // merkeze göre dikey kayma
            var rot = Quaternion.Euler(0f, 0f, sag * angle * Mathf.Rad2Deg);   // yayın teğeti

            for (int j = 0; j < 4; j++)
            {
                Vector3 local = verts[v0 + j] - mid;
                verts[v0 + j] = mid + rot * local + new Vector3(0f, lift, 0f);
            }
        }

        for (int m = 0; m < info.meshInfo.Length; m++)
        {
            var meshInfo = info.meshInfo[m];
            if (meshInfo.mesh == null) continue;
            meshInfo.mesh.vertices = meshInfo.vertices;
            text.UpdateGeometry(meshInfo.mesh, m);
        }
    }
}
