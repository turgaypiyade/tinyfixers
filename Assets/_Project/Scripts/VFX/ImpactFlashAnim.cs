using UnityEngine;

public class ImpactFlashAnim : MonoBehaviour
{
    [Header("Timing")]
    public float lifetime = 0.15f;

    [Header("Scale")]
    public float startScale = 0.25f;
    public float endScale = 1.2f;

    [Header("Fade")]
    public float startAlpha = 1f;
    public float endAlpha = 0f;

    private SpriteRenderer sr;
    private float t;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void OnEnable()
    {
        t = 0f;
        transform.localScale = Vector3.one * startScale;

        if (sr != null)
        {
            var c = sr.color;
            c.a = startAlpha;
            sr.color = c;
        }
    }

    void Update()
    {
        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / lifetime);

        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, u);

        if (sr != null)
        {
            var c = sr.color;
            c.a = Mathf.Lerp(startAlpha, endAlpha, u);
            sr.color = c;
        }

        if (t >= lifetime)
            Destroy(gameObject);
    }
}
