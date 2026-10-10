using UnityEngine;
using UnityEngine.UI;

/// <summary>One reusable textured sheet, driven by the view's coroutine; no Update or debris objects.</summary>
public sealed class WonderRestorationPieceGraphic : MaskableGraphic
{
    const int Columns = 20;
    const int Rows = 24;
    readonly Vector2[] sourcePositions = new Vector2[(Columns + 1) * (Rows + 1)];
    readonly Vector2[] sourceUVs = new Vector2[(Columns + 1) * (Rows + 1)];
    readonly float[] bottom = new float[Columns + 1];
    readonly float[] height = new float[Columns + 1];
    Material ownedMaterial;
    Texture texture;
    Rect imageRect;
    float progress, centerX;
    bool visible;

    public override Texture mainTexture => texture != null ? texture : Texture2D.whiteTexture;

    public void Begin(Sprite sprite, Material sourceMaterial, Rect rect, int cell, int count)
    {
        if (ownedMaterial == null)
        {
            ownedMaterial = new Material(sourceMaterial)
            {
                name = "WonderRestoration_Curl",
                hideFlags = HideFlags.HideAndDontSave
            };
            ownedMaterial.SetFloat("_RestorationPiece", 1f);
            material = ownedMaterial;
        }
        texture = sprite != null ? sprite.texture : Texture2D.whiteTexture;
        Vector4 atlas = sprite != null ? UnityEngine.Sprites.DataUtility.GetOuterUV(sprite) : new Vector4(0, 0, 1, 1);
        ownedMaterial.SetVector("_SpriteUVRect", atlas);
        imageRect = rect;
        float minX = float.PositiveInfinity, maxX = float.NegativeInfinity;
        for (int x = 0; x <= Columns; x++)
        {
            float u = (float)x / Columns;
            Vector2 low = WonderRestorationLayout.SurfacePoint(cell, count, u, 0f);
            Vector2 high = WonderRestorationLayout.SurfacePoint(cell, count, u, 1f);
            bottom[x] = rect.yMin + low.y * rect.height;
            height[x] = Mathf.Max(1f, (high.y - low.y) * rect.height);
            for (int y = 0; y <= Rows; y++)
            {
                int index = y * (Columns + 1) + x;
                Vector2 uv = WonderRestorationLayout.SurfacePoint(cell, count, u, (float)y / Rows);
                Vector2 position = new Vector2(rect.xMin + uv.x * rect.width, rect.yMin + uv.y * rect.height);
                sourcePositions[index] = position;
                sourceUVs[index] = new Vector2(Mathf.Lerp(atlas.x, atlas.z, uv.x), Mathf.Lerp(atlas.y, atlas.w, uv.y));
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
            }
        }
        centerX = (minX + maxX) * 0.5f;
        progress = 0f;
        visible = true;
        gameObject.SetActive(true);
        SetMaterialDirty();
        SetVerticesDirty();
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        SetVerticesDirty();
    }

    public void Clear()
    {
        visible = false;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (!visible) return;
        float roll = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.7f));
        float detached = Mathf.Clamp01((progress - 0.58f) / 0.42f);
        float drop = detached * detached * imageRect.height * 1.25f;
        float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((progress - 0.88f) / 0.12f));
        for (int y = 0; y <= Rows; y++)
        {
            for (int x = 0; x <= Columns; x++)
            {
                int index = y * (Columns + 1) + x;
                Vector2 source = sourcePositions[index];
                float hinge = bottom[x] + height[x] * (1f - roll);
                float distance = Mathf.Max(0f, source.y - hinge);
                float radius = height[x] * 0.16f;
                float angle = Mathf.Min(2.7f, distance / radius);
                float tail = Mathf.Max(0f, distance - radius * 2.7f);
                float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
                float depth = radius * (1f - cos) + tail * sin;
                float bentY = distance > 0f ? hinge + radius * sin + tail * cos : source.y;
                // Project the curl towards the viewer; render ordering stays in UI space.
                float perspective = 1f + depth / Mathf.Max(1f, imageRect.width) * 0.55f;
                float projectedX = centerX + (source.x - centerX) * perspective;
                float shade = 0.72f + 0.28f * cos + 0.2f * Mathf.Pow(sin, 8f);
                Color tint = new Color(shade, shade * (1f - 0.06f * sin), shade * (1f - 0.14f * sin), alpha);
                vh.AddVert(new Vector3(projectedX, bentY - drop, 0f), tint, sourceUVs[index]);
            }
        }
        // Lower rows first: the top curling over them must be drawn last.
        for (int y = 0; y < Rows; y++)
            for (int x = 0; x < Columns; x++)
            {
                int a = y * (Columns + 1) + x;
                int b = a + Columns + 1;
                vh.AddTriangle(a, b, b + 1);
                vh.AddTriangle(a, b + 1, a + 1);
            }
    }

    protected override void OnDisable()
    {
        visible = false;
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        if (ownedMaterial != null)
        {
            if (Application.isPlaying) Destroy(ownedMaterial);
            else DestroyImmediate(ownedMaterial);
        }
        base.OnDestroy();
    }
}
