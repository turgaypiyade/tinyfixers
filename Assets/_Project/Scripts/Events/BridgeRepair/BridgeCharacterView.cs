using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Köprüdeki tek karakter: kare animasyonu (yürü / eğil / kalk / sevin) + beklerken kendi başına canlılık.
/// Pivot alt-orta (ayaklar); konumu şerit (lane) belirler. Tüm kareler aynı tuvalde, ayak hizalı üretildi.
///
/// Beklerken (yalnız 2 yürüme + 3 eğilme karesiyle) rastgele aralıklarla: çekiçle vurma, zıplama,
/// geri dönüp birkaç adım yürüme, etrafa bakınma. Dış bir hareket (WalkTo/BendDown/Cheer) başlayınca
/// o anki boşta-hareket token ile iptal edilir ve poz sıfırlanır → çakışma olmaz.
/// </summary>
public sealed class BridgeCharacterView : MonoBehaviour
{
    private const float WalkFps = 7f;
    private const float BendFrameTime = 0.1f;

    private Image image;
    private BridgeCharacterDef def;
    private RectTransform rt;
    private RectTransform shadow;
    private float idleSeed;
    private bool idle = true;
    private bool acting;
    private int token;
    private Vector2 idleBase;

    public RectTransform Rect => rt;
    /// Ses ölçeği (oyuncu 1, botlar kısık). 0 → sessiz.
    public float SfxScale { get; set; } = 1f;


    public static BridgeCharacterView Create(Transform parent, BridgeCharacterDef def, float height)
    {
        var idleSprite = def != null ? def.Idle : null;
        float aspect = idleSprite != null && idleSprite.rect.height > 0f ? idleSprite.rect.width / idleSprite.rect.height : 0.85f;

        // Ayak gölgesi (karakterin arkasında, zemine yapışık).
        var shadowImg = BridgeRepairUI.Picture("Shadow", parent, BridgeRepairUI.Glow(),
            new Vector2(height * aspect * 0.85f, height * 0.16f), Vector2.zero, preserveAspect: false);
        shadowImg.color = new Color(0f, 0f, 0f, 0.35f);

        var rt = BridgeRepairUI.Rect("Character", parent, new Vector2(height * aspect, height), Vector2.zero);
        rt.pivot = new Vector2(0.5f, 0f);
        var view = rt.gameObject.AddComponent<BridgeCharacterView>();
        view.rt = rt;
        view.def = def;
        view.shadow = shadowImg.rectTransform;
        view.image = rt.gameObject.AddComponent<Image>();
        view.image.raycastTarget = false;
        view.image.preserveAspect = true;
        view.image.sprite = idleSprite;
        view.idleSeed = Random.value * 10f;
        return view;
    }

    private void OnEnable() => StartCoroutine(IdleLoop());

    private void OnDestroy()
    {
        if (shadow != null) Destroy(shadow.gameObject);
    }

    private void Update()
    {
        if (!idle || acting || rt == null) return;
        // Beklerken hafif nefes (ayak sabit, gövde esner).
        float s = Mathf.Sin((Time.unscaledTime + idleSeed) * 2.4f);
        rt.localScale = new Vector3(Facing * (1f - s * 0.012f), 1f + s * 0.018f, 1f);
    }

    private void LateUpdate()
    {
        if (shadow == null || rt == null) return;
        // Gölge zeminde kalır; karakter havalandıkça küçülür/soluklaşır.
        float lift = Mathf.Max(0f, rt.anchoredPosition.y - idleGroundY);
        float k = Mathf.Clamp01(1f - lift / Mathf.Max(1f, rt.sizeDelta.y * 0.5f));
        shadow.anchoredPosition = new Vector2(rt.anchoredPosition.x, idleGroundY + 4f);
        shadow.localScale = new Vector3(Mathf.Lerp(0.6f, 1f, k), Mathf.Lerp(0.6f, 1f, k), 1f);
    }

    private float idleGroundY;
    private int lastHammerHit = -1;
    private float Facing => rt != null && rt.localScale.x < 0f ? -1f : 1f;

    /// Zemin (ayak) yüksekliği — şerit karakteri yerleştirince çağrılır.
    public void SetGround(float y)
    {
        idleGroundY = y;
        var p = rt.anchoredPosition;
        p.y = y;
        rt.anchoredPosition = p;
    }

    /// Anında yerleştir (boşta-hareketi iptal eder).
    public void Place(float x)
    {
        Interrupt();
        rt.anchoredPosition = new Vector2(x, idleGroundY);
        SetIdle();
    }

