using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;


public class PulseCoreVfxPlayer : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private RectTransform vfxRoot;
    [FormerlySerializedAs("pulseVfxPrefab")][SerializeField] private RectTransform pulsePrefab;
    [SerializeField] private RectTransform lightningPrefab;
    [SerializeField] private Sprite lightningFallbackSprite;
    [SerializeField] private Vector2 lightningFallbackSize = new Vector2(180f, 180f);


    public RectTransform VfxRoot => vfxRoot;

    [Header("Teleport VFX")]
    public RectTransform TeleportMarkerPrefab; // VFX_TeleportMarker_UI prefab (RectTransform)

    [Header("Tuning")]
    [SerializeField] private float expandTime = 0.35f;       // Halkanın yayılma hızı
    [SerializeField] private float coreBurstDuration = 0.2f; // Patlamanın ekranda kalma süresi

    [ContextMenu("TEST/Play Pulse At Center")]
    public void PlayTestAtCenter()
    {
        if (vfxRoot != null && pulsePrefab != null)
            StartCoroutine(Play(Vector2.zero, 2, 110));
    }

    public void PlayPulseVfx(Vector2 centerLocalPos, int radiusCells, int tileSize)
    {
        if (vfxRoot == null || pulsePrefab == null)
            return;

        StartCoroutine(Play(centerLocalPos, radiusCells, tileSize));
    }

    public void PlayLightningAtTile(TileView tile, float duration)
    {
        if (tile == null || vfxRoot == null || lightningPrefab == null)
            return;

        var tileRect = tile.GetComponent<RectTransform>();
        if (tileRect == null)
            return;

        var worldPos = tileRect.TransformPoint(tileRect.rect.center);
        var localPos = (Vector2)vfxRoot.InverseTransformPoint(worldPos);

        var lightningVfx = Instantiate(lightningPrefab, vfxRoot);
        lightningVfx.anchoredPosition = localPos;
        lightningVfx.localScale = Vector3.one;
        lightningVfx.SetAsLastSibling();

        EnsureLightningVisual(lightningVfx);
        StartCoroutine(DestroyAfterDuration(lightningVfx, Mathf.Max(0.12f, duration)));
    }

    void EnsureLightningVisual(RectTransform lightningVfx)
    {
        if (lightningVfx == null)
            return;

        var image = lightningVfx.GetComponentInChildren<Image>(true);
        if (image != null)
            return;

        var spriteRenderer = lightningVfx.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null && spriteRenderer.sprite != null)
            return;

        if (lightningFallbackSprite == null)
            return;

        var fallbackImage = lightningVfx.GetComponent<Image>();
        if (fallbackImage == null)
            fallbackImage = lightningVfx.gameObject.AddComponent<Image>();

        fallbackImage.sprite = lightningFallbackSprite;
        fallbackImage.preserveAspect = true;
        fallbackImage.color = Color.white;
        lightningVfx.sizeDelta = lightningFallbackSize;
    }

    IEnumerator Play(Vector2 centerLocalPos, int radiusCells, int tileSize)
    {
        var vfx = Instantiate(pulsePrefab, vfxRoot);
        vfx.anchoredPosition = centerLocalPos;

        var refs = vfx.GetComponent<PulseCoreVfxRefs>();
        if (refs == null) { Destroy(vfx.gameObject); yield break; }

        var ring = refs.ring;
        var glow = refs.glow;
        var shock = refs.shockwave;
        var core = refs.coreBurst;

        // --- 1. AYARLAR (ZORUNLU KISIMLAR) ---
        
        // Patlama en önde olsun ama artık çok büyük olmayacak
        core.transform.SetAsLastSibling();

        // 512x512 Zorlaması (Bunu koruyoruz, görünmezlik sorununu bu çözdü)
        Vector2 forceSize = new Vector2(512f, 512f);
        core.rectTransform.sizeDelta = forceSize;
        ring.rectTransform.sizeDelta = forceSize;

        // Renkler ve Görünürlük
        core.color = Color.white;
        ring.color = Color.white;
        SetAlpha(core, 1f);
        SetAlpha(ring, 1f);
        SetAlpha(glow, 0.75f);
        SetAlpha(shock, 0.85f);

        // --- 2. ESTETİK AYARLAR (BURASI DEĞİŞTİ) ---
        
        // Root (Ana kutu) küçük başlasın
        vfx.localScale = Vector3.one * 0.2f; 
        
        // *** DÜZELTME BURADA ***
        // Eskiden 2.5f yapıyorduk, o yüzden halkayı kapatıyordu.
        // Şimdi 1.2f yapıyoruz. Patlama sadece merkeze odaklı olacak.
        core.rectTransform.localScale = Vector3.one * 1.6f; 
        
        shock.rectTransform.localScale = Vector3.one * 0.15f;

        // Hedef Hesaplamaları
        int sizeCells = radiusCells * 2 + 1;
        float targetSizePx = sizeCells * tileSize;
        float spriteRef = (ring.sprite != null) ? ring.sprite.rect.width : 256f;
        float targetScale = targetSizePx / spriteRef;

        // Unity'nin boyutları algılaması için 1 kare mola (Yarış durumunu önler)
        yield return null; 

        // --- 3. ANİMASYON ---
        
        // Patlamayı başlat
        StartCoroutine(PlayCoreBurst(core, coreBurstDuration));

        // Halka Büyüme Döngüsü
        float t = 0f;
        while (t < expandTime)
        {
            t += Time.deltaTime;
            // EaseOutBack: Halka büyürken hafifçe dışarılaşıp yerine oturur (Daha canlı hissettirir)
            // Eğer çok zıplak gelirse 'EaseOutCubic' yapabilirsin.
            float k = EaseOutCubic(Mathf.Clamp01(t / expandTime));

            // Büyüme (0.2 -> Hedef)
            float s = Mathf.Lerp(0.2f, targetScale, k);
            vfx.localScale = Vector3.one * s;

            // Fade (Halkalar sonlara doğru sönmeye başlasın)
            float fadeStart = 0.4f; 
            float fk = Mathf.Clamp01(Mathf.InverseLerp(fadeStart, 1f, k));
            
            SetAlpha(ring, Mathf.Lerp(1f, 0f, fk));
            SetAlpha(glow, Mathf.Lerp(0.75f, 0f, fk));

            // Shockwave halkadan daha hızlı yayılsın
            float shockScale = Mathf.Lerp(0.15f, targetScale * 1.6f, k);
            shock.rectTransform.localScale = Vector3.one * shockScale;
            SetAlpha(shock, Mathf.Lerp(0.85f, 0f, k));

            yield return null;
        }

        Destroy(vfx.gameObject);
    }

    private Vector2 WorldToVfxAnchored(Vector3 worldPos)
    {
        var vfxRootRt = (RectTransform)VfxRoot;
        var canvas = vfxRootRt.GetComponentInParent<Canvas>();
        var cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(vfxRootRt, screen, cam, out var local);
        return local;
    }

    public void PlayTeleportMarkers(Vector3 fromWorld, Vector3 toWorld)
    {
        if (TeleportMarkerPrefab == null || VfxRoot == null) return;

        SpawnTeleportMarker(fromWorld, TeleportMarkerAnim.Mode.In);
        SpawnTeleportMarker(toWorld, TeleportMarkerAnim.Mode.Out);
    }

    private void SpawnTeleportMarker(Vector3 worldPos, TeleportMarkerAnim.Mode mode)
    {
        var inst = Instantiate(TeleportMarkerPrefab, VfxRoot);
        inst.anchoredPosition = WorldToVfxAnchored(worldPos);

        var anim = inst.GetComponent<TeleportMarkerAnim>();
        if (anim != null) anim.mode = mode;
    }

    IEnumerator DestroyAfterDuration(RectTransform target, float duration)
    {
        if (target == null) yield break;
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        if (target != null)
            Destroy(target.gameObject);
    }

    IEnumerator PlayCoreBurst(Image core, float duration)
    {
        core.gameObject.SetActive(true);
        SetAlpha(core, 1f);
        core.rectTransform.sizeDelta = new Vector2(512f, 512f); // Garanti

        Vector3 startScale = core.rectTransform.localScale; // 1.2f
        
        // Punch: Patlama anında hafifçe şişsin
        core.rectTransform.localScale = startScale * 1.5f; 

        // Hold: Çok kısa bekle (Göz görsün ama halkayı kapatmasın)
        yield return new WaitForSeconds(0.08f);

        // Fade Out: Patlama hızlıca sönmeli ki arkadan gelen halka görünsün
        float t = 0f;
        while (t < duration)
        {
            if (core == null) yield break;
            t += Time.deltaTime;
            float k = t / duration;

            SetAlpha(core, Mathf.Lerp(1f, 0f, k)); // Hızla sön
            // Sönerken biraz küçülsün (İçe çökme efekti)
            core.rectTransform.localScale = Vector3.Lerp(startScale * 1.3f, startScale * 0.5f, k);
            
            yield return null;
        }
    }

    void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        Color c = img.color;
        c.a = a;
        img.color = c;
    }

    float EaseOutCubic(float x) { return 1f - Mathf.Pow(1f - x, 3); }
}
