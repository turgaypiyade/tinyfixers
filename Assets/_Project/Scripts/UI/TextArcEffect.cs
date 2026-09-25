using TMPro;
using UnityEngine;

/// <summary>
/// TMP yazısını hafif yay (arc) şeklinde eğer. Font asset'ine dokunmaz; harflerin
/// mesh vertex'lerini runtime'da kaydırıp döndürür, yani her TMP fontunda (BakBak dahil) çalışır.
/// arcHeight = yayın tepe yüksekliği (yazının local birimi; UI'da ~piksel). Negatif = aşağı bükülür.
/// Satır başına ayrı hesaplanır, çok satırlı yazılarda da düzgün durur.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(TMP_Text))]
public sealed class TextArcEffect : MonoBehaviour
{
    [Header("Yay")]
    [Tooltip("Yayın tepe yüksekliği. UI'da piksel gibi düşün: 10-40 arası 'hafif yay' verir. Negatif = ters (gülümseme yerine kaş).")]
    [SerializeField] private float arcHeight = 20f;
    [Tooltip("Yay yüksekliğini satır genişliğine ve dereceye göre hesapla. Mevcut yükseklik tabanlı kullanımlar değişmez.")]
    [SerializeField] private bool useArcAngle;
    [Tooltip("Yayın iki ucundaki teğetler arasındaki toplam açı. 15 = uçlarda +7.5 / -7.5 derece.")]
    [Range(-90f, 90f)][SerializeField] private float arcAngleDegrees = 15f;
    [Tooltip("Harfler de eğime göre dönsün mü? Kapalıysa harfler dik kalır, sadece yukarı/aşağı kayar.")]
    [SerializeField] private bool rotateLetters = true;
    [Tooltip("Harf dönüşünün şiddeti (1 = yayın gerçek eğimi).")]
    [Range(0f, 2f)][SerializeField] private float rotationStrength = 1f;

    private TMP_Text text;
    private bool applying;

    private void Awake() => text = GetComponent<TMP_Text>();

    private void OnEnable()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        Apply();
    }

    private void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        if (text != null) text.ForceMeshUpdate();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        if (text == null) text = GetComponent<TMP_Text>();
        UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
    }
#endif

    private void OnTextChanged(Object obj)
    {
        if (applying || obj != text) return;
        Apply();
    }

    /// <summary>Yay yüksekliğini koddan değiştirmek için (örn. animasyon).</summary>
    public void SetArcHeight(float value)
    {
        useArcAngle = false;
        arcHeight = value;
        Apply();
    }

    /// <summary>Kısa/uzun başlıklarda aynı toplam yay açısını korur.</summary>
    public void SetArcAngle(float degrees)
    {
        useArcAngle = true;
        arcAngleDegrees = Mathf.Clamp(degrees, -90f, 90f);
        Apply();
    }

    public void Apply()
    {
        if (text == null) return;

        applying = true;
        text.ForceMeshUpdate();               // orijinal (düz) vertex'leri geri getir
        TMP_TextInfo info = text.textInfo;

        if (info == null || info.characterCount == 0
            || Mathf.Approximately(useArcAngle ? arcAngleDegrees : arcHeight, 0f))
        {
            applying = false;
            return;
        }

        for (int line = 0; line < info.lineCount; line++)
        {
            TMP_LineInfo lineInfo = info.lineInfo[line];
            float lineLeft = lineInfo.lineExtents.min.x;
            float lineWidth = lineInfo.lineExtents.max.x - lineLeft;
            if (lineWidth <= 0.0001f) continue;
            // For this parabola the endpoint slope is +/-4h / width.
            // Derive h from half the requested total angle, independent of font/line width.
            float lineArcHeight = useArcAngle
                ? lineWidth * 0.25f * Mathf.Tan(arcAngleDegrees * 0.5f * Mathf.Deg2Rad)
                : arcHeight;

            for (int i = lineInfo.firstCharacterIndex; i <= lineInfo.lastCharacterIndex; i++)
            {
                TMP_CharacterInfo ch = info.characterInfo[i];
                if (!ch.isVisible) continue;

                int vi = ch.vertexIndex;
                Vector3[] verts = info.meshInfo[ch.materialReferenceIndex].vertices;

                // Harfin yatay orta noktası -> 0..1 arası konum
                float mid = (verts[vi].x + verts[vi + 2].x) * 0.5f;
                float t = Mathf.Clamp01((mid - lineLeft) / lineWidth);

                // Parabol: uçlarda 0, ortada arcHeight
                float d = t - 0.5f;
                float offsetY = lineArcHeight * (1f - 4f * d * d);

                // Eğim (türev) -> harf dönüş açısı
                float angle = 0f;
                if (rotateLetters)
                {
                    float slope = (-8f * d * lineArcHeight) / lineWidth;
                    angle = Mathf.Atan(slope) * Mathf.Rad2Deg * rotationStrength;
                }

                // Harfin taban-orta noktası etrafında döndür
                Vector3 pivot = new Vector3(mid, ch.baseLine, 0f);
                Matrix4x4 m = Matrix4x4.TRS(new Vector3(0f, offsetY, 0f), Quaternion.Euler(0f, 0f, angle), Vector3.one);

                for (int v = 0; v < 4; v++)
                    verts[vi + v] = m.MultiplyPoint3x4(verts[vi + v] - pivot) + pivot;
            }
        }

        text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        applying = false;
    }
}
