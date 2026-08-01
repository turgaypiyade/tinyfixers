using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;
public sealed class TileAnimator
{
    private static readonly Vector2 CenterPivot = new Vector2(0.5f, 0.5f);

    private readonly BoardController board;

    public TileAnimator(BoardController board)
    {
        this.board = board;
    }

    // ============================================================
    // ROYAL MATCH TARZI CLEAR BURST
    // Taş küçülür + halka (glow ring) açılır + yıldızlar dans eder + altın shardlar saçılır.
    // Burst VFX TileClearBurstVfx sınıfı tarafından üretilir (runtime UI, prefab gerekmez).
    //
    // Hissiyat ayarları (hızlandırıldı):
    //   BURST_DURATION = 0.06s (PlayPop coroutine bekleme, burst arkada devam eder)
    //   Burst kendisi arka planda 0.30s yaşamaya devam eder (fire-and-forget),
    //   sadece PlayPop'un coroutine beklemesi 0.08s → clear anim blokaj azaldı.
    // ============================================================
    private const float BURST_DURATION = 0.06f;   // Taş küçülme süresi (PlayPop coroutine bekleme)
    private const float TILE_SHRINK_END = 0.00f;  // Taş scale sonu
    private const float TILE_SHRINK_MID = 0.55f;  // Taş scale orta (shrink hissini verir)
    private const float BURST_VFX_DURATION = 0.30f; // Halka/yıldız/shard yaşam süresi (paralel)

    public IEnumerator PlayPop(TileView tile, float duration, bool suppressBurst = false)
    {
        if (tile == null || !tile)
            yield break;

        Transform root;
        RectTransform rt;

        try
        {
            root = tile.transform;
            rt = tile.RectTransform;
        }
        catch (MissingReferenceException)
        {
            yield break;
        }

        if (root == null || rt == null)
            yield break;

        CanvasGroup canvasGroup = null;
        Vector2 originalPivot = CenterPivot;

        try
        {
            canvasGroup = tile.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = tile.gameObject.AddComponent<CanvasGroup>();

            originalPivot = rt.pivot;

            if (rt.pivot != CenterPivot)
                SetPivotWithoutVisualJump(rt, CenterPivot);

            canvasGroup.alpha = 1f;
        }
        catch (MissingReferenceException)
        {
            yield break;
        }

        // Burst VFX'i paralel tetikle (kendi life'ını yaşar, PlayPop bekleme zorunda değil)
        // Fire-and-forget: burst 300ms yaşar ama PlayPop sadece 120ms blokluyor
        // suppressBurst: Override+Override radial wave gibi board-wide bir efekt zaten patlamayı
        // gösteriyorsa, her hücrede ayrı halka/yıldız/shard daireleri istemiyoruz.
        if (board != null && !suppressBurst)
        {
            board.StartCoroutine(TileClearBurstVfx.CoPlayBurst(tile, board, BURST_VFX_DURATION));
        }

        // Taş animasyonu — çağıran taraftaki duration parametresini dikkate al
        // (cascade sırasında farklı sürede oynatmak isteyebilir)
        float shrinkDuration = Mathf.Clamp(duration, 0.045f, BURST_DURATION);
        float t = 0f;

        while (t < shrinkDuration)
        {
            if (tile == null || !tile || root == null) yield break;

            try
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, shrinkDuration));

                // 2 fazlı scale: 1.0 → 0.55 → 0.0
                // Faz 1 (0-0.4): 1.0 → 0.55 (hızlı küçülme, burst ile senkronize)
                // Faz 2 (0.4-1.0): 0.55 → 0.0 (yavaş kayboluş)
                float scale;
                if (k < 0.4f)
                {
                    float kk = k / 0.4f;
                    float eased = 1f - (1f - kk) * (1f - kk); // easeOutQuad
                    scale = Mathf.Lerp(1f, TILE_SHRINK_MID, eased);
                }
                else
                {
                    float kk = (k - 0.4f) / 0.6f;
                    scale = Mathf.Lerp(TILE_SHRINK_MID, TILE_SHRINK_END, kk);
                }

                root.localScale = new Vector3(scale, scale, 1f);

