using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RocketBasket roketlerinin uçuş görseli — havada bir "yarım ay" (yarım daire) çizerek süzülüş:
///   • Kaynaktan bir AÇIYLA yükselir, tepede yarım daire çizip hedefe iner (S DEĞİL, tek kavis).
///   • Yay src↔tgt'yi çap kabul eden bir daire üzerinde; bulgeScale ile daha yüksek/oval yapılır.
///   • Ölçek tepe anında büyüyüp (1 → peakScale) inişte 1'e döner.
///   • Roket burnu daima gittiği yöne (tanjant) döner — tek sprite yeter, alev arkada kalır.
/// BoardController GameObject'ine component olarak eklenir. Hedefleme/impact PatchBot ile aynı.
/// </summary>
public sealed class RocketProjectileFlight : MonoBehaviour
{
    [SerializeField] private BoardController board;

    [Header("Boyut")]
    [Tooltip("Roket temel (scale=1) boyutu — tile oranı.")]
    [Range(0.3f, 1.5f)] [SerializeField] private float rocketSizeRatio = 0.7f;
    [Tooltip("Tepe anında ulaşılan ölçek (1 → peakScale → 1).")]
    [Range(1f, 3f)] [SerializeField] private float peakScale = 1.6f;

    [Header("Yarım daire yayı")]
    [Tooltip("Yayın yüksekliği. 1 = tam yarım daire; >1 daha yüksek/oval yarım ay; <1 daha basık.")]
    [Range(0.4f, 2.5f)] [SerializeField] private float bulgeScale = 1.4f;

    [Header("Süre")]
    [Tooltip("Temel uçuş süresi (sn).")]
    [SerializeField] private float baseDuration = 0.6f;
    [Tooltip("Mesafeye göre eklenen süre (sn / tile).")]
    [SerializeField] private float durationPerTile = 0.03f;

    [Header("Yön")]
    [Tooltip("Sprite'ın burnu +Y (yukarı) bakıyorsa 0. Sağa bakıyorsa -90.")]
    [SerializeField] private float noseOffsetDeg = 0f;

    private BoardController Board => board != null ? board : (board = GetComponent<BoardController>());

    public IEnumerator Fly(Vector2Int from, Vector2Int to, Sprite rocketSprite, Action onArrived)
    {
        var b = Board;
        // Obstacle'ların ÜSTÜNDE uçsun: PatchBot dash'iyle aynı VFX katmanı (yoksa board parent'ı).
        var flightRoot = (b != null && b.BoardVfxPlayer != null && b.BoardVfxPlayer.VfxRoot != null)
            ? b.BoardVfxPlayer.VfxRoot
            : (b != null ? b.Parent : null);
        if (b == null || flightRoot == null || rocketSprite == null)
        {
            onArrived?.Invoke();
            yield break;
        }

        Vector2 src = CellAnchored(b, from, flightRoot);
        Vector2 tgt = CellAnchored(b, to, flightRoot);
        // Flight root local uzayındaki gerçek tile boyutu (VfxRoot ölçeği farklı olabilir).
        float ts = Vector2.Distance(src, CellAnchored(b, from + Vector2Int.right, flightRoot));
        if (ts < 1f) ts = Mathf.Max(1f, b.TileSize);

        // src↔tgt'yi çap kabul eden dairesel yay. along = merkez→src; perp = yukarı kabaran dik.
        Vector2 mid = (src + tgt) * 0.5f;
        Vector2 along = src - mid;
        Vector2 perp = new Vector2(-along.y, along.x);
        if (perp.y < 0f) perp = -perp;                                  // yay yukarı kabarsın
        Vector2 perpUnit = perp.sqrMagnitude > 0.0001f ? perp.normalized : Vector2.up;
        float radius = along.magnitude;

        Vector2 PathPoint(float t)
        {
            float ang = Mathf.PI * t;                                   // 0 → π : src → tgt
            return mid
                 + Mathf.Cos(ang) * along
                 + Mathf.Sin(ang) * perpUnit * (radius * bulgeScale);
        }

        var go = new GameObject("Rocket", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(flightRoot, false);
        go.transform.SetAsLastSibling();

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        float size = ts * rocketSizeRatio;
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = src;
        rt.localScale = Vector3.one;

        var img = go.GetComponent<Image>();
        img.sprite = rocketSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;

        float dur = Mathf.Max(0.15f, baseDuration + durationPerTile * (radius * 2f / Mathf.Max(1f, ts)));

        float time = 0f;
        while (time < dur)
        {
            time += Time.deltaTime;
            float k = Mathf.Clamp01(time / dur);

            Vector2 pos = PathPoint(k);
            rt.anchoredPosition = pos;

            float s = 1f + (peakScale - 1f) * Mathf.Sin(Mathf.PI * k);
            rt.localScale = new Vector3(s, s, 1f);

            Vector2 next = PathPoint(Mathf.Min(1f, k + 0.02f));
            Vector2 dir = next - pos;
            if (dir.sqrMagnitude > 0.0001f)
            {
                float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f + noseOffsetDeg;
                rt.localRotation = Quaternion.Euler(0f, 0f, ang);
            }

            yield return null;
        }

        rt.anchoredPosition = tgt;
        rt.localScale = Vector3.one;
        Destroy(go);
        onArrived?.Invoke();
    }

    // Hücre merkezini, verilen flight root'un local anchored uzayına çevirir (PatchBot dash ile
    // aynı yöntem) — böylece roket obstacle katmanının ÜSTÜNDEKİ VFX root'ta doğru konumda uçar.
    private static Vector2 CellAnchored(BoardController b, Vector2Int cell, RectTransform space)
    {
        Vector3 worldPos = b.GetCellWorldCenterPosition(cell.x, cell.y);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            space,
            RectTransformUtility.WorldToScreenPoint(null, worldPos),
            null,
            out var localPoint);
        return localPoint;
    }
}