    public void SetIdle()
    {
        idle = true;
        if (def != null) image.sprite = def.Idle;
    }

    // Dış hareket başlıyor: boşta-hareketi iptal et, pozu sıfırla.
    private void Interrupt()
    {
        token++;
        if (acting)
        {
            acting = false;
            rt.anchoredPosition = new Vector2(idleBase.x, idleGroundY);
        }
        idle = false;
        rt.localScale = Vector3.one;
    }

    // ── Dış hareketler ───────────────────────────────────────────

    /// X (lokal) konumuna yürür. speed: lokal birim/sn.
    public IEnumerator WalkTo(float targetX, float speed)
    {
        Interrupt();
        float startX = rt.anchoredPosition.x;
        float distance = Mathf.Abs(targetX - startX);
        if (distance < 0.5f) { SetIdle(); yield break; }

        float duration = distance / Mathf.Max(1f, speed);
        float t = 0f;
        // Ayak sesi klibi bir adım DİZİSİ (~1.4 sn) → yürüyüş başında bir kez, yürüyüş süresi kadar.
        if (SfxScale > 0f && duration > 0.25f)
            EventSfx.Play(x => x.footstep, SfxScale, 1f, Mathf.Min(duration, 1.4f));
        var walk = def != null ? def.walk : null;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            var p = rt.anchoredPosition;
            p.x = Mathf.Lerp(startX, targetX, k);
            p.y = idleGroundY + Mathf.Abs(Mathf.Sin(t * WalkFps * Mathf.PI)) * rt.sizeDelta.y * 0.025f;
            rt.anchoredPosition = p;
            if (walk != null && walk.Length > 0)
                image.sprite = walk[(int)(t * WalkFps) % walk.Length];
            yield return null;
        }
        rt.anchoredPosition = new Vector2(targetX, idleGroundY);
        SetIdle();
    }

    /// Eğilip köprüye uzanır (bend kareleri 0→son). Sonunda eğik kalır (StandUp ile kalkar).
    public IEnumerator BendDown(float speedMul = 1f)
    {
        Interrupt();
        var bend = def != null ? def.bend : null;
        if (bend == null || bend.Length == 0) yield break;
        for (int i = 0; i < bend.Length; i++)
        {
            image.sprite = bend[i];
            yield return Wait(BendFrameTime / speedMul);
        }
    }

    public IEnumerator StandUp(float speedMul = 1f)
    {
        var bend = def != null ? def.bend : null;
        if (bend != null)
            for (int i = bend.Length - 2; i >= 0; i--)
            {
                image.sprite = bend[i];
                yield return Wait(BendFrameTime / speedMul);
            }
        SetIdle();
    }

    /// Eğikken vuruş hissi (köprü parçası otururken küçük sıkışma).
    public IEnumerator Hammer(float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.Abs(Mathf.Sin(t / duration * Mathf.PI * 3f));
            rt.localScale = new Vector3(1f + s * 0.03f, 1f - s * 0.05f, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
    }

    /// Bitiş sevinci: iki kez zıpla.
    public IEnumerator Cheer()
    {
        Interrupt();
        if (def != null) image.sprite = def.Idle;
        for (int hop = 0; hop < 2; hop++)
            yield return Hop(0.22f, 0.32f, -1);
        SetIdle();
    }

    // ── Boşta canlılık ───────────────────────────────────────────

    private IEnumerator IdleLoop()
    {
        yield return Wait(Random.Range(0.8f, 3f));
        while (true)
        {
            float wait = Random.Range(2.5f, 6f), t = 0f;
            while (t < wait)
            {
                t = idle ? t + Time.unscaledDeltaTime : 0f;   // hareket sürerken sayaç beklesin
                yield return null;
            }

            int my = ++token;
            acting = true;
            idleBase = rt.anchoredPosition;
            rt.localScale = Vector3.one;

            float roll = Random.value;
            if (roll < 0.35f)      yield return IdleHammer(my);
            else if (roll < 0.6f)  yield return IdleShuffle(my);
            else if (roll < 0.8f)  yield return Hop(0.1f, 0.3f, my);
            else                   yield return IdleLookBack(my);

            if (token != my) continue;   // dış hareket devraldı
            acting = false;
            rt.localScale = Vector3.one;
            rt.anchoredPosition = new Vector2(idleBase.x, idleGroundY);
            if (def != null) image.sprite = def.Idle;
        }
    }

    // Diz çöküp köprüye birkaç kez vurur (bakım yapıyor gibi).
    private IEnumerator IdleHammer(int my)
    {
        var bend = def != null ? def.bend : null;
        if (bend == null || bend.Length < 2) yield break;
        for (int i = 1; i < bend.Length; i++)
        {
            if (token != my) yield break;
            image.sprite = bend[i];
            yield return Wait(0.09f);
        }
        float t = 0f;
        const float d = 0.9f;
        lastHammerHit = -1;
        while (t < d)
        {
            if (token != my) yield break;
            t += Time.unscaledDeltaTime;
            float s = Mathf.Abs(Mathf.Sin(t / d * Mathf.PI * 4f));
            int hit = (int)(t / d * 4f);
            if (hit != lastHammerHit && hit < 4)
            {
                lastHammerHit = hit;
                // Boşta çekiç yalnız oyuncuda (5 karakter aynı anda gürültü yapmasın).
                if (SfxScale >= 1f) EventSfx.Play(x => x.hammerTap, 0.5f);
            }
            rt.localScale = new Vector3(1f + s * 0.03f, 1f - s * 0.05f, 1f);
            yield return null;
        }
        rt.localScale = Vector3.one;
        for (int i = bend.Length - 2; i >= 0; i--)
        {
            if (token != my) yield break;
            image.sprite = bend[i];
            yield return Wait(0.09f);
        }
    }

    // Arkasını dönüp birkaç adım geri yürür, sonra dönüp yerine gelir.
    private IEnumerator IdleShuffle(int my)
    {
        float dist = rt.sizeDelta.x * Random.Range(0.18f, 0.3f);
        yield return IdleWalk(my, idleBase.x - dist, -1f);
        if (token != my) yield break;
        yield return Wait(0.25f);
        if (token != my) yield break;
        yield return IdleWalk(my, idleBase.x, 1f);
    }

    private IEnumerator IdleWalk(int my, float targetX, float facing)
    {
        var walk = def != null ? def.walk : null;
        float startX = rt.anchoredPosition.x;
        float dur = Mathf.Abs(targetX - startX) / Mathf.Max(1f, rt.sizeDelta.x * 0.9f);
        float t = 0f;
        rt.localScale = new Vector3(facing, 1f, 1f);
        while (t < dur)
        {
            if (token != my) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, targetX, k),
                idleGroundY + Mathf.Abs(Mathf.Sin(t * WalkFps * Mathf.PI)) * rt.sizeDelta.y * 0.02f);
            if (walk != null && walk.Length > 0) image.sprite = walk[(int)(t * WalkFps) % walk.Length];
            yield return null;
        }
        rt.anchoredPosition = new Vector2(targetX, idleGroundY);
        rt.localScale = Vector3.one;
        if (def != null) image.sprite = def.Idle;
    }

    // Arkasına bakınır (yüz çevirme), sonra geri döner.
    private IEnumerator IdleLookBack(int my)
    {
        rt.localScale = new Vector3(-1f, 1f, 1f);
        yield return Wait(Random.Range(0.7f, 1.2f));
        if (token != my) yield break;
        rt.localScale = Vector3.one;
        yield return Wait(0.35f);
        if (token != my) yield break;
        rt.localScale = new Vector3(-1f, 1f, 1f);
        yield return Wait(0.4f);
        rt.localScale = Vector3.one;
    }

    // Zıplama: height = boyun oranı. my = -1 → iptal edilemez (dış hareket).
    private IEnumerator Hop(float height, float duration, int my)
    {
        float t = 0f;
        float baseX = rt.anchoredPosition.x;
        // Kısa çömelme.
        while (t < 0.08f)
        {
            if (my >= 0 && token != my) yield break;
            t += Time.unscaledDeltaTime;
            rt.localScale = new Vector3(1.04f, 0.94f, 1f);
            yield return null;
        }
        t = 0f;
        while (t < duration)
        {
            if (my >= 0 && token != my) yield break;
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            rt.localScale = new Vector3(0.97f, 1.04f, 1f);
            rt.anchoredPosition = new Vector2(baseX, idleGroundY + Mathf.Sin(k * Mathf.PI) * rt.sizeDelta.y * height);
            yield return null;
        }
        rt.anchoredPosition = new Vector2(baseX, idleGroundY);
        rt.localScale = Vector3.one;
    }

    private static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds) { t += Time.unscaledDeltaTime; yield return null; }
    }
}
