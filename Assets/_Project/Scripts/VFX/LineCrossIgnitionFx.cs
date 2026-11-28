using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// LineV+LineH combo "çapraz ateşleme": roketler kalkmadan, kesişim hücresinde dört kollu yıldız
/// parlaması + dışa yayılan şok halkası. YALNIZ GÖRSEL — temizleme/zamanlama LineH/LineV'de aynen kalır.
/// Görseller Resources/LineCrossFx'ten bir kez yüklenir; Image'ler UiVfxPool'dan kiralanır/iade edilir.
/// </summary>
public static class LineCrossIgnitionFx
{
    private const string StarPath = "LineCrossFx/LineCrossStar";
    private const string RingPath = "LineCrossFx/LineCrossRing";
    private const string StarKey = "LineCrossFx.Star";
    private const string RingKey = "LineCrossFx.Ring";

    private const float Duration = 0.85f;      // toplam (0.42 gözle seçilmiyordu — kullanıcı 2026-11-24)
    private const float StarPopTime = 0.16f;   // yıldızın açılma anı
    private const float StarHoldTime = 0.22f;  // tepe parlaklıkta bekleme (açılıştan sonra)
    private const float StarCells = 4.2f;      // yıldızın tepe boyu (hücre)
    private const float RingDelay = 0.06f;     // halka yıldız açılırken çıkar
    private const float RingStartCells = 0.8f;
    private const float RingEndCells = 7f;
    private const float StarSpinDeg = 22f;

    private static Sprite star, ring;
    private static bool loaded;

    public static void Play(BoardController board, Vector2Int cell)
    {
        if (board == null || !board.isActiveAndEnabled || RuntimeSimulationSession.IsActive) return;
        RectTransform space = board.BreakFxParent;
        if (space == null) return;

        if (!loaded)
        {
            loaded = true;
            star = Resources.Load<Sprite>(StarPath);
            ring = Resources.Load<Sprite>(RingPath);
        }
        if (star == null && ring == null) return;

        Vector2 pos = board.WorldToAnchoredIn(space, board.GetCellWorldCenterPosition(cell.x, cell.y));
        float cellPx = Mathf.Max(1f, Vector2.Distance(pos,
            board.WorldToAnchoredIn(space, board.GetCellWorldCenterPosition(cell.x + 1, cell.y))));

        board.StartCoroutine(Run(space, pos, cellPx));
    }

    private static IEnumerator Run(RectTransform space, Vector2 pos, float cellPx)
    {
        Image ringImg = Rent(RingKey, space, ring, pos);
        Image starImg = Rent(StarKey, space, star, pos);   // yıldız halkanın önünde
        try
        {
            float t = 0f;
            while (t < Duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Duration);

                if (ringImg != null)
                {
                    float rk = Mathf.Clamp01((t - RingDelay) / (Duration - RingDelay));
                    float e = 1f - (1f - rk) * (1f - rk);   // ease-out büyüme
                    float size = Mathf.Lerp(RingStartCells, RingEndCells, e) * cellPx;
                    ringImg.rectTransform.sizeDelta = new Vector2(size, size);
                    ringImg.color = new Color(1f, 1f, 1f, t < RingDelay ? 0f : 0.9f * (1f - rk * rk));
                }

                if (starImg != null)
                {
                    // Hızlı açılış (overshoot), sonra hafif dönüp söner.
                    float pop = Mathf.Clamp01(t / StarPopTime);
                    float fadeStart = StarPopTime + StarHoldTime;
                    float fk = Mathf.Clamp01((t - fadeStart) / (Duration - fadeStart));
                    float scale = pop < 1f
                        ? Mathf.Lerp(0.2f, 1.12f, 1f - (1f - pop) * (1f - pop))
                        : t < fadeStart
                            ? 1.12f + 0.04f * Mathf.Sin((t - StarPopTime) * 28f)   // tepede hafif nabız
                            : Mathf.Lerp(1.12f, 0.85f, fk);
                    float size = StarCells * cellPx * scale;
                    starImg.rectTransform.sizeDelta = new Vector2(size, size);
                    starImg.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -StarSpinDeg * k);
                    starImg.color = new Color(1f, 1f, 1f, 1f - fk * fk);
                }

                yield return null;
            }
        }
        finally
        {
            if (ringImg != null) UiVfxPool.Return(RingKey, ringImg.gameObject, 4);
            if (starImg != null) UiVfxPool.Return(StarKey, starImg.gameObject, 4);
        }
    }

    private static Image Rent(string key, RectTransform space, Sprite sprite, Vector2 pos)
    {
        if (sprite == null) return null;
        var img = UiVfxPool.RentImage(key, space, key);
        if (img == null) return null;
        img.gameObject.layer = space.gameObject.layer;   // runtime UI: kamera katmanı (culling)
        img.sprite = sprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = new Color(1f, 1f, 1f, 0f);
        var rt = img.rectTransform;
        rt.anchoredPosition = pos;
        rt.sizeDelta = Vector2.zero;
        rt.SetAsLastSibling();
        return img;
    }
}
