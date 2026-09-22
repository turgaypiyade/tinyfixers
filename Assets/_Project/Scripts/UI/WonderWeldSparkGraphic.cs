using UnityEngine;
using UnityEngine.UI;

/// Small UI spark pool: particles stay in the reveal's space as the torch moves.
public sealed class WonderWeldSparkGraphic : MaskableGraphic
{
    private struct Spark
    {
        public Vector2 position, velocity;
        public float age, lifetime, width;
        public Color tint;
    }

    private readonly Spark[] sparks = new Spark[64];
    private Vector2 emitter;
    private float scale = 1f, emissionTime;
    private int next;
    private bool emitting;

    public void Begin(Vector2 position, float characterSize)
    {
        Clear();
        SetEmitter(position, characterSize);
        emitting = true;
        for (int i = 0; i < 10; i++) Emit();
    }

    public void SetEmitter(Vector2 position, float characterSize)
    {
        emitter = position;
        scale = Mathf.Max(0.1f, characterSize / 240f);
    }

    public void StopEmitting() => emitting = false;

    public void Clear()
    {
        emitting = false;
        emissionTime = 0f;
        next = 0;
        System.Array.Clear(sparks, 0, sparks.Length);
        SetVerticesDirty();
    }

    private void Emit()
    {
        float angle = Random.Range(12f, 168f) * Mathf.Deg2Rad;
        float speed = Random.Range(100f, 320f) * scale;
        sparks[next] = new Spark
        {
            position = emitter + Random.insideUnitCircle * (3f * scale),
            velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
            lifetime = Random.Range(0.18f, 0.45f),
            width = Random.Range(1.2f, 2.6f) * scale,
            tint = next % 4 == 0 ? new Color(1f, 0.74f, 0.2f, 1f) : new Color(0.5f, 0.9f, 1f, 1f)
        };
        next = (next + 1) % sparks.Length;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        bool dirty = false;
        for (int i = 0; i < sparks.Length; i++)
        {
            if (sparks[i].age >= sparks[i].lifetime) continue;
            sparks[i].age += dt;
            sparks[i].velocity.y -= 650f * scale * dt;
            sparks[i].position += sparks[i].velocity * dt;
            dirty = true;
        }
        if (emitting)
        {
            emissionTime += dt;
            int count = Mathf.Min(8, Mathf.FloorToInt(emissionTime * 65f));
            if (count > 0)
            {
                emissionTime %= 1f / 65f;
                for (int i = 0; i < count; i++) Emit();
                dirty = true;
            }
        }
        if (dirty) SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        foreach (var spark in sparks)
        {
            if (spark.age >= spark.lifetime) continue;
            float fade = 1f - spark.age / spark.lifetime;
            Vector2 direction = spark.velocity.normalized;
            Vector2 normal = new Vector2(-direction.y, direction.x);
            Vector2 tail = spark.position - direction * Mathf.Clamp(spark.velocity.magnitude * 0.035f, 3f * scale, 12f * scale);
            Color tip = Color.Lerp(spark.tint, Color.white, fade);
            tip.a = fade;
            Color end = spark.tint;
            end.a = fade * 0.15f;
            int start = vh.currentVertCount;
            vh.AddVert(tail, end, Vector2.zero);
            vh.AddVert(spark.position + normal * spark.width, tip, Vector2.zero);
            vh.AddVert(spark.position + direction * spark.width, tip, Vector2.zero);
            vh.AddVert(spark.position - normal * spark.width, tip, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2);
            vh.AddTriangle(start, start + 2, start + 3);
        }
    }

    protected override void OnDisable()
    {
        Clear();
        base.OnDisable();
    }
}
