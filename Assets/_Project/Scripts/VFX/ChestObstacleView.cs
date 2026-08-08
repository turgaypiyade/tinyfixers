using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Dolabin acik halindeki katmanli gorseli.
// Her renk objesi ayri bir child Image. Renk kirilinca ilgili child gizlenir.
// Layout, parent boyutu kesinlestikten sonra ApplyLayout() ile set edilir.
public class ChestObstacleView : MonoBehaviour
{
    [SerializeField] private Image gearObject;
    [SerializeField] private Image coreObject;
    [SerializeField] private Image boltObject;
    [SerializeField] private Image plateObject;

    [Header("Kapı Düşüşü (ilk hit)")]
    [Tooltip("Sol/sağ kapak sprite'ları. İlk hitte iki büyük parça halinde yerçekimiyle düşer.")]
    [SerializeField] private Sprite leftDoorSprite;
    [SerializeField] private Sprite rightDoorSprite;
    [Tooltip("Opsiyonel sap/kulp — ortada ayrı bir küçük parça olarak düşer.")]
    [SerializeField] private Sprite doorHandleSprite;
    [Tooltip("Opsiyonel: kapılar düşünce root sprite'ı buna geçer (dolabın AÇIK iç gövdesi). " +
             "Atanmazsa root olduğu gibi kalır.")]
    [SerializeField] private Sprite openBodySprite;
    [SerializeField] private bool doorFallEnabled = true;
    [SerializeField, Min(0.05f)] private float doorFallDuration = 1.4f;
    [SerializeField] private float doorFallGravity = 2600f;
    [SerializeField] private float doorFallSideKick = 150f;
    [Tooltip("İlk fırlatma yukarı hızı (px/s). Parçalar önce yukarı zıplar, sonra düşer.")]
    [SerializeField] private float doorFallLaunchUp = 600f;
    [SerializeField] private float doorSpinDegrees = 80f;

    [Header("Shake")]
    [Tooltip("Sallama sucresi (saniye).")]
    [SerializeField, Range(0.1f, 0.8f)] private float shakeDuration = 0.35f;

    [Tooltip("Sallama genisligi (px).")]
    [SerializeField, Range(1f, 20f)] private float shakeMagnitude = 7f;

    [Tooltip("Sallama frekansı (yuksek = hizli titresim).")]
    [SerializeField, Range(10f, 60f)] private float shakeFrequency = 35f;

    [Header("Layout")]
    [Tooltip("Dis kenarlardaki bosluk (px).")]
    [SerializeField, Range(0f, 40f)] private float iconPadding = 12f;

    [Tooltip("Ampuller arasindaki ic bosluk (px). Kucuk deger = ampuller birbirine yakin.")]
    [SerializeField, Range(0f, 20f)] private float innerSpacing = 4f;

    [Tooltip("Tum ikon bandini dikey kaydirma (0 = ortada, pozitif = yukari, negatif = asagi).")]
    [SerializeField, Range(-0.3f, 0.3f)] private float verticalShift = 0f;

    [Tooltip("Ampul sprite oranı. Sabit boyut değil, sadece oran korunur. 341 / 402.")]
    [SerializeField] private Vector2 bulbAspectSourceSize = new Vector2(341f, 402f);

    [Tooltip("Sol ampuller sağa, sağ ampuller sola bu kadar px yaklaşır.")]
    [SerializeField, Range(0f, 8f)] private float horizontalInwardNudge = 6f;

    // GridSpawner, parent RT boyutunu set ettikten sonra cagirmalı.
    // GridSpawner, parent RT boyutunu set ettikten sonra cagirmalı.
    public void ApplyLayout()
    {
        float s = verticalShift;
        float h = innerSpacing * 0.5f;
        float n = horizontalInwardNudge;

        // Sol taraf +n: sağa yaklaşır
        // Sağ taraf -n: sola yaklaşır
        PlaceIconAspectFit(
            gearObject,
            0f, 0.5f + s, 0.5f, 1.0f + s,
            iconPadding, h, h, iconPadding,
            +n);

        PlaceIconAspectFit(
            coreObject,
            0.5f, 0.5f + s, 1.0f, 1.0f + s,
            h, iconPadding, h, iconPadding,
            -n);

        PlaceIconAspectFit(
            boltObject,
            0f, 0.0f + s, 0.5f, 0.5f + s,
            iconPadding, h, iconPadding, h,
            +n);

        PlaceIconAspectFit(
            plateObject,
            0.5f, 0.0f + s, 1.0f, 0.5f + s,
            h, iconPadding, iconPadding, h,
            -n);
    }
    public void HideColor(ChestColorMask color)
    {
        switch (color)
        {
            case ChestColorMask.Gear:  SetActive(gearObject,  false); break;
            case ChestColorMask.Core:  SetActive(coreObject,  false); break;
            case ChestColorMask.Bolt:  SetActive(boltObject,  false); break;
            case ChestColorMask.Plate: SetActive(plateObject, false); break;
        }
    }

    public void ShowAll()
    {
        SetActive(gearObject,  true);
        SetActive(coreObject,  true);
        SetActive(boltObject,  true);
        SetActive(plateObject, true);
    }

    /// <summary>İlk hit açılışı: kapaklar 2 büyük parça halinde düşer, iç (renkler) görünür.
    /// GridSpawner.HandleChestOpened bunu çağırır (eski ShowAll yerine).</summary>
    public void OpenCabinet()
    {
        // Önce iç görünsün (açık gövde + renkler)...
        if (openBodySprite != null)
        {
            var root = GetComponent<Image>();
            if (root != null) root.sprite = openBodySprite;
        }

        ShowAll();

        // ...kapaklar EN SON spawn edilir → renklerin/içeriğin ÜSTÜnde düşer (arkada kalma fix'i).
        if (doorFallEnabled && (leftDoorSprite != null || rightDoorSprite != null))
            SpawnFallingDoors();
    }

