using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class OilSpreadAnimator : MonoBehaviour
{
    [SerializeField] private GameObject spreadFxPrefab;  // UI_OilSpreadFX
    [SerializeField] private BoardController board;
    [SerializeField] private float duration = 0.50f;
    [SerializeField, Min(0.05f)] private float spreadFrameSequenceDuration = 0.42f;
    [SerializeField, Range(0.05f, 0.95f)] private float spreadFrameCrossFadeRatio = 0.55f;
    [SerializeField] private Sprite oilOverlaySprite;
    [SerializeField] private Texture oilInteriorTexture;
    [SerializeField] private Sprite oilDropSprite;
    [SerializeField] private Sprite oilSpreadSprite;
    [SerializeField] private Sprite[] spreadUpFrames;
    [SerializeField] private Sprite[] spreadRightFrames;

    // Kalıcı oil görseli (cell-anchored blob) bu assetlerle çizilir — OilOverlayRenderer kullanır.
    public Sprite OilOverlaySprite => oilOverlaySprite;
    public Texture OilInteriorTexture => oilInteriorTexture;

    public IEnumerator PlaySpread(IReadOnlyList<OilSpreadPair> pairs)
    {
        if (pairs == null || pairs.Count == 0) yield break;

        int done = 0;
        foreach (var pair in pairs)
            StartCoroutine(PlayOnePair(pair, () => done++));

        while (done < pairs.Count)
            yield return null;
    }

    private IEnumerator PlayOnePair(OilSpreadPair pair, System.Action onDone)
    {
        if (spreadFxPrefab == null || board == null) { onDone?.Invoke(); yield break; }

        // TilesRoot: board'un tile'ları yerleştirdiği parent.
        // DrawObstacleImage ile aynı koordinat sistemi: (x*ts, -y*ts) top-left origin.
        var tilesRoot = board.TilesRoot;
        if (tilesRoot == null) { onDone?.Invoke(); yield break; }

        float ts = board.TileSize;
        int dx = pair.Target.x - pair.Source.x;
        int dy = pair.Target.y - pair.Source.y;
        bool sameCell = dx == 0 && dy == 0;       // yeni oil: kendi hücresinde belirir
        bool isH = !sameCell && dy == 0;          // aynı satır farklı sütun = yatay; aynı hücre = dikey
        float spanTiles = sameCell ? 1f : Mathf.Max(1, Mathf.Abs(isH ? dx : dy));
        float bridgeLen = spanTiles * ts;         // köprü kaynak↔hedef mesafesi kadar (bitişikte 1 tile)

        // Hücre merkezleri: DrawObstacleImage ile aynı formül
        Vector2 srcCenter = new Vector2(pair.Source.x * ts + ts * 0.5f, -pair.Source.y * ts - ts * 0.5f);
        Vector2 tgtCenter = new Vector2(pair.Target.x * ts + ts * 0.5f, -pair.Target.y * ts - ts * 0.5f);
        Vector2 midCenter = (srcCenter + tgtCenter) * 0.5f;

        var go = Instantiate(spreadFxPrefab, tilesRoot);
        go.transform.SetAsLastSibling();

        var fxRt = go.GetComponent<RectTransform>();
        fxRt.anchorMin = new Vector2(0f, 1f);
        fxRt.anchorMax = new Vector2(0f, 1f);
        fxRt.pivot     = new Vector2(0.5f, 0.5f);
        fxRt.anchoredPosition = midCenter;

        var bridgeH   = go.transform.Find("Bridge_H");
        var bridgeV   = go.transform.Find("Bridge_V");
        var targetOil = go.transform.Find("TargetOil");

        if (bridgeH   != null) bridgeH.gameObject.SetActive(isH);
        if (bridgeV   != null) bridgeV.gameObject.SetActive(!isH);
        if (targetOil != null) targetOil.gameObject.SetActive(false);

        Image dropImg = null;
        RectTransform dropRt = null;
        if (oilDropSprite != null)
        {
            var dropGo = new GameObject("OilDrop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            dropRt = dropGo.GetComponent<RectTransform>();
            dropRt.SetParent(fxRt, false);
            dropRt.anchorMin = dropRt.anchorMax = new Vector2(0.5f, 0.5f);
            dropRt.pivot = new Vector2(0.5f, 0.5f);
            dropRt.anchoredPosition = (sameCell ? tgtCenter : srcCenter) - midCenter + Vector2.up * ts * 0.45f;
            dropRt.sizeDelta = new Vector2(ts * 0.72f, ts);
            dropRt.localScale = Vector3.one;

            dropImg = dropGo.GetComponent<Image>();
            dropImg.sprite = oilDropSprite;
            dropImg.preserveAspect = true;
            dropImg.raycastTarget = false;
            dropImg.color = Color.white;
        }

        var activeGo  = isH ? bridgeH : bridgeV;
        var activeImg = activeGo?.GetComponent<Image>();
        var bridgeRt  = activeGo?.GetComponent<RectTransform>();

        // Bridge: ortala, tile arası mesafe = tileSize
        if (bridgeRt != null)
        {
            bridgeRt.anchorMin = new Vector2(0.5f, 0.5f);
            bridgeRt.anchorMax = new Vector2(0.5f, 0.5f);
            bridgeRt.pivot     = new Vector2(0.5f, 0.5f);
            bridgeRt.anchoredPosition = Vector2.zero;
            float crossSize = isH
                ? (bridgeRt.sizeDelta.y > 1f ? bridgeRt.sizeDelta.y : 24f)
                : (bridgeRt.sizeDelta.x > 1f ? bridgeRt.sizeDelta.x : 24f);
            bridgeRt.sizeDelta = isH ? new Vector2(bridgeLen, crossSize) : new Vector2(crossSize, bridgeLen);
        }

        // Image Fill modu
        if (activeImg != null)
        {
            activeImg.type       = Image.Type.Filled;
            activeImg.fillMethod = isH ? Image.FillMethod.Horizontal : Image.FillMethod.Vertical;
            bool positiveDir = isH ? (dx > 0) : (dy > 0);
            // NOT: grid y AŞAĞI doğru büyür (target.y > source.y => target ekranda ALTTA, kaynak ÜSTTE).
            // Dolum kaynaktan hedefe akmalı → target alttayken Top'tan başla (yatayda x normal yönde).
            activeImg.fillOrigin = isH
                ? (int)(positiveDir ? Image.OriginHorizontal.Left : Image.OriginHorizontal.Right)
                : (int)(positiveDir ? Image.OriginVertical.Top    : Image.OriginVertical.Bottom);
            activeImg.fillAmount = 0f;
        }

        // Aşama 1: Bridge akışı
        float elapsed  = 0f;
        float growTime = duration * 0.65f;
        while (elapsed < growTime)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Clamp01(elapsed / growTime);
            if (activeImg != null) activeImg.fillAmount = Mathf.SmoothStep(0f, 1f, k);
            if (dropRt != null)
            {
                Vector2 start = (sameCell ? tgtCenter : srcCenter) - midCenter + Vector2.up * ts * 0.45f;
                Vector2 end = tgtCenter - midCenter;
                dropRt.anchoredPosition = Vector2.LerpUnclamped(start, end, Mathf.SmoothStep(0f, 1f, k));
                dropRt.localScale = Vector3.one * Mathf.Lerp(1f, 0.72f, k);
            }
            yield return null;
        }
        if (activeImg != null) activeImg.fillAmount = 1f;
        if (dropImg != null)
            dropImg.color = new Color(1f, 1f, 1f, 0f);

        Image targetOilImg = null;

        // Aşama 2: TargetOil hedef hücre merkezine yerleştir
        if (targetOil != null)
        {
            var tOilRt = targetOil.GetComponent<RectTransform>();
            if (tOilRt != null)
            {
                // FX root local space'inde hedef hücrenin merkezi
                tOilRt.anchorMin = new Vector2(0.5f, 0.5f);
                tOilRt.anchorMax = new Vector2(0.5f, 0.5f);
                tOilRt.pivot     = new Vector2(0.5f, 0.5f);
                tOilRt.anchoredPosition = tgtCenter - midCenter;
                tOilRt.sizeDelta = new Vector2(ts, ts);
                tOilRt.localEulerAngles = new Vector3(0f, 0f, ResolveTargetRotation(dx, dy, sameCell, isH));
            }

            targetOilImg = targetOil.GetComponent<Image>();
            if (targetOilImg != null)
            {
                Sprite targetSprite = ResolveFinalOilSprite();
                if (targetSprite != null)
                    targetOilImg.sprite = targetSprite;
                targetOilImg.type = Image.Type.Simple;
                targetOilImg.preserveAspect = false;
                targetOilImg.color = Color.white;
                targetOilImg.raycastTarget = false;
            }

            targetOil.gameObject.SetActive(true);
        }

        float revealTime = Mathf.Max(duration * 0.35f, spreadFrameSequenceDuration);
        yield return AnimateTargetOilFrames(targetOilImg, dx, dy, sameCell, isH, revealTime);

        Destroy(go);
        onDone?.Invoke();
    }

    private IEnumerator AnimateTargetOilFrames(Image image, int dx, int dy, bool sameCell, bool isH, float revealTime)
    {
        if (image == null)
        {
            if (revealTime > 0f)
                yield return new WaitForSeconds(revealTime);
            yield break;
        }

        var frames = BuildFrameSequence(dx, dy, sameCell, isH);
        if (frames.Count == 0)
        {
            Sprite finalSprite = ResolveFinalOilSprite();
            if (finalSprite != null)
                image.sprite = finalSprite;
            if (revealTime > 0f)
                yield return new WaitForSeconds(revealTime);
            yield break;
        }

        Image blendImage = CreateBlendImage(image);
        float frameTime = Mathf.Max(0.04f, revealTime / frames.Count);
        float fadeTime = Mathf.Clamp(frameTime * spreadFrameCrossFadeRatio, 0.025f, frameTime);
        for (int i = 0; i < frames.Count; i++)
        {
            yield return CrossFadeFrame(image, blendImage, frames[i], fadeTime);

            float holdTime = frameTime - fadeTime;
            if (holdTime > 0f)
                yield return new WaitForSeconds(holdTime);
        }

        if (blendImage != null)
            Destroy(blendImage.gameObject);

        Sprite final = ResolveFinalOilSprite();
        if (final != null)
            image.sprite = final;
        SetImageAlpha(image, 1f);
    }

    private List<Sprite> BuildFrameSequence(int dx, int dy, bool sameCell, bool isH)
    {
        var frames = new List<Sprite>(6);
        Sprite finalSprite = ResolveFinalOilSprite();

        if (sameCell)
        {
            AddFrame(frames, finalSprite);
            return frames;
        }

        if (isH)
        {
            AddFrames(frames, spreadRightFrames);
            AddFrame(frames, finalSprite);
            return frames;
        }

        AddFrame(frames, finalSprite);
        AddFrames(frames, spreadUpFrames);
        AddFrame(frames, finalSprite);
        return frames;
    }

    private float ResolveTargetRotation(int dx, int dy, bool sameCell, bool isH)
    {
        if (sameCell)
            return 0f;

        if (isH)
            return dx < 0 ? 180f : 0f;

        // SpreadUp sprite'ları yukarı yön içindir; aşağı akışta 180° döndürülür.
        return dy > 0 ? 180f : 0f;
    }

    private Sprite ResolveFinalOilSprite()
    {
        return oilSpreadSprite != null ? oilSpreadSprite : oilOverlaySprite;
    }

    private static Image CreateBlendImage(Image source)
    {
        if (source == null)
            return null;

        var go = new GameObject("OilFrameBlend", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(source.rectTransform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localEulerAngles = Vector3.zero;

        var image = go.GetComponent<Image>();
        image.type = Image.Type.Simple;
        image.preserveAspect = source.preserveAspect;
        image.raycastTarget = false;
        SetImageAlpha(image, 0f);
        return image;
    }

    private static IEnumerator CrossFadeFrame(Image baseImage, Image blendImage, Sprite nextSprite, float fadeTime)
    {
        if (baseImage == null)
            yield break;

        if (nextSprite == null || blendImage == null || fadeTime <= 0f)
        {
            if (nextSprite != null)
                baseImage.sprite = nextSprite;
            SetImageAlpha(baseImage, 1f);
            if (blendImage != null)
                SetImageAlpha(blendImage, 0f);
            yield break;
        }

        blendImage.sprite = nextSprite;
        SetImageAlpha(blendImage, 0f);
        SetImageAlpha(baseImage, 1f);

        float elapsed = 0f;
        while (elapsed < fadeTime)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeTime));
            SetImageAlpha(baseImage, 1f - k);
            SetImageAlpha(blendImage, k);
            yield return null;
        }

        baseImage.sprite = nextSprite;
        SetImageAlpha(baseImage, 1f);
        SetImageAlpha(blendImage, 0f);
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        if (image == null)
            return;

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private static void AddFrames(List<Sprite> frames, Sprite[] sprites)
    {
        if (sprites == null)
            return;

        for (int i = 0; i < sprites.Length; i++)
            AddFrame(frames, sprites[i]);
    }

    private static void AddFrame(List<Sprite> frames, Sprite sprite)
    {
        if (sprite != null)
            frames.Add(sprite);
    }
}
