using UnityEngine;
using System.Text;

[RequireComponent(typeof(LineRenderer))]
public class LightningBeam : MonoBehaviour
{
    [Header("Shape")]
    [SerializeField] private int segments = 12;          // kaç kırık nokta
    [SerializeField] private float jaggedness = 0.25f;   // sağa sola sapma
    [SerializeField] private float noiseScale = 1.0f;    // noise frekansı

    [Header("Timing")]
    [SerializeField] private float lifeTime = 0.20f;     // toplam ömür
    [SerializeField] private AnimationCurve alphaOverLife =
        AnimationCurve.EaseInOut(0, 1, 1, 0);            // başta güçlü, sonda kaybol

    public float extraLength = 0.15f; // Inspector'dan ayarlanabilir
    [SerializeField] private GameObject impactFlashPrefab;

    private LineRenderer lr;
    private float t;
    private Color startColor;
    private Color endColor;

    private Vector3 a;
    private Vector3 b;
    private bool initialized;

    private void Awake()
    {
        lr = GetComponent<LineRenderer>();

        // FORCE VISIBLE DEBUG
        lr.useWorldSpace = false;
       // lr.widthMultiplier = 1.5f;          // çok kalın olsun
        //lr.startColor = Color.magenta;      // fosforlu pembe
        //lr.endColor   = Color.magenta;
        //lr.positionCount = 2;

        // mevcut kodun devamı...
        startColor = lr.startColor;
        endColor = lr.endColor;
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public void Dump(string tag = "")
        {
            if (lr == null)
            {
                Debug.LogWarning($"[Lightning][Dump]{tag} lr is NULL on {name}");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[Lightning][Dump]{tag} name={name}");
            sb.AppendLine($"  activeInHierarchy={gameObject.activeInHierarchy} enabled(LR)={lr.enabled}");
            sb.AppendLine($"  parent={(transform.parent ? transform.parent.name : "NULL")}");
            sb.AppendLine($"  transform pos={transform.position} localPos={transform.localPosition} scale={transform.lossyScale}");
            sb.AppendLine($"  useWorldSpace={lr.useWorldSpace} alignment={lr.alignment} positionCount={lr.positionCount}");
            sb.AppendLine($"  startWidth={lr.startWidth} endWidth={lr.endWidth}");
            sb.AppendLine($"  sortingLayerID={lr.sortingLayerID} orderInLayer={lr.sortingOrder}");
            sb.AppendLine($"  material={(lr.sharedMaterial ? lr.sharedMaterial.name : "NULL")} shader={(lr.sharedMaterial ? lr.sharedMaterial.shader.name : "NULL")}");

            // İlk/son point + bounds
            if (lr.positionCount > 0)
            {
                Vector3 p0 = lr.GetPosition(0);
                Vector3 pL = lr.GetPosition(lr.positionCount - 1);
                sb.AppendLine($"  p0={p0}  plast={pL}");
            }

            var b = lr.bounds;
            sb.AppendLine($"  bounds center={b.center} size={b.size}");

            // Gradient alpha anahtarları (0 mı diye kontrol)
            var g = lr.colorGradient;
            if (g != null)
            {
                var ak = g.alphaKeys;
                if (ak != null && ak.Length > 0)
                {
                    sb.Append("  alphaKeys=");
                    for (int i = 0; i < ak.Length; i++)
                        sb.Append($"({ak[i].time:0.##}:{ak[i].alpha:0.##}) ");
                    sb.AppendLine();
                }
            }

            Debug.Log(sb.ToString());
        }

    /// <summary> Başlangıç ve bitiş noktalarını ayarla, çizimi oluştur. </summary>
    public void Init(Vector3 start, Vector3 end)
    {
        a = start;
        b = end;

        a.z = 0f;
        b.z = 0f;

        initialized = true;
        Vector3 dir = (end - start).normalized;
        end += dir * extraLength;   // sadece hedefi az uzat
        lr.SetPosition(0, start);
        lr.SetPosition(1, end);
        if (impactFlashPrefab != null)
        {
            var fx = Instantiate(impactFlashPrefab, transform);
            fx.transform.position = end;
            fx.transform.localScale = Vector3.one * 0.35f; // küçük başlasın (0.25–0.5 dene)
        }

        lr.positionCount = Mathf.Max(2, segments);
        BuildLightning();
     //   Dump(" AFTER_INIT");
    }

    private void Update()
    {
        if (!initialized) return;

        t += Time.deltaTime;
        float u = Mathf.Clamp01(t / lifeTime);

        // alpha fade
        float alpha = alphaOverLife.Evaluate(u);
        var sc = startColor; sc.a = alpha;
        var ec = endColor;   ec.a = alpha;
        lr.startColor = sc;
        lr.endColor = ec;

        // hafif titreşim: çok pahalıya kaçmadan 1-2 kere rebuild
        if (u < 0.75f)
            BuildLightning();

        if (t >= lifeTime)
            Destroy(gameObject);
    }

    private void BuildLightning()
    {
        Vector3 dir = (b - a);
        float len = dir.magnitude;
        if (len < 0.001f) len = 0.001f;

        Vector3 forward = dir / len;

        // 2D UI/Camera için “yana” sapma vektörü (kameraya göre)
        Vector3 side = Vector3.Cross(forward, Vector3.forward);
        if (side.sqrMagnitude < 0.0001f)
            side = Vector3.Cross(forward, Camera.main ? Camera.main.transform.forward : Vector3.forward);
        side.Normalize();

        for (int i = 0; i < lr.positionCount; i++)
        {
            float p = (lr.positionCount == 1) ? 0 : (float)i / (lr.positionCount - 1);
            Vector3 pos = Vector3.Lerp(a, b, p);

            // uçlarda sapma olmasın
            float edgeFade = Mathf.Sin(p * Mathf.PI); // 0..1..0
            float n = Mathf.PerlinNoise(Time.time * noiseScale, p * noiseScale) - 0.5f;

            pos += side * (n * jaggedness * edgeFade);

            lr.SetPosition(i, pos);
        }
    }
}