    private void SpawnFallingDoors()
    {
        var rt = GetComponent<RectTransform>();
        if (rt == null) return;

        float w = rt.rect.width  > 1f ? rt.rect.width  : 100f;
        float h = rt.rect.height > 1f ? rt.rect.height : 100f;

        // Kapaklar yarım kabini DOLDURUR (Wardrobe gibi, preserveAspect=false); sap oranını korur.
        if (leftDoorSprite  != null) SpawnDoorPiece(leftDoorSprite,  new Vector2(-w * 0.25f, 0f), w * 0.5f, h, -1, keepAspect: false);
        if (rightDoorSprite != null) SpawnDoorPiece(rightDoorSprite, new Vector2(+w * 0.25f, 0f), w * 0.5f, h, +1, keepAspect: false);
        if (doorHandleSprite != null) SpawnDoorPiece(doorHandleSprite, Vector2.zero, w * 0.30f, h * 0.30f, +1, keepAspect: true);
    }

    private void SpawnDoorPiece(Sprite sp, Vector2 localCenter, float w, float h, int dir, bool keepAspect)
    {
        if (sp == null) return;

        var go = new GameObject("DoorPiece", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var prt = go.GetComponent<RectTransform>();
        prt.SetParent(transform, false);
        prt.anchorMin = prt.anchorMax = prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(w, h);
        prt.anchoredPosition = localCenter;
        prt.SetAsLastSibling();   // renklerin/iç gövdenin ÜSTÜnde düşsün

        var img = go.GetComponent<Image>();
        img.sprite = sp;
        img.color = Color.white;      // tam opak başla
        img.preserveAspect = keepAspect;
        img.raycastTarget = false;

        StartCoroutine(CoDoorFall(prt, img, dir));
    }

    private IEnumerator CoDoorFall(RectTransform rt, Image img, int dir)
    {
        Vector2 pos = rt.anchoredPosition;
        Vector2 vel = new Vector2(dir * doorFallSideKick, doorFallLaunchUp);   // önce yukarı fırlar, sonra düşer
        float rot = 0f;
        float t = 0f;
        while (t < doorFallDuration && rt != null)
        {
            float dt = Time.deltaTime;
            t += dt;
            vel.y -= doorFallGravity * dt;
            pos   += vel * dt;
            rot   += dir * doorSpinDegrees * dt;
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, rot);

            float k = Mathf.Clamp01(t / doorFallDuration);
            // Fade yalnız SON %20'de → parçalar düşüşün büyük kısmında tam opak kalır.
            if (img != null && k > 0.8f)
            {
                var c = img.color; c.a = Mathf.Lerp(1f, 0f, (k - 0.8f) / 0.2f); img.color = c;
            }
            yield return null;
        }
        if (rt != null) Destroy(rt.gameObject);
    }

    public void HideAll()
    {
        SetActive(gearObject,  false);
        SetActive(coreObject,  false);
        SetActive(boltObject,  false);
        SetActive(plateObject, false);
    }

    private Coroutine _shakeRoutine;

    public void Shake()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _shakeRoutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        var rt = GetComponent<RectTransform>();
        Vector2 origin = rt.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            float damping = 1f - (elapsed / shakeDuration);
            float offsetX = Mathf.Sin(elapsed * shakeFrequency) * shakeMagnitude * damping;
            rt.anchoredPosition = origin + new Vector2(offsetX, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        rt.anchoredPosition = origin;
        _shakeRoutine = null;
    }

    private void PlaceIconAspectFit(
        Image img,
        float xMin,
        float yMin,
        float xMax,
        float yMax,
        float left,
        float right,
        float bottom,
        float top,
        float xNudge)
    {
        if (img == null) return;

        var rt = img.GetComponent<RectTransform>();
        var parent = rt.parent as RectTransform;
        if (parent == null) return;

        Rect parentRect = parent.rect;

        float areaX0 = parentRect.xMin + parentRect.width * xMin + left;
        float areaX1 = parentRect.xMin + parentRect.width * xMax - right;
        float areaY0 = parentRect.yMin + parentRect.height * yMin + bottom;
        float areaY1 = parentRect.yMin + parentRect.height * yMax - top;

        float availableW = Mathf.Max(0f, areaX1 - areaX0);
        float availableH = Mathf.Max(0f, areaY1 - areaY0);

        float aspect = bulbAspectSourceSize.x / bulbAspectSourceSize.y;

        float finalW = availableW;
        float finalH = finalW / aspect;

        if (finalH > availableH)
        {
            finalH = availableH;
            finalW = finalH * aspect;
        }

        Vector2 localCenter = new Vector2(
            (areaX0 + areaX1) * 0.5f + xNudge,
            (areaY0 + areaY1) * 0.5f
        );

        Vector2 parentCenter = new Vector2(
            parentRect.xMin + parentRect.width * 0.5f,
            parentRect.yMin + parentRect.height * 0.5f
        );

        img.preserveAspect = true;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localCenter - parentCenter;
        rt.sizeDelta = new Vector2(finalW, finalH);
        rt.localScale = Vector3.one;
    }

    private static void SetActive(Image img, bool active)
    {
        if (img != null) img.gameObject.SetActive(active);
    }
}