                // Alpha: ilk %60 tam opak, sonra hızlı solma
                if (canvasGroup != null)
                {
                    float alpha = (k < 0.6f) ? 1f : 1f - (k - 0.6f) / 0.4f;
                    canvasGroup.alpha = Mathf.Clamp01(alpha);
                }
            }
            catch (MissingReferenceException)
            {
                yield break;
            }

            yield return null;
        }

        try
        {
            if (root != null)
            {
                root.localScale = Vector3.zero;
                root.localRotation = Quaternion.identity;
            }

            if (canvasGroup != null)
                canvasGroup.alpha = 0f;

            if (rt != null && rt)
            {
                if (rt.pivot != originalPivot)
                    SetPivotWithoutVisualJump(rt, originalPivot);
            }
        }
        catch (MissingReferenceException)
        {
            yield break;
        }
    }

    public IEnumerator PlayLightningStrikeAndShrink(TileView tile, float duration, Color lightningColor)
    {
        if (tile == null) yield break;

        Image iconImage = tile.IconImage;
        if (iconImage == null)
        {
            yield return PlayPop(tile, duration);
            yield break;
        }

        Transform root = tile.transform;
        Color baseColor = iconImage.color;

        float flashTime = Mathf.Min(0.05f, duration * 0.30f);
        float impactTime = Mathf.Min(0.04f, duration * 0.25f);
        float t = 0f;

        // 1) flash
        while (t < flashTime)
        {
            if (tile == null || iconImage == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, flashTime));
            iconImage.color = Color.Lerp(baseColor, lightningColor, k);
            yield return null;
        }

        // 2) kısa sert punch
        t = 0f;
        while (t < impactTime)
        {
            if (tile == null || root == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, impactTime));
            float s = Mathf.Lerp(1f, 1.14f, k);
            root.localScale = new Vector3(s, 1f - (s - 1f) * 0.65f, 1f);
            yield return null;
        }

        if (iconImage != null)
            iconImage.color = baseColor;

        // 3) shrink out
        float shrinkDuration = Mathf.Max(0.04f, duration - flashTime - impactTime);
        t = 0f;
        Vector3 start = root != null ? root.localScale : Vector3.one;
        Vector3 end = Vector3.zero;

        while (t < shrinkDuration)
        {
            if (tile == null || root == null || iconImage == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, shrinkDuration));
            float eased = k * k;

            root.localScale = Vector3.Lerp(start, end, eased);
            root.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 18f, eased));

            var c = iconImage.color;
            c.a = Mathf.Lerp(baseColor.a, 0f, eased);
            iconImage.color = c;

            yield return null;
        }

        if (root != null)
        {
            root.localScale = end;
            root.localRotation = Quaternion.identity;
        }

        if (iconImage != null)
        {
            var finalColor = iconImage.color;
            finalColor.a = 0f;
            iconImage.color = finalColor;
        }
    }

    public void PlaySelectionPulse(
        TileView tile,
        float delay = 0f,
        float peakScale = 1.12f,
        float upTime = 0.06f,
        float downTime = 0.08f)
    {
        if (tile == null || board == null) return;
        board.StartCoroutine(CoSelectionPulse(tile, delay, peakScale, upTime, downTime));
    }

    private IEnumerator CoSelectionPulse(
        TileView tile,
        float delay,
        float peakScale,
        float upTime,
        float downTime)
    {
        if (tile == null) yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile == null) yield break;

        Transform tr = GetVisualTarget(tile);
        if (tr == null) yield break;

        Vector3 baseScale = tr.localScale;
        float peak = Mathf.Max(1f, peakScale);
        Vector3 targetScale = baseScale * peak;

        float t = 0f;
        float upDur = Mathf.Max(0.0001f, upTime);
        while (t < upDur)
        {
            if (tile == null || tr == null) yield break;

            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / upDur);
            float e = 1f - (1f - a) * (1f - a); // easeOutQuad
            tr.localScale = Vector3.LerpUnclamped(baseScale, targetScale, e);
            yield return null;
        }

        t = 0f;
        float downDur = Mathf.Max(0.0001f, downTime);
        while (t < downDur)
        {
            if (tile == null || tr == null) yield break;

            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / downDur);
            float e = a * a; // easeInQuad
            tr.localScale = Vector3.LerpUnclamped(targetScale, baseScale, e);
            yield return null;
        }

        if (tr != null)
            tr.localScale = baseScale;
    }

    public IEnumerator PlayPulseImpact(TileView tile, float delay, float totalTime)
    {
        if (tile == null) yield break;

        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (tile == null) yield break;

        RectTransform rt = tile.RectTransform;
        if (rt == null) yield break;

        CanvasGroup g = tile.GetComponent<CanvasGroup>();
        if (g == null)
            g = tile.gameObject.AddComponent<CanvasGroup>();

        Vector3 start = rt.localScale;
        Vector3 up = start * 1.08f;
        Vector3 down = start * 0.90f;

        float t = 0f;
        float half = totalTime * 0.45f;

        while (t < half)
        {
            if (tile == null || rt == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, half));
            rt.localScale = Vector3.Lerp(start, up, k);
            yield return null;
        }

        t = 0f;
        float backDur = Mathf.Max(0.0001f, totalTime - half);
        while (t < backDur)
        {
            if (tile == null || rt == null) yield break;

            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / backDur);
            rt.localScale = Vector3.Lerp(up, down, k);
            g.alpha = Mathf.Lerp(1f, 0f, k);
            yield return null;
        }
    }


    public IEnumerator PlaySpecialCreationMerge(TileView createdTile, IEnumerable<TileView> sourceTiles, float duration)
    {
        if (createdTile == null)
            yield break;

        Image createdIcon = createdTile.IconImage;
        RectTransform createdIconRt = createdIcon != null ? createdIcon.rectTransform : null;
        if (createdIcon == null || createdIconRt == null)
        {
            RestoreTileVisualState(createdTile);
            yield break;
        }

        CanvasGroup createdGroup = createdTile.GetComponent<CanvasGroup>();
        if (createdGroup == null)
            createdGroup = createdTile.gameObject.AddComponent<CanvasGroup>();

        // FIX:
        // Special oluşurken doğru special layout'u baştan garantiye al.
        // Normal tile'dan kalan FillCell / stretch state'i baseScale'e karışmasın.
        if (board != null)
            createdTile.ApplyTileSize(board.TileSize);

        // Layout sonrası tekrar referansı alalım; aynı obje olur ama güvenli.
        createdIcon = createdTile.IconImage;
        createdIconRt = createdIcon != null ? createdIcon.rectTransform : null;
        if (createdIcon == null || createdIconRt == null)
        {
            RestoreTileVisualState(createdTile);
            yield break;
        }

        // Board yoksa basit pop fallback.
        if (board == null || board.Parent == null)
        {
            yield return PlayCreatedSpecialAppearOnly(createdTile, duration);
            yield break;
        }

        RectTransform ghostParent = board.Parent;
        Vector2 targetPos = GetRectCenterInParentSpace(ghostParent, createdIconRt);

        // ── Katkı taşları için ghost'lar (kaynak ikonları gizlenir) ──
        var ghosts = new List<SpecialCreationGhostState>();
        var seen = new HashSet<TileView>();
        if (sourceTiles != null)
        {
            foreach (var src in sourceTiles)
            {
                if (src == null || src == createdTile || !seen.Add(src))
                    continue;

                Image srcIcon = src.IconImage;
                RectTransform srcIconRt = srcIcon != null ? srcIcon.rectTransform : null;
                if (srcIcon == null || srcIconRt == null || srcIcon.sprite == null)
                    continue;

                GameObject ghostGo = new GameObject(
                    "SpecialGatherGhost",
                    typeof(RectTransform), typeof(CanvasGroup), typeof(Image));

                RectTransform ghostRt = ghostGo.GetComponent<RectTransform>();
                ghostRt.SetParent(ghostParent, false);
                ghostRt.anchorMin = CenterPivot;
                ghostRt.anchorMax = CenterPivot;
                ghostRt.pivot = CenterPivot;
                ghostRt.SetAsLastSibling();
                ghostRt.sizeDelta = GetRectSizeInParentSpace(srcIconRt, ghostParent);
                ghostRt.anchoredPosition = GetRectCenterInParentSpace(ghostParent, srcIconRt);
                ghostRt.localScale = Vector3.one;
                ghostRt.localRotation = Quaternion.identity;

                Image ghostImg = ghostGo.GetComponent<Image>();
                ghostImg.sprite = srcIcon.sprite;
                ghostImg.type = srcIcon.type;
                ghostImg.preserveAspect = srcIcon.preserveAspect;
                ghostImg.material = srcIcon.material;
                ghostImg.raycastTarget = false;
                ghostImg.color = srcIcon.color;

                CanvasGroup ghostGroup = ghostGo.GetComponent<CanvasGroup>();
                ghostGroup.alpha = srcIcon.color.a;

                ghosts.Add(new SpecialCreationGhostState
                {
                    tile = src,
                    sourceImage = srcIcon,
                    sourceColor = srcIcon.color,
                    ghostRect = ghostRt,
                    ghostGroup = ghostGroup,
                    startPos = ghostRt.anchoredPosition
                });

                Color hidden = srcIcon.color; hidden.a = 0f;
                srcIcon.color = hidden;
            }
        }

        // Katkı taşı yoksa basit pop.
        if (ghosts.Count == 0)
        {
            yield return PlayCreatedSpecialAppearOnly(createdTile, duration);
            yield break;
        }

        Vector3 baseScale = createdIconRt.localScale;
        Color baseColor = createdIcon.color;

        // ── Anchor ghost: swap taşının ORİJİNAL (special'sız) hali, hedefte bekler.
        // Match aynı renk olduğundan bir katkı taşının sprite'ı = orijinal görsel.
        // ÖLÇÜM createdIconRt küçültülmeden ÖNCE yapılır. Kozmetik → try/catch ile board asla kilitlenmez.
        RectTransform anchorGhostRt = null;
        try
        {
            Image rep = null; Color repColor = Color.white;
            for (int gi = 0; gi < ghosts.Count; gi++)
            {
                if (ghosts[gi].sourceImage != null && ghosts[gi].sourceImage && ghosts[gi].sourceImage.sprite != null)
                { rep = ghosts[gi].sourceImage; repColor = ghosts[gi].sourceColor; break; }
            }

            if (rep != null)
            {
                GameObject aGo = new GameObject(
                    "SpecialAnchorGhost",
                    typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
                anchorGhostRt = aGo.GetComponent<RectTransform>();
                anchorGhostRt.SetParent(ghostParent, false);
                anchorGhostRt.anchorMin = CenterPivot;
                anchorGhostRt.anchorMax = CenterPivot;
                anchorGhostRt.pivot = CenterPivot;
                anchorGhostRt.SetAsLastSibling(); // katkılar bunun ALTINDA toplanır
                anchorGhostRt.sizeDelta = GetRectSizeInParentSpace(createdIconRt, ghostParent);
                anchorGhostRt.anchoredPosition = targetPos;
                anchorGhostRt.localScale = Vector3.one;
                anchorGhostRt.localRotation = Quaternion.identity;

                Image aImg = aGo.GetComponent<Image>();
                aImg.sprite = rep.sprite;
                aImg.type = rep.type;
                aImg.preserveAspect = rep.preserveAspect;
                aImg.material = rep.material;
                aImg.raycastTarget = false;
                aImg.color = new Color(repColor.r, repColor.g, repColor.b, 1f);
                aGo.GetComponent<CanvasGroup>().alpha = 1f;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[SpecialCreationMerge] anchor ghost skipped: {e.Message}");
            anchorGhostRt = null;
        }

        // Gerçek special gather boyunca GİZLİ (hemen çıkmasın); anchor ghost onun yerine bekler.
        createdTile.transform.localScale = Vector3.one;
        createdTile.transform.localRotation = Quaternion.identity;
        createdGroup.alpha = 0f;
        createdIconRt.localScale = baseScale * 0.18f;
        createdIconRt.localRotation = Quaternion.identity;
        createdIcon.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        float gatherDuration = Mathf.Max(0.28f, duration); // görünür toplanma (arka planda, board'u durdurmaz)
        float flashDuration = 0.05f;
        float settleDuration = 0.035f;
        float pulseScale = 1.16f;

        // PARALEL: gather+reveal+settle ARKA PLANDA (fire-and-forget). PlaySpecialCreationMerge
        // hemen döner → formation biter, delikler açılır, board HEMEN düşüşe geçer. Ghost'lar
        // (bağımsız kopya) taşlar düşerken merkeze toplanıp special'ı YOLDA oluşturur.
        // Saf görsel coroutine — background-job sayacına dokunmaz (hang riski yok).
        if (board != null)
            board.StartCoroutine(CoAnimateSpecialGather(
                createdTile, createdIcon, createdIconRt, createdGroup,
                baseScale, baseColor, ghosts, anchorGhostRt, targetPos, ghostParent,
                gatherDuration, flashDuration, settleDuration, pulseScale));
    }

    // Gather + reveal + settle — ARKA PLAN (board düşerken). Ghost'lar bağımsız kopya olduğu
    // için kaynak taşlar (FinalClearTiles ile) temizlense de animasyon bozulmadan tamamlanır.
    private IEnumerator CoAnimateSpecialGather(
        TileView createdTile,
        Image createdIcon,
        RectTransform createdIconRt,
        CanvasGroup createdGroup,
        Vector3 baseScale,
        Color baseColor,
        List<SpecialCreationGhostState> ghosts,
        RectTransform anchorGhostRt,
        Vector2 targetPos,
        RectTransform ghostParent,
        float gatherDuration,
        float flashDuration,
        float settleDuration,
        float pulseScale)
    {
        try
        {
            // Toplanma: TÜM katkı taşları AYNI ANDA merkeze akar, ikinci yarıda solup birleşir.
            float t = 0f;
            while (t < gatherDuration)
            {
                if (createdTile == null || !createdTile || createdIconRt == null || !createdIconRt)
                    yield break;

                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / gatherDuration);
                float e = EaseOutCubic(p);
                float fade = Mathf.InverseLerp(0.5f, 1f, p);

                for (int i = 0; i < ghosts.Count; i++)
                {
                    var g = ghosts[i];
                    if (g.ghostRect == null || !g.ghostRect) continue;
                    g.ghostRect.anchoredPosition = Vector2.LerpUnclamped(g.startPos, targetPos, e);
                    g.ghostRect.localScale = Vector3.LerpUnclamped(Vector3.one, Vector3.one * 0.18f, e);
                    if (g.ghostGroup != null && g.ghostGroup)
                        g.ghostGroup.alpha = Mathf.Lerp(g.sourceColor.a, 0f, fade);
                }

                yield return null;
            }

            // Toplanma bitti → bekleyen anchor'ı kaldır, special şimdi belirir.
            if (anchorGhostRt != null && anchorGhostRt && anchorGhostRt.gameObject != null)
            {
                Object.Destroy(anchorGhostRt.gameObject);
                anchorGhostRt = null;
            }

            if (ghostParent != null && ghostParent && board != null)
            {
                Vector3 worldCenter = ghostParent.TransformPoint(targetPos);
                board.StartCoroutine(TileClearBurstVfx.CoPlayBurstAtWorldPosition(
                    worldCenter, ghostParent, board, Mathf.Max(flashDuration, 0.12f)));
            }

            // ── REVEAL ──
            // LineV/H/PulseCore: normalin ÜSTÜNDE (1.75×) belirip kendi ekseninde 360° dönüp
            // yerine oturur — görünür kalması için ~0.24sn. Diğer special'lar: eski flash+pop+settle.
            bool isSpinReveal =
                createdTile != null && createdTile &&
                (createdTile.GetSpecial() == TileSpecial.LineV
                 || createdTile.GetSpecial() == TileSpecial.LineH
                 || createdTile.GetSpecial() == TileSpecial.PulseCore);

            if (isSpinReveal)
            {
                float revealDur = 0.32f;   // pop + tepe bekleme + spin + otur (görünür kalması için)
                float peakScale = 1.5f;    // 0.75'ten başlar, 1.50'ye büyür, sonra 1.0'a oturur (kullanıcı isteği)

                // Düz 2D sprite → dönüş DAİMA Z ekseninde (ekran düzleminde, para/pervane gibi).
                // X/Y ekseni kağıdı 3B çevirir gibi yassıltıyordu, çirkindi → geri alındı.
                TileSpecial sp = createdTile.GetSpecial();
                bool isRocket = sp == TileSpecial.LineV || sp == TileSpecial.LineH;
                // PulseCore artık kendi Y-ekseni flipbook'uyla (PulseFuseSparkleView.PlayCreationSpin)
                // dönüyor → buradaki rotasyon PulseCore için KAPALI (0).
                // Roketler (LineV/LineH): Y ekseninde 360° (kullanıcı isteği).
                float spinDegrees = isRocket ? 360f : 0f;
                Color colorTarget = new Color(baseColor.r, baseColor.g, baseColor.b, 1f);

                // Flipbook (silindir dönüşü): TileView'de LineH/LineV için 5 kare doluysa frame'leri
                // sırayla oynat + düz eksen dönüşünü kapat. Kare yoksa null → mevcut düz X/Y dönüşü.
                Sprite[] flipFrames = createdTile.GetLineSpinFrames(sp);
                float flipZ = 0f;
                // LineV'nin kendi kareleri yoksa LineH karelerini 90° Z döndürüp kullan (yatay silindir → dik).
                if (flipFrames == null && sp == TileSpecial.LineV)
                {
                    flipFrames = createdTile.GetLineSpinFrames(TileSpecial.LineH);
                    if (flipFrames != null) flipZ = 90f;
                }
                bool useFlipbook = flipFrames != null;
                Sprite originalSpecialSprite = createdIcon != null ? createdIcon.sprite : null;

                t = 0f;
                while (t < revealDur)
                {
                    if (createdTile == null || !createdTile || createdIconRt == null || !createdIconRt)
                        yield break;

                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / revealDur);

                    // Scale: ilk %30 pop (0.22→peak), %30-55 tepede bekle (görünür), sonra 1.0 otur.
                    float scaleFactor;
                    if (k < 0.30f)
                        scaleFactor = Mathf.LerpUnclamped(0.75f, peakScale, EaseOutCubic(k / 0.30f));
                    else if (k < 0.55f)
                        scaleFactor = peakScale;
                    else
                        scaleFactor = Mathf.LerpUnclamped(peakScale, 1f, EaseOutCubic((k - 0.55f) / 0.45f));
                    createdIconRt.localScale = baseScale * scaleFactor;

                    if (useFlipbook)
                    {
                        // 5 kareyi reveal boyunca sırayla göster (silindir dönüşü); düz eksen dönüşü yok.
                        int fi = Mathf.Clamp(Mathf.FloorToInt(k * flipFrames.Length), 0, flipFrames.Length - 1);
                        if (createdIcon != null && flipFrames[fi] != null) createdIcon.sprite = flipFrames[fi];
                        createdIconRt.localRotation = Quaternion.Euler(0f, 0f, flipZ);   // LineV: LineH kareleri 90° dik
                    }
                    else
                    {
                        // Spin: spinDegrees→0 (easeOut), tam turda 0'da biter. LineH X ekseninde, LineV Y
                        // ekseninde döner (kullanıcı isteği); PulseCore=0.
                        float spinAngle = Mathf.LerpUnclamped(spinDegrees, 0f, EaseOutCubic(k));
                        Quaternion spinRot =
                            sp == TileSpecial.LineH ? Quaternion.Euler(spinAngle, 0f, 0f) :   // X ekseni
                            sp == TileSpecial.LineV ? Quaternion.Euler(0f, spinAngle, 0f) :   // Y ekseni
                            Quaternion.identity;
                        createdIconRt.localRotation = spinRot;
                    }

                    // Hızlı fade-in + kısa beyaz parlama.
                    float a = Mathf.Clamp01(k * 3.3f);
                    if (createdGroup != null && createdGroup) createdGroup.alpha = a;
                    if (createdIcon != null && createdIcon)
                    {
                        Color c = Color.Lerp(Color.white, colorTarget, Mathf.Clamp01(k * 1.6f));
                        c.a = a;
                        createdIcon.color = c;
                    }
                    yield return null;
                }

                // Final: tam normale otur (scale 1, rotation 0). Flipbook kullandıysak son karede değil,
                // asıl special sprite'ında bitir.
                createdIconRt.localScale = baseScale;
                createdIconRt.localRotation = Quaternion.identity;
                if (useFlipbook && createdIcon != null && originalSpecialSprite != null)
                    createdIcon.sprite = originalSpecialSprite;
                if (createdGroup != null && createdGroup) createdGroup.alpha = 1f;
                if (createdIcon != null && createdIcon) createdIcon.color = colorTarget;
            }
            else
            {
                // Flash: special fade-in + pop.
                t = 0f;
                while (t < flashDuration)
                {
                    if (createdTile == null || !createdTile || createdIconRt == null || !createdIconRt)
                        yield break;

                    t += Time.deltaTime;
                    float k = Mathf.Clamp01(t / flashDuration);
                    if (createdGroup != null && createdGroup) createdGroup.alpha = k;
                    if (createdIcon != null && createdIcon)
                        createdIcon.color = Color.Lerp(new Color(baseColor.r, baseColor.g, baseColor.b, 0f), Color.white, k);
                    createdIconRt.localScale = Vector3.LerpUnclamped(baseScale * 0.22f, baseScale * pulseScale, EaseOutCubic(k));
                    yield return null;
                }

                // Settle: pulseScale'den normale, white'tan renge (kısa).
                t = 0f;
                while (t < settleDuration)
                {
                    if (createdTile == null || !createdTile || createdIconRt == null || !createdIconRt)
                        yield break;

                    t += Time.deltaTime;
                    float k = EaseOutCubic(Mathf.Clamp01(t / Mathf.Max(0.0001f, settleDuration)));
                    createdIconRt.localScale = Vector3.LerpUnclamped(baseScale * pulseScale, baseScale, k);
                    if (createdIcon != null && createdIcon)
                        createdIcon.color = Color.Lerp(Color.white, new Color(baseColor.r, baseColor.g, baseColor.b, 1f), k);
                    if (createdGroup != null && createdGroup) createdGroup.alpha = 1f;
                    yield return null;
                }
            }
        }
        finally
        {
            for (int i = 0; i < ghosts.Count; i++)
            {
                try { if (ghosts[i].ghostRect != null && ghosts[i].ghostRect && ghosts[i].ghostRect.gameObject != null) Object.Destroy(ghosts[i].ghostRect.gameObject); }
                catch (MissingReferenceException) { }
            }

            try { if (anchorGhostRt != null && anchorGhostRt && anchorGhostRt.gameObject != null) Object.Destroy(anchorGhostRt.gameObject); }
            catch (MissingReferenceException) { }

            try
            {
                if (createdTile != null && createdTile)
                {
                    RestoreTileVisualState(createdTile);
                    if (board != null)
                        createdTile.ApplyTileSize(board.TileSize);
                }
            }
            catch (MissingReferenceException) { }
        }
    }

    private static float EaseOutCubic(float t)
    {
        float inv = 1f - Mathf.Clamp01(t);
        return 1f - (inv * inv * inv);
    }

    private IEnumerator PlayCreatedSpecialAppearOnly(TileView createdTile, float duration)
    {
        if (createdTile == null)
            yield break;

        Image createdIcon = createdTile.IconImage;
        RectTransform createdIconRt = createdIcon != null ? createdIcon.rectTransform : null;
        if (createdIcon == null || createdIconRt == null)
        {
            RestoreTileVisualState(createdTile);
            yield break;
        }

        CanvasGroup createdGroup = createdTile.GetComponent<CanvasGroup>();
        if (createdGroup == null)
            createdGroup = createdTile.gameObject.AddComponent<CanvasGroup>();

        // FIX:
        // Fallback path'te de special layout'u baştan garantiye al.
        // Aksi halde baseScale bazen eski normal tile / FillCell state'inden yakalanabiliyor.
        if (board != null)
            createdTile.ApplyTileSize(board.TileSize);

        // Layout sonrası tekrar referansı alalım; aynı obje olur ama güvenli.
        createdIcon = createdTile.IconImage;
        createdIconRt = createdIcon != null ? createdIcon.rectTransform : null;
        if (createdIcon == null || createdIconRt == null)
        {
            RestoreTileVisualState(createdTile);
            yield break;
        }

        // Special appear fallback — burst'ü de tetikle
        // Icon rt sprite'ın gerçek konumunu verir (tile rootu offsetli olabilir)
        if (board != null && board.Parent != null && createdIconRt != null)
        {
            Vector3[] _cornersAppear = new Vector3[4];
            createdIconRt.GetWorldCorners(_cornersAppear);
            Vector3 _appearCenter = (_cornersAppear[0] + _cornersAppear[2]) * 0.5f;

            board.StartCoroutine(TileClearBurstVfx.CoPlayBurstAtWorldPosition(
                _appearCenter, board.Parent, board, BURST_VFX_DURATION));
        }

        Vector3 baseScale = createdIconRt.localScale;
        Quaternion baseRotation = createdIconRt.localRotation;
        Color baseColor = createdIcon.color;

        createdTile.transform.localScale = Vector3.one;
        createdTile.transform.localRotation = Quaternion.identity;
        createdGroup.alpha = 0f;
        createdIconRt.localScale = baseScale * 0.18f;
        createdIconRt.localRotation = Quaternion.identity;
        createdIcon.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);

        float animDuration = Mathf.Clamp(duration, 0.06f, 0.08f);
        float t = 0f;

        try
        {
            while (t < animDuration)
            {
                if (createdTile == null || createdIconRt == null)
                    yield break;

                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / animDuration);
                float fadeEase = Mathf.Clamp01(k * 1.15f);
                float createdScaleFactor = EvaluateCreatedSpecialScale(k);

                createdIconRt.localScale = baseScale * createdScaleFactor;
                createdIconRt.localRotation = Quaternion.identity;
                createdGroup.alpha = fadeEase;
                createdIcon.color = new Color(baseColor.r, baseColor.g, baseColor.b, fadeEase);

                yield return null;
            }
        }
        finally
        {
            try
            {
                if (createdTile != null && createdTile)
                {
                    RestoreTileVisualState(createdTile);
                    if (board != null)
                        createdTile.ApplyTileSize(board.TileSize);
                }
            }
            catch (MissingReferenceException) { }
        }
    }

    private Vector2 GetRectCenterInParentSpace(RectTransform parent, RectTransform rect)
    {
        if (parent == null || rect == null)
            return Vector2.zero;

        return board != null
            ? board.WorldToAnchoredIn(parent, rect.TransformPoint(rect.rect.center))
            : Vector2.zero;
    }

    private static Vector2 GetRectSizeInParentSpace(RectTransform rect, RectTransform parent)
    {
        if (rect == null || parent == null)
            return Vector2.zero;

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);

        Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 max = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 local = parent.InverseTransformPoint(corners[i]);
            min = Vector2.Min(min, local);
            max = Vector2.Max(max, local);
        }

        return max - min;
    }
    private static float EvaluateCreatedSpecialScale(float t)
    {
        t = Mathf.Clamp01(t);

        if (t < 0.60f)
        {
            float phase = t / 0.60f;
            return Mathf.LerpUnclamped(0.10f, 1.18f, EaseOutBack(phase));
        }

        float settle = (t - 0.60f) / 0.40f;
        return Mathf.LerpUnclamped(1.18f, 1f, EaseOutCubic(settle));
    }

    private static float EaseOutBack(float t)
    {
        t = Mathf.Clamp01(t);
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float x = t - 1f;
        return 1f + c3 * x * x * x + c1 * x * x;
    }

    private struct SpecialCreationGhostState
    {
        public TileView tile;
        public Image sourceImage;
        public Color sourceColor;
        public RectTransform ghostRect;
        public CanvasGroup ghostGroup;
        public Vector2 startPos;
    }

    private static Transform GetVisualTarget(TileView tile)
    {
        if (tile == null) return null;

        Image icon = tile.IconImage;
        if (icon != null && icon.transform != null && icon.transform != tile.transform)
            return icon.transform;

        return tile.transform;
    }

    private static void SetPivotWithoutVisualJump(RectTransform rt, Vector2 newPivot)
    {
        if (rt == null)
            return;

        Vector2 size = rt.rect.size;

        // ESKİSİ TERS YÖNDEYDİ
        Vector2 pivotDelta = newPivot - rt.pivot;
        Vector2 anchoredOffset = new Vector2(
            pivotDelta.x * size.x,
            pivotDelta.y * size.y);

        rt.pivot = newPivot;
        rt.anchoredPosition += anchoredOffset;
    }
    private static void RestoreTileVisualState(TileView tile)
    {
        if (tile == null)
            return;

        RectTransform tileRt = tile.RectTransform;
        if (tileRt != null)
        {
            tileRt.localScale = Vector3.one;
            tileRt.localRotation = Quaternion.identity;
        }

        if (tile.TryGetComponent<CanvasGroup>(out var cg))
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }

        tile.SetIconAlpha(1f);

        Image icon = tile.IconImage;
        if (icon != null)
        {
            Color c = icon.color;
            c.a = 1f;
            icon.color = c;

            RectTransform iconRt = icon.rectTransform;
            if (iconRt != null)
            {
                iconRt.localScale = Vector3.one;
                iconRt.localRotation = Quaternion.identity;
            }
        }
    }


    public IEnumerator PlayTilesImplodeToCell(
    Vector2Int targetCell,
    IReadOnlyList<TileView> sourceTiles,
    float duration,
    float clearAtNormalizedTime,
    Action<TileView> onTileClear)
    {
        if (board == null || sourceTiles == null || sourceTiles.Count == 0)
            yield break;

        RectTransform ghostParent = board.Parent;
        if (ghostParent == null)
            yield break;

        float animDuration = Mathf.Max(0.10f, duration);
        float clearT = Mathf.Clamp01(clearAtNormalizedTime);

        Vector2 targetPos = GetCellCenterInParentSpace(ghostParent, targetCell);

        var ghosts = new List<SpecialCreationGhostState>();
        var cleared = new HashSet<TileView>();
        var seen = new HashSet<TileView>();

        foreach (var tile in sourceTiles)
        {
            if (tile == null || !seen.Add(tile))
                continue;

            Image sourceIcon = tile.IconImage;
            RectTransform sourceIconRt = sourceIcon != null ? sourceIcon.rectTransform : null;
            if (sourceIcon == null || sourceIconRt == null || sourceIcon.sprite == null)
                continue;

            GameObject ghostGo = new GameObject(
                "PulseImplodeGhost",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image));

            RectTransform ghostRt = ghostGo.GetComponent<RectTransform>();
            ghostRt.SetParent(ghostParent, false);
            ghostRt.anchorMin = CenterPivot;
            ghostRt.anchorMax = CenterPivot;
            ghostRt.pivot = CenterPivot;
            ghostRt.SetAsLastSibling();
            ghostRt.sizeDelta = GetRectSizeInParentSpace(sourceIconRt, ghostParent);
            ghostRt.anchoredPosition = GetRectCenterInParentSpace(ghostParent, sourceIconRt);
            ghostRt.localScale = Vector3.one;
            ghostRt.localRotation = Quaternion.identity;

            Image ghostImage = ghostGo.GetComponent<Image>();
            ghostImage.sprite = sourceIcon.sprite;
            ghostImage.type = sourceIcon.type;
            ghostImage.preserveAspect = sourceIcon.preserveAspect;
            ghostImage.material = sourceIcon.material;
            ghostImage.raycastTarget = false;
            ghostImage.color = sourceIcon.color;

            CanvasGroup ghostGroup = ghostGo.GetComponent<CanvasGroup>();
            ghostGroup.alpha = sourceIcon.color.a;

            ghosts.Add(new SpecialCreationGhostState
            {
                tile = tile,
                sourceImage = sourceIcon,
                sourceColor = sourceIcon.color,
                ghostRect = ghostRt,
                ghostGroup = ghostGroup,
                startPos = ghostRt.anchoredPosition
            });

            sourceIcon.color = new Color(
                sourceIcon.color.r,
                sourceIcon.color.g,
                sourceIcon.color.b,
                0f);
        }

        float t = 0f;
        while (t < animDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / animDuration);
            float travelEase = 1f - Mathf.Pow(1f - k, 3f);

            for (int i = 0; i < ghosts.Count; i++)
            {
                var ghost = ghosts[i];
                if (ghost.ghostRect == null)
                    continue;

                ghost.ghostRect.anchoredPosition =
                    Vector2.LerpUnclamped(ghost.startPos, targetPos, travelEase);
                ghost.ghostRect.localScale =
                    Vector3.LerpUnclamped(Vector3.one, Vector3.one * 0.15f, travelEase);
                ghost.ghostRect.localRotation = Quaternion.identity;

                if (ghost.ghostGroup != null)
                    ghost.ghostGroup.alpha = Mathf.Lerp(ghost.sourceColor.a, 0f, k);
            }

            if (k >= clearT)
            {
                for (int i = 0; i < ghosts.Count; i++)
                {
                    var tile = ghosts[i].tile;
                    if (tile == null || cleared.Contains(tile))
                        continue;

                    cleared.Add(tile);
                    onTileClear?.Invoke(tile);
                }
            }

            yield return null;
        }

        for (int i = 0; i < ghosts.Count; i++)
        {
            var ghost = ghosts[i];
            if (ghost.ghostRect != null)
                UnityEngine.Object.Destroy(ghost.ghostRect.gameObject);
        }
    }

    private Vector2 GetCellCenterInParentSpace(RectTransform parent, Vector2Int cell)
    {
        if (board == null || parent == null)
            return Vector2.zero;

        if (cell.x >= 0 && cell.x < board.Width && cell.y >= 0 && cell.y < board.Height)
        {
            TileView tile = board.Tiles[cell.x, cell.y];
            if (tile != null && tile.IconImage != null && tile.IconImage.rectTransform != null)
                return GetRectCenterInParentSpace(parent, tile.IconImage.rectTransform);
        }

        Vector2 basePos = new Vector2(cell.x * board.TileSize, -cell.y * board.TileSize);
        return basePos + new Vector2(board.TileSize * 0.5f, -board.TileSize * 0.5f);
    }
}
