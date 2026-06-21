using System.Collections;
using UnityEngine;

/// <summary>
/// GoldMoney parçaları için "arada bir dönen para" idle animasyonu: belirli aralıklarla
/// görseli Y ekseninde sağa-sola sallar (para ışığa dönüyormuş hissi). İnce bir juice efekti.
/// Bu transform üzerinde çalışır (TileView'in icon'una eklenir), tile konumunu/düşüşünü bozmaz.
/// </summary>
public sealed class CoinIdleWobble : MonoBehaviour
{
    [Tooltip("İki dönüş arası min/max bekleme (sn). Rastgele seçilir.")]
    [SerializeField] private float minInterval = 2.5f;
    [SerializeField] private float maxInterval = 5f;

    [Tooltip("Sağa-sola dönüş açısı (derece). Y ekseninde foreshortening = para dönüyor hissi.")]
    [SerializeField] private float turnAngle = 60f;
    [SerializeField] private float turnDuration = 1.2f;
    [Tooltip("Dönüş ekseni. Y (up) = sağa-sola; Z (forward) = sallanma/tilt.")]
    [SerializeField] private Vector3 axis = Vector3.up;

    private Coroutine _co;

    private void OnEnable() => _co = StartCoroutine(Loop());

    private void OnDisable()
    {
        if (_co != null) StopCoroutine(_co);
        _co = null;
        transform.localRotation = Quaternion.identity;
    }

    private IEnumerator Loop()
    {
        // Rastgele ilk gecikme → tüm coinler aynı anda dönmesin.
        yield return new WaitForSeconds(Random.Range(0f, maxInterval));
        while (true)
        {
            yield return TurnOnce();
            yield return new WaitForSeconds(Random.Range(minInterval, maxInterval));
        }
    }

    // 0 → +açı → -açı → 0 (sağa-sola), ease-in-out.
    private IEnumerator TurnOnce()
    {
        yield return RotateTo(turnAngle,  turnDuration * 0.35f);
        yield return RotateTo(-turnAngle, turnDuration * 0.40f);
        yield return RotateTo(0f,         turnDuration * 0.25f);
    }

    private IEnumerator RotateTo(float angle, float dur)
    {
        Quaternion from = transform.localRotation;
        Quaternion to   = Quaternion.AngleAxis(angle, axis);
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / dur);
            float e = k * k * (3f - 2f * k);   // smoothstep
            transform.localRotation = Quaternion.SlerpUnclamped(from, to, e);
            yield return null;
        }
        transform.localRotation = to;
    }
}
