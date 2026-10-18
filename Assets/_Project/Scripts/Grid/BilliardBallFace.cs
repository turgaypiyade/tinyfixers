using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bilardo topunun 3B yuvarlanan yüzü (UI). Host = numarasız gövde Image'ı (RectTransform). Gerçek bilardo topu gibi
/// iki karşıt yüzünde numara var; top yuvarlandıkça numara yüzeyde GİDİŞ YÖNÜNDE kayar, kenara yaklaştıkça radyal
/// yönde basıklaşır (ortografik izdüşüm), arkaya geçince söner, karşı yüzdeki numara öbür kenardan gelir. Parlama en
/// üstte ve sabit (tek ışık kaynağı). Numaralar gövde silüetiyle maskelenir (kenardan taşmaz).
/// Tahtadaki top (BilliardBallRoll) ve cebe batan ghost (GoalFlyFx) aynı yüzü kullanır.
///
/// Matematik (yerel çerçeve: x sağ, y yukarı, z izleyiciye): ekranda d kadar giden top, eksen = z × d etrafında
/// |d|/r radyan döner. Numaranın yüzey normali p = yönelim * (0,0,±1); ekrandaki merkezi r·(p.x, p.y), basıklık p.z.
/// </summary>
public sealed class BilliardBallFace : MonoBehaviour
{
    // Top sprite'ı hücrenin %86'sı → yarıçap host genişliğinin %43'ü. Numara çapı top çapının %42'si (kaynak görsel).
    private const float RadiusOfWidth = 0.43f;
    private const float NumberDiameterOfBall = 0.42f;
    // Çok hızlı düşüşte kare başı dönüş sınırı: üstü stroboskop gibi geri dönüyormuş görünür.
    private const float MaxRadiansPerFrame = 0.7f;
    // Numara kenara yaklaşınca (p.z küçük) söner — ince şerit hâlinde titremesin.
    private const float FadeStartZ = 0.08f, FadeFullZ = 0.35f;

    private RectTransform _host;
    private RectTransform _clip;
    private readonly RectTransform[] _pivots = new RectTransform[2];
    private readonly Image[] _numbers = new Image[2];
    private Image _shine;
    private Quaternion _orientation = Quaternion.identity;
    private bool _built;

    public Quaternion Orientation
    {
        get => _orientation;
        set { _orientation = value; Apply(); }
    }

    /// Çocukları bir kez kurar (tekrar çağrı yalnız sprite'ları günceller). layer = host'un katmanı.
    public void Init(Sprite numberSprite, Sprite shineSprite)
    {
        _host = transform as RectTransform;
        if (!_built)
        {
            _built = true;
            // Maske: gövde silüeti (showMaskGraphic=false) → numaralar kenardan taşmaz.
            var clipGo = NewChild("BallFace", _host, typeof(Image), typeof(Mask));
            _clip = (RectTransform)clipGo.transform;
            Stretch(_clip);
            var clipImg = clipGo.GetComponent<Image>();
            clipImg.raycastTarget = false;
            clipImg.preserveAspect = true;
            clipGo.GetComponent<Mask>().showMaskGraphic = false;

            for (int i = 0; i < 2; i++)
            {
                _pivots[i] = (RectTransform)NewChild("NumberPivot", _clip).transform;
                _pivots[i].anchorMin = _pivots[i].anchorMax = _pivots[i].pivot = new Vector2(0.5f, 0.5f);
                _pivots[i].sizeDelta = Vector2.zero;
                var numGo = NewChild("Number", _pivots[i], typeof(Image));
                _numbers[i] = numGo.GetComponent<Image>();
                _numbers[i].raycastTarget = false;
                _numbers[i].preserveAspect = true;
                var nrt = _numbers[i].rectTransform;
                nrt.anchorMin = nrt.anchorMax = nrt.pivot = new Vector2(0.5f, 0.5f);
            }

            var shineGo = NewChild("BallShine", _host, typeof(Image));
            _shine = shineGo.GetComponent<Image>();
            _shine.raycastTarget = false;
            _shine.preserveAspect = true;
            Stretch(_shine.rectTransform);
        }

        var hostImage = GetComponent<Image>();
        var clipImage = _clip.GetComponent<Image>();
        if (hostImage != null && clipImage.sprite != hostImage.sprite) clipImage.sprite = hostImage.sprite;
        for (int i = 0; i < 2; i++)
            if (_numbers[i].sprite != numberSprite) _numbers[i].sprite = numberSprite;
        if (_shine.sprite != shineSprite) _shine.sprite = shineSprite;
        _shine.enabled = shineSprite != null;
        Apply();
    }

