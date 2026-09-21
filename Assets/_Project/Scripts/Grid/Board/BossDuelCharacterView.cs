using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// Animates full-body sprites on the body child only. The controller owns root hit/win motion.
public sealed class BossDuelCharacterView : MonoBehaviour
{
    private Image body;
    private BossDuelCharacterProfile profile;
    private Vector2 ground;
    private float standingHeight;
    private Vector3 baseScale;
    private bool focused, attacking, finished;
    private bool victoryPending;
    private int poseVersion;
    private float idleTime;
    private bool shieldActive;
    private float shieldBlockRemaining;
    private float shieldBlockDirection;
    private BossDuelAttackVfx attackVfx;
    private Quaternion restRotation;
    private bool dazed;
    private float dazeTime;
    private RectTransform dazeRoot;
    private RectTransform[] dazeStars;
    public bool IsAttacking => attacking;
    public bool HasShieldPose => profile != null && profile.shield != null && profile.shield.sprite != null;

    public void Initialize(Image image, BossDuelCharacterProfile character, RectTransform effectsRoot = null)
    {
        body = image;
        profile = character;
        var rt = body.rectTransform;
        standingHeight = rt.rect.height;
        ground = rt.anchoredPosition - new Vector2(0f, rt.rect.height * rt.pivot.y);
        baseScale = rt.localScale;
        restRotation = rt.localRotation;
        if (profile.mirrorHorizontally) baseScale.x *= -1f;
        body.type = Image.Type.Simple;
        body.preserveAspect = true;
        body.raycastTarget = false;
        attackVfx = gameObject.AddComponent<BossDuelAttackVfx>();
        attackVfx.Initialize(body, profile, standingHeight, effectsRoot);
        Show(profile.idle);
    }

    public void SetProfile(BossDuelCharacterProfile character)
    {
        if (character != null && character.IsUsable && character != profile)
        {
            if (profile.mirrorHorizontally != character.mirrorHorizontally) baseScale.x *= -1f;
            profile = character;
            attackVfx.Clear();
            attackVfx.Initialize(body, profile, standingHeight);
        }
        ResetForWave();
    }

    public void SetFocused(bool value) => focused = value;

    public void SetShieldActive(bool value)
    {
        if (shieldActive == value) return;
        shieldActive = value;
        if (!attacking && !finished) ShowRestPose();
    }

    public void PlayShieldBlock(float knockDirection)
    {
        if (!HasShieldPose || finished) return;
        shieldBlockDirection = Mathf.Sign(knockDirection);
        shieldBlockRemaining = Mathf.Max(0.02f, profile.shieldBlockDuration);
        if (!attacking) ShowRestPose();
    }

    private bool ShowShield => HasShieldPose && (shieldActive || shieldBlockRemaining > 0f);

    private void ShowRestPose()
    {
        // Keep the final absorbed hit visible briefly even when it consumes the last shield charge.
        var guard = shieldBlockRemaining > 0f && profile.shieldHit != null && profile.shieldHit.sprite != null
            ? profile.shieldHit : profile.shield;
        Show(ShowShield ? guard : focused ? profile.focused : profile.idle);
    }

    private void Update()
    {
        if (profile == null) return;
        if (dazed)
        {
            UpdateDaze(Time.deltaTime);
            return;
        }
        if (finished) return;
        shieldBlockRemaining = Mathf.Max(0f, shieldBlockRemaining - Time.deltaTime);
        if (attacking) return;
        if (ShowShield)
        {
            ShowRestPose();
            float progress = 1f - shieldBlockRemaining / Mathf.Max(0.02f, profile.shieldBlockDuration);
            float recoil = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            body.rectTransform.anchoredPosition += Vector2.right *
                (shieldBlockDirection * standingHeight * profile.shieldBlockRecoil * recoil);
            body.rectTransform.localScale = Vector3.Scale(baseScale,
                new Vector3(1f + recoil * 0.025f, 1f - recoil * 0.025f, 1f));
            return;
        }
        idleTime += Time.deltaTime;
        float interval = Mathf.Max(0.1f, profile.blinkInterval);
        bool blink = idleTime % (interval + profile.blinkDuration) >= interval;
        float alternateInterval = Mathf.Max(0.1f, profile.idleAlternateInterval);
        bool alternate = idleTime % (alternateInterval + profile.idleAlternateDuration) >= alternateInterval;
        var idlePose = alternate && profile.idleAlternate != null && profile.idleAlternate.sprite != null
            ? profile.idleAlternate : profile.idle;
        Show(focused ? profile.focused : blink && profile.blink != null && profile.blink.sprite != null ? profile.blink : idlePose);
        float breath = 1f + Mathf.Sin(idleTime * 2.2f) * profile.breathingAmount;
        body.rectTransform.localScale = Vector3.Scale(baseScale, new Vector3(1f, breath, 1f));
    }

