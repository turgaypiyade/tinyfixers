using UnityEngine;

/// <summary>
/// Cargo (işçi robot) için "süzülerek iniyor" hissi: tile DÜŞERKEN (hareket ederken) görseli
/// sağa-sola yatırır (Z tilt). Tile durunca yumuşakça dik konuma döner. Böylece sway tam olarak
/// düşüşe bağlıdır — kısa düşüşte kısa, büyük düşüşte belirgin. Hareket, tile root'unun
/// anchoredPosition değişiminden algılanır (düşüş animasyonuna dokunmaz).
/// TileView'in icon'una eklenir. LateUpdate'te uygulanır → başka bir şey rotation'ı ezse bile kazanır.
/// </summary>
public sealed class CargoFloatSway : MonoBehaviour
{
    [Tooltip("Sağa-sola yatış genliği (derece).")]
    [SerializeField] private float tiltAngle = 12f;
    [Tooltip("Düşerken saniyedeki salınım sayısı (Hz).")]
    [SerializeField] private float frequency = 2.5f;
    [Tooltip("Durunca dik konuma dönüş hızı (derece/sn katsayısı).")]
    [SerializeField] private float returnSpeed = 6f;
    [Tooltip("Hareket sayılması için minimum kare-başı yer değişimi (kare).")]
    [SerializeField] private float moveEpsilon = 0.05f;

    private RectTransform _root;
    private Vector2 _lastPos;
    private bool _hasLast;
    private float _phase;
    private float _current;

    private void OnEnable()
    {
        _phase = Random.Range(0f, Mathf.PI * 2f);
        ResolveRoot();
    }

    private void OnDisable()
    {
        transform.localRotation = Quaternion.identity;
        _current = 0f;
    }

    private void ResolveRoot()
    {
        var tv = GetComponentInParent<TileView>();
        _root = tv != null ? tv.RectTransform : transform.parent as RectTransform;
        if (_root != null)
        {
            _lastPos = _root.anchoredPosition;
            _hasLast = true;
        }
    }

    private void LateUpdate()
    {
        if (_root == null) ResolveRoot();

        bool moving = false;
        if (_root != null)
        {
            Vector2 p = _root.anchoredPosition;
            if (_hasLast)
                moving = (p - _lastPos).sqrMagnitude > (moveEpsilon * moveEpsilon);
            _lastPos = p;
            _hasLast = true;
        }

        if (moving)
        {
            _phase += Time.deltaTime * frequency * (Mathf.PI * 2f);
            _current = Mathf.Sin(_phase) * tiltAngle;
        }
        else
        {
            // Dururken yumuşakça dik konuma dön.
            _current = Mathf.MoveTowards(_current, 0f, tiltAngle * returnSpeed * Time.deltaTime);
        }

        transform.localRotation = Quaternion.Euler(0f, 0f, _current);
    }
}
