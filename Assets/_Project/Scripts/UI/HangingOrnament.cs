using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// PreLevel popup'ında başlık şeridine çakılı çividen ipiyle sarkan alet demeti (kullanıcı 2026-11-13).
/// Popup açılınca sönümlü sarkaç gibi sallanıp durur; dokununca yeniden sallanır. Sürekli animasyon YOK
/// (performans kuralı): coroutine yalnız sallanırken çalışır, kapanınca durur ve açı sıfırlanır.
/// Çeşitlilik (kullanıcı 2026-11-13): her açılışta süs rastgele değişir — alet demeti ya da fener.
/// Görseller: Resources/PreLevelOrnament/<süs> (ip halkası tepede), NailHead. Sesler süs başına (yoksa alet demetinin
/// sesleri): <süs>Swing / <süs>Tap — merkezi GameEventSfx.PlayUiClip (Ayarlar'da ses kapalıysa çalmaz).
/// </summary>
public sealed class HangingOrnament : MonoBehaviour, IPointerClickHandler
{
    private const float OpenAmplitude = 14f;   // derece
    private const float TapAmplitude = 10f;
    private const float Frequency = 1.1f;      // salınım / sn
    private const float Damping = 1.7f;        // büyüdükçe çabuk durur
    private const float Duration = 2.4f;
    private const float OpenDelay = 0.3f;      // popup girişi otursun
    // Süsler: görsel adı, ip halkasının tepesi (normalize; sarkacın dönme noktası), ses ön eki, boy çarpanı.
    private struct Kind { public string sprite, sfx; public Vector2 pivot; public float heightScale; }
    private static readonly Kind[] Kinds =
    {
        new Kind { sprite = "ToolBundle", sfx = "Ornament", pivot = new Vector2(0.531f, 0.995f), heightScale = 1f },
        new Kind { sprite = "Lantern",    sfx = "Lantern",  pivot = new Vector2(0.504f, 0.995f), heightScale = 1.15f },
    };

    private RectTransform swing;
    private Image image;
    private Coroutine routine;
    private float baseHeight, nailSize;
    private AudioClip swingClip, tapClip;

    /// <summary><paramref name="nailPos"/>: çivinin <paramref name="parent"/> içindeki yeri. Görsel yoksa null.</summary>
    public static HangingOrnament Create(RectTransform parent, Vector2 nailPos, float toolHeight, float nailSize)
    {
        var toolSprite = Resources.Load<Sprite>("PreLevelOrnament/" + Kinds[0].sprite);
        var nailSprite = Resources.Load<Sprite>("PreLevelOrnament/NailHead");
        if (parent == null || toolSprite == null) return null;

        int layer = parent.gameObject.layer;   // UI kamerası layer 0'ı çizmez
        var root = NewRect("HangingOrnament", parent, layer);
        root.anchorMin = root.anchorMax = root.pivot = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;
        root.anchoredPosition = nailPos;

        var swing = NewRect("Swing", root, layer);
        swing.anchorMin = swing.anchorMax = swing.pivot = new Vector2(0.5f, 0.5f);
        swing.sizeDelta = Vector2.zero;

        // İp halkası çivinin üst yarısına takılı görünsün: halka tepesi çivi merkezinin biraz üstünde.
        var tool = NewRect("Tools", swing, layer).gameObject.AddComponent<Image>();
        tool.sprite = toolSprite;
        tool.preserveAspect = true;
        tool.raycastTarget = true;   // dokunuş üstteki HangingOrnament'a çıkar
        var tr = tool.rectTransform;
        tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
        tr.pivot = Kinds[0].pivot;   // Apply() süse göre yeniden ayarlar
        tr.sizeDelta = new Vector2(toolHeight * toolSprite.rect.width / toolSprite.rect.height, toolHeight);
        tr.anchoredPosition = new Vector2(0f, nailSize * 0.3f);

        if (nailSprite != null)
        {
            var nail = NewRect("Nail", root, layer).gameObject.AddComponent<Image>();
            nail.sprite = nailSprite;
            nail.preserveAspect = true;
            nail.raycastTarget = false;
            nail.rectTransform.sizeDelta = new Vector2(nailSize, nailSize);
        }

        var ornament = root.gameObject.AddComponent<HangingOrnament>();
        ornament.swing = swing;
        ornament.image = tool;
        ornament.baseHeight = toolHeight;
        ornament.nailSize = nailSize;
        ornament.Apply(0);
        return ornament;
    }

    // Süs görselini, dönme noktasını ve seslerini uygular (görseli olmayan süs atlanır).
    private void Apply(int index)
    {
        var kind = Kinds[index];
        var sprite = Resources.Load<Sprite>("PreLevelOrnament/" + kind.sprite);
        if (sprite == null) { if (index != 0) Apply(0); return; }
        image.sprite = sprite;
        var tr = image.rectTransform;
        float h = baseHeight * kind.heightScale;
        tr.pivot = kind.pivot;
        tr.sizeDelta = new Vector2(h * sprite.rect.width / sprite.rect.height, h);
        tr.anchoredPosition = new Vector2(0f, nailSize * 0.3f);
        swingClip = Resources.Load<AudioClip>("PreLevelOrnament/" + kind.sfx + "Swing");
        tapClip = Resources.Load<AudioClip>("PreLevelOrnament/" + kind.sfx + "Tap");
        if (swingClip == null) swingClip = Resources.Load<AudioClip>("PreLevelOrnament/OrnamentSwing");
        if (tapClip == null) tapClip = Resources.Load<AudioClip>("PreLevelOrnament/OrnamentTap");
    }

    private static RectTransform NewRect(string name, Transform parent, int layer)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        return rt;
    }

    private void OnEnable()
    {
        if (swing == null) return;
        Apply(Random.Range(0, Kinds.Length));   // her açılışta rastgele süs
        Swing(OpenAmplitude, OpenDelay, swingClip);
    }

    private void OnDisable()
    {
        routine = null;   // coroutine'ler devre dışı kalınca zaten durur
        if (swing != null) swing.localRotation = Quaternion.identity;
    }

    public void OnPointerClick(PointerEventData eventData) => Swing(TapAmplitude, 0f, tapClip);

    private void Swing(float amplitude, float delay, AudioClip sound)
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(Run(amplitude, delay, sound));
    }

    private IEnumerator Run(float amplitude, float delay, AudioClip sound)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
        GameEventSfx.PlayUiClip(sound);   // null ise / ses kapalıysa çalmaz
        for (float t = 0f; t < Duration; t += Time.unscaledDeltaTime)
        {
            float angle = amplitude * Mathf.Exp(-Damping * t) * Mathf.Sin(2f * Mathf.PI * Frequency * t);
            swing.localRotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }
        swing.localRotation = Quaternion.identity;
        routine = null;
    }
}