    private void Show(BossDuelCharacterProfile.Pose pose, Vector2 motion = default)
    {
        if (body == null || profile == null) return;
        if (pose == null || pose.sprite == null) pose = profile.idle;
        var rt = body.rectTransform;
        body.sprite = pose.sprite;
        rt.pivot = pose.groundPivot;
        float height = standingHeight * Mathf.Max(0.01f, pose.height);
        rt.sizeDelta = new Vector2(height * pose.sprite.rect.width / pose.sprite.rect.height, height);
        rt.anchoredPosition = ground + pose.offset + motion;
        rt.localScale = baseScale;
        rt.localRotation = restRotation;
    }

    public IEnumerator Attack(RectTransform target, Action impact, Func<bool> cancelled, int power)
    {
        if (attacking || finished || body == null || profile == null) yield break;
        int version = poseVersion;
        bool landed = false;
        attacking = true;
        try
        {
            Show(profile.windup);
            yield return new WaitForSeconds(Mathf.Max(0.02f, profile.windupDuration));
            if (finished || version != poseVersion || cancelled()) yield break;

            // Bring the hammer/fists close to the opponent without moving HP bars or hit roots.
            float dx = 0f;
            if (target != null && body.rectTransform.parent != null)
            {
                float targetX = body.rectTransform.parent.InverseTransformPoint(target.position).x;
                float distance = targetX - body.rectTransform.localPosition.x;
                dx = Mathf.Sign(distance) * Mathf.Max(0f, Mathf.Abs(distance) - standingHeight * profile.contactDistance);
            }
            Vector2 reach = new Vector2(dx, 0f);
            var swingPose = profile.swing != null && profile.swing.sprite != null ? profile.swing : profile.windup;
            float duration = Mathf.Max(0.02f, profile.swingDuration);
            attackVfx.BeginTrail();
            attackVfx.BeginSwing(power, GetStrikeContact(reach), target);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (finished || version != poseVersion || cancelled()) yield break;
                Show(swingPose, Vector2.Lerp(Vector2.zero, reach, Mathf.Clamp01(t / duration)));
                attackVfx.TickTrail(Time.deltaTime);
                attackVfx.TickSwing(Mathf.Clamp01(t / duration));
                yield return null;
            }
            if (finished || version != poseVersion || cancelled()) yield break;
            Show(profile.strike, reach);
            attackVfx.ReleaseSwing();
            attackVfx.PlayImpact(power);
            landed = true;
            impact?.Invoke();
            if (finished) yield break;
            yield return new WaitForSeconds(Mathf.Max(0.02f, profile.impactDuration));

            // Once the hit has landed, dash back even if it ended the wave or won the duel.
            // Defeat/reset can still interrupt; victory is displayed only after landing at home.
            duration = Mathf.Max(0.02f, profile.recoveryDuration);
            attackVfx.BeginTrail();
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (finished || version != poseVersion) yield break;
                Show(profile.focused, Vector2.Lerp(reach, Vector2.zero, Mathf.Clamp01(t / duration)));
                attackVfx.TickTrail(Time.deltaTime);
                yield return null;
            }
        }
        finally
        {
            if (!landed) attackVfx.CancelSwing();
            if (version == poseVersion)
            {
                attacking = false;
                if (victoryPending)
                {
                    victoryPending = false;
                    finished = true;
                    Show(profile.victory);
                }
                else if (!finished) ShowRestPose();
            }
        }
    }

    private Vector3 GetStrikeContact(Vector2 reach)
    {
        var pose = profile.strike != null && profile.strike.sprite != null ? profile.strike : profile.idle;
        float height = standingHeight * Mathf.Max(0.01f, pose.height);
        float width = height * pose.sprite.rect.width / pose.sprite.rect.height;
        Vector2 point = profile.weaponImpactPoint - pose.groundPivot;
        Vector3 local = restRotation * Vector3.Scale(new Vector3(point.x * width, point.y * height, 0f), baseScale);
        var rt = body.rectTransform;
        Vector3 anchorOffset = rt.localPosition - (Vector3)rt.anchoredPosition;
        return rt.parent.TransformPoint(anchorOffset + (Vector3)(ground + pose.offset + reach) + local);
    }

    public void Finish(bool won, bool immediate = false)
    {
        ClearDaze();
        shieldActive = false;
        shieldBlockRemaining = 0f;
        if (immediate)
        {
            attackVfx?.Clear();
            poseVersion++;
            attacking = false;
        }
        if (won && attacking)
        {
            victoryPending = true;
            return;
        }
        victoryPending = false;
        finished = true;
        Show(won ? profile.victory : profile.defeated);
    }

    public void PlayDazed()
    {
        Finish(false, immediate: true);
        dazed = true;
        dazeTime = 0f;
        if (dazeRoot == null)
        {
            var root = new GameObject("DazeStars", typeof(RectTransform));
            dazeRoot = (RectTransform)root.transform;
            dazeRoot.SetParent(body.rectTransform.parent, false);
            BossDuelController.MatchParentLayer(dazeRoot);
            dazeRoot.anchorMin = body.rectTransform.anchorMin;
            dazeRoot.anchorMax = body.rectTransform.anchorMax;
            dazeRoot.sizeDelta = Vector2.zero;
            dazeStars = new RectTransform[3];
            for (int i = 0; i < dazeStars.Length; i++)
            {
                var go = new GameObject("Star", typeof(RectTransform), typeof(CanvasRenderer), typeof(BossDuelStatusGraphic));
                go.transform.SetParent(dazeRoot, false);
                BossDuelController.MatchParentLayer(go.transform);
                var star = go.GetComponent<BossDuelStatusGraphic>();
                star.Star = true;
                star.color = new Color(1f, 0.84f, 0.18f, 1f);
                star.raycastTarget = false;
                dazeStars[i] = star.rectTransform;
                dazeStars[i].sizeDelta = Vector2.one * standingHeight * 0.085f;
            }
        }
        dazeRoot.gameObject.SetActive(true);
        UpdateDaze(0f);
    }

    private void UpdateDaze(float dt)
    {
        dazeTime += dt;
        float settle = Mathf.Clamp01(dazeTime / 0.35f);
        float direction = profile.mirrorHorizontally ? -1f : 1f;
        Show(profile.defeated, new Vector2(Mathf.Sin(dazeTime * 13f) * standingHeight * 0.012f * (1f - settle), 0f));
        body.rectTransform.localRotation = restRotation * Quaternion.Euler(0f, 0f,
            direction * (7f * settle + Mathf.Sin(dazeTime * 3f) * 1.5f));
        if (dazeRoot == null) return;
        dazeRoot.anchoredPosition = ground + new Vector2(0f, standingHeight * 0.94f);
        for (int i = 0; i < dazeStars.Length; i++)
        {
            float angle = dazeTime * 3.8f + i * Mathf.PI * 2f / dazeStars.Length;
            dazeStars[i].anchoredPosition = new Vector2(Mathf.Cos(angle) * standingHeight * 0.2f,
                Mathf.Sin(angle) * standingHeight * 0.055f);
            dazeStars[i].localRotation = Quaternion.Euler(0f, 0f, -dazeTime * 70f);
            dazeStars[i].localScale = Vector3.one * (0.85f + Mathf.Sin(angle) * 0.15f);
        }
    }

    private void ClearDaze()
    {
        dazed = false;
        if (dazeRoot != null) dazeRoot.gameObject.SetActive(false);
        if (body != null) body.rectTransform.localRotation = restRotation;
    }

    private void OnDisable()
    {
        ClearDaze();
        attackVfx?.Clear();
    }

    private void OnDestroy()
    {
        if (dazeRoot != null) Destroy(dazeRoot.gameObject);
    }

    public void ResetForWave()
    {
        ClearDaze();
        attackVfx?.Clear();
        shieldActive = false;
        shieldBlockRemaining = 0f;
        poseVersion++;
        attacking = false;
        victoryPending = false;
        finished = false;
        focused = false;
        idleTime = 0f;
        Show(profile.idle);
    }
}
