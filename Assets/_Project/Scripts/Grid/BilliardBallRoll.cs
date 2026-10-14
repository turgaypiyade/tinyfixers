using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tahtadaki bilardo topu: ikon numarasız gövdeyi gösterir, BilliardBallFace üstünde iki numara 3B yuvarlanır
/// (yüzeyde gidiş yönünde kayar, kenarda basıklaşıp arkaya geçer) + sabit parlama. Top HER harekette (yana, çapraz,
/// düz düşüş) gittiği yol kadar fiziksel yuvarlanır. Hareket, taş kökünün anchoredPosition değişiminden okunur
/// (düşüş motoruna dokunmaz). TileView ikonuna eklenir; BoardController.EnsureMovableIdleFx açar/kapatır
/// (yok edilmez — ertelenen Destroy aynı karedeki yeniden eklemeyi yutuyordu).
/// </summary>
public sealed class BilliardBallRoll : MonoBehaviour
{
    private RectTransform _root;
    private Image _icon;
    private BilliardBallFace _face;
    private Sprite _body;
    private Vector2 _last;
    private bool _hasLast;

    public BilliardBallFace Face => _face;

    /// Sprite'lar: gövde (numarasız; ikon bunu gösterir), numara çıkartması, parlama. Her refresh'te çağrılır, ucuz.
    public void Setup(Sprite body, Sprite number, Sprite shine)
    {
        _body = body;
        if (_icon == null) _icon = GetComponent<Image>();
        if (_face == null && !TryGetComponent(out _face)) _face = gameObject.AddComponent<BilliardBallFace>();
        EnforceBodySprite();
        _face.Init(number, shine);
        _face.SetVisible(true);
    }

    private void OnEnable()
    {
        var tv = GetComponentInParent<TileView>();
        _root = tv != null ? tv.RectTransform : null;
        _hasLast = false;
        if (_face != null) _face.SetVisible(true);
    }

    private void OnDisable()
    {
        // Normal taşa dönen havuz taşında yüz görünmesin; yönelim sıfırlansın.
        if (_face != null)
        {
            _face.SetVisible(false);
            _face.Orientation = Quaternion.identity;
        }
    }

    // TileView.RefreshIcon ikonu tanımın (numaralı, HUD/editör için) sprite'ına geri yazar → tahtada numarasız gövde.
    private void EnforceBodySprite()
    {
        if (_icon == null || _body == null || _icon.sprite == _body) return;
        _icon.sprite = _body;
        if (_face != null) _face.SyncMaskSprite(_body);
    }

    private void LateUpdate()
    {
        EnforceBodySprite();
        if (_root == null || _face == null) return;

        Vector2 p = _root.anchoredPosition;
        if (_hasLast)
        {
            Vector2 d = p - _last;
            // Işınlanma (snap / havuz yeniden kullanımı) yuvarlanma sayılmaz: kare başı bir hücreden fazla yol.
            float cell = ((RectTransform)transform).rect.width;
            if (d.sqrMagnitude < cell * cell)
                _face.Roll(d);
        }
        _last = p;
        _hasLast = true;
    }
}