    /// Maske gövde sprite'ını izler (host sprite'ı değişirse çağır).
    public void SyncMaskSprite(Sprite bodySprite)
    {
        if (_clip == null) return;
        var clipImage = _clip.GetComponent<Image>();
        if (clipImage.sprite != bodySprite) clipImage.sprite = bodySprite;
    }

    /// Ekranda (host'un ebeveyn uzayında, y yukarı) delta kadar yuvarlan.
    public void Roll(Vector2 delta)
    {
        float radius = Radius;
        float dist = delta.magnitude;
        if (dist < 0.0001f || radius <= 0f) return;
        float rad = Mathf.Min(dist / radius, MaxRadiansPerFrame);
        var axis = new Vector3(-delta.y, delta.x, 0f);   // z × d
        _orientation = Quaternion.AngleAxis(rad * Mathf.Rad2Deg, axis) * _orientation;
        Apply();
    }

    /// Yerinde dönerek yuvarlanma (cebe batarken): yön birim vektör, açı radyan.
    public void Spin(Vector2 direction, float radians)
    {
        if (direction.sqrMagnitude < 0.0001f || radians <= 0f) return;
        Roll(direction.normalized * (radians * Radius));
    }

    private float Radius => _host != null ? _host.rect.width * RadiusOfWidth : 0f;

    private void Apply()
    {
        if (!_built) return;
        float radius = Radius;
        float size = radius * 2f * NumberDiameterOfBall;
        for (int i = 0; i < 2; i++)
        {
            Vector3 p = _orientation * new Vector3(0f, 0f, i == 0 ? 1f : -1f);
            float alpha = Mathf.Clamp01((p.z - FadeStartZ) / (FadeFullZ - FadeStartZ));
            var img = _numbers[i];
            bool visible = alpha > 0.001f;
            if (img.enabled != visible) img.enabled = visible;
            if (!visible) continue;

            // Radyal basıklık: pivot radyal yöne döner, x'te p.z ile ölçeklenir; numara geri döner (dik kalır).
            float psi = Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg;
            var pivot = _pivots[i];
            pivot.anchoredPosition = new Vector2(p.x, p.y) * radius;
            pivot.localRotation = Quaternion.Euler(0f, 0f, psi);
            pivot.localScale = new Vector3(Mathf.Max(0.01f, p.z), 1f, 1f);
            var nrt = img.rectTransform;
            nrt.localRotation = Quaternion.Euler(0f, 0f, -psi);
            nrt.sizeDelta = new Vector2(size, size);
            var c = img.color;
            if (!Mathf.Approximately(c.a, alpha)) { c.a = alpha; img.color = c; }
        }
    }

    private GameObject NewChild(string name, Transform parent, params System.Type[] components)
    {
        var types = new System.Type[components.Length + 2];
        types[0] = typeof(RectTransform);
        types[1] = typeof(CanvasRenderer);
        components.CopyTo(types, 2);
        var go = new GameObject(name, types);
        go.layer = gameObject.layer;   // runtime UI: layer 0'da doğarsa kamera çizmez
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    /// Kararma (cebe batarken): numaralar gövdeyle aynı tonda kararır, parlama söner.
    public void SetShade(float shade, float shineAlpha)
    {
        if (!_built) return;
        for (int i = 0; i < 2; i++)
        {
            var c = _numbers[i].color;
            _numbers[i].color = new Color(shade, shade, shade, c.a);
        }
        _shine.color = new Color(1f, 1f, 1f, shineAlpha);
    }

    /// Kapat/aç: havuz taşı normal taşa dönünce yüz görünmesin.
    public void SetVisible(bool visible)
    {
        if (_clip != null && _clip.gameObject.activeSelf != visible) _clip.gameObject.SetActive(visible);
        if (_shine != null && _shine.gameObject.activeSelf != visible) _shine.gameObject.SetActive(visible);
    }
}
