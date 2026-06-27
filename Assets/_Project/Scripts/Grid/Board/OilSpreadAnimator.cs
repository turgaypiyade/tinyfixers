using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class OilSpreadAnimator : MonoBehaviour
{
    [SerializeField] private GameObject spreadFxPrefab;  // UI_OilSpreadFX
    [SerializeField] private BoardController board;
    [SerializeField] private float duration = 0.50f;
    [SerializeField] private Sprite oilOverlaySprite;

    // Kalıcı oil görseli (cell-anchored) bu sprite ile çizilir — OilOverlayRenderer kullanır.
    public Sprite OilOverlaySprite => oilOverlaySprite;

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
            if (activeImg != null) activeImg.fillAmount = Mathf.SmoothStep(0f, 1f, elapsed / growTime);
            yield return null;
        }
        if (activeImg != null) activeImg.fillAmount = 1f;

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
            }

            var tOilImg = targetOil.GetComponent<Image>();
            if (tOilImg != null)
            {
                if (oilOverlaySprite != null)
                    tOilImg.sprite = oilOverlaySprite;
                tOilImg.type = Image.Type.Simple;
                tOilImg.preserveAspect = false;
                tOilImg.color = Color.white;
                tOilImg.raycastTarget = false;
            }

            targetOil.gameObject.SetActive(true);
        }

        yield return new WaitForSeconds(duration * 0.35f);

        Destroy(go);
        onDone?.Invoke();
    }
}
