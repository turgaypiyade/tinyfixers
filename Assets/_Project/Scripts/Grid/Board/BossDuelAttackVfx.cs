using UnityEngine;
using UnityEngine.UI;

/// Small reusable UI pool. Ghosts snapshot the body pose, so they stay behind during a dash.
public sealed class BossDuelAttackVfx : MonoBehaviour
{
    private const int GhostCount = 12;
    private readonly Image[] ghosts = new Image[GhostCount];
    private readonly float[] ages = new float[GhostCount];
    private readonly Color[] colors = new Color[GhostCount];
    private Image body;
    private BossDuelCharacterProfile profile;
    private BossDuelImpactGraphic impact;
    private float standingHeight, trailTimer;
    private int nextGhost;

    public void Initialize(Image source, BossDuelCharacterProfile character, float height)
    {
        body = source;
        profile = character;
        standingHeight = height;
    }

    public void BeginTrail() => trailTimer = Mathf.Max(0.01f, profile.trailInterval);

    public void TickTrail(float delta)
    {
        trailTimer += delta;
        if (trailTimer < Mathf.Max(0.01f, profile.trailInterval)) return;
        trailTimer = 0f;
        EmitGhost();
    }

    private void EmitGhost()
    {
        if (body == null || body.sprite == null) return;
        int slot = nextGhost++ % GhostCount;
        var ghost = ghosts[slot];
        if (ghost == null)
        {
            var go = new GameObject("DuelAfterimage", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(body.transform.parent, false);
            BossDuelController.MatchParentLayer(go.transform);
            ghost = ghosts[slot] = go.GetComponent<Image>();
            ghost.raycastTarget = false;
            ghost.material = profile.afterimageMaterial;
            ghost.preserveAspect = body.preserveAspect;
        }
        // Behind the live character, with the exact sprite crop/pivot/mirroring captured.
        ghost.transform.SetAsLastSibling();
        ghost.transform.SetSiblingIndex(body.transform.GetSiblingIndex());
        var from = body.rectTransform;
        var to = ghost.rectTransform;
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.sizeDelta = from.sizeDelta;
        to.anchoredPosition3D = from.anchoredPosition3D;
        to.localRotation = from.localRotation;
        to.localScale = from.localScale;
        ghost.sprite = body.sprite;
        ghost.material = profile.afterimageMaterial;
        colors[slot] = Color.Lerp(profile.trailColor, profile.trailAccentColor, (nextGhost % 3) * 0.5f);
        ghost.color = colors[slot];
        ages[slot] = 0f;
        ghost.gameObject.SetActive(true);
    }

    public void PlayImpact(int power)
    {
        if (body == null || power <= 0) return;
        if (impact == null)
        {
            var go = new GameObject("DuelWeaponImpact", typeof(RectTransform), typeof(CanvasRenderer), typeof(BossDuelImpactGraphic));
            go.transform.SetParent(body.transform.parent, false);
            BossDuelController.MatchParentLayer(go.transform);
            impact = go.GetComponent<BossDuelImpactGraphic>();
            impact.raycastTarget = false;
        }
        var rt = impact.rectTransform;
        var source = body.rectTransform;
        var parent = (RectTransform)source.parent;
        Rect rect = source.rect;
        Vector2 point = profile.weaponImpactPoint;
        Vector3 contact = source.TransformPoint(new Vector3(
            rect.xMin + rect.width * point.x, rect.yMin + rect.height * point.y, 0f));
        rt.anchorMin = rt.anchorMax = parent.pivot;
        rt.pivot = Vector2.one * 0.5f;
        rt.anchoredPosition = parent.InverseTransformPoint(contact);
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();
        // A bounded visual intensity; the damage itself is never normalized or clamped here.
        float strength = Mathf.Clamp01((float)power / Mathf.Max(1, profile.powerForFullImpact));
        impact.Play(standingHeight * Mathf.Lerp(0.32f, 0.85f, strength), strength, profile.impactFxDuration);
    }

    private void Update()
    {
        if (profile == null) return;
        float life = Mathf.Max(0.02f, profile.trailLifetime);
        for (int i = 0; i < GhostCount; i++)
        {
            var ghost = ghosts[i];
            if (ghost == null || !ghost.gameObject.activeSelf) continue;
            ages[i] += Time.deltaTime;
            if (ages[i] >= life) { ghost.gameObject.SetActive(false); continue; }
            Color tint = colors[i];
            float fade = 1f - ages[i] / life;
            tint.a *= fade * fade;
            ghost.color = tint;
        }
    }

    public void Clear()
    {
        foreach (var ghost in ghosts)
            if (ghost != null) ghost.gameObject.SetActive(false);
        if (impact != null) impact.gameObject.SetActive(false);
    }

    private void OnDisable() => Clear();

    private void OnDestroy()
    {
        foreach (var ghost in ghosts)
            if (ghost != null) Destroy(ghost.gameObject);
        if (impact != null) Destroy(impact.gameObject);
    }
}
