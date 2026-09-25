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
    public bool HasThrowAnimation
    {
        get
        {
            if (profile == null || profile.throwFrames == null || profile.throwFrames.Length == 0) return false;
            foreach (var frame in profile.throwFrames)
                if (frame == null || frame.pose == null || frame.pose.sprite == null) return false;
            return true;
        }
    }

    public float HeldObstacleWorldSize => body.rectTransform.TransformVector(
        Vector3.up * standingHeight * profile.heldObstacleSize).magnitude;

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

    // Only yields individual frames so the volley can advance flight and follow-through together.
    // Shares attack ownership with melee: idle, blink and shield updates cannot replace a throw pose.
    public IEnumerator ThrowObstacle(Action<Vector3> trackHand, Action release, Func<bool> cancelled)
    {
        if (attacking || finished || body == null || !HasThrowAnimation) yield break;
        int version = poseVersion;
        attacking = true;
        try
        {
            Show(profile.idle);
            for (float time = 0f; time < profile.throwIdleLeadIn; time += Time.deltaTime)
            {
                if (!isActiveAndEnabled || finished || version != poseVersion || cancelled()) yield break;
                yield return null;
            }
            float fps = Mathf.Clamp(profile.throwFramesPerSecond, 1f, 30f);
            int releaseFrame = Mathf.Clamp(profile.throwReleaseFrame, 0, profile.throwFrames.Length - 1);
            bool released = false;
            for (float time = 0f; ; time += Time.deltaTime)
            {
                if (!isActiveAndEnabled || finished || version != poseVersion || cancelled()) yield break;
                int index = Mathf.Min((int)(time * fps), profile.throwFrames.Length - 1);
                var frame = profile.throwFrames[index];
                Show(frame.pose);
                if (!released)
                {
                    var rt = body.rectTransform;
                    Vector2 hand = frame.handPoint - rt.pivot;
                    trackHand?.Invoke(rt.TransformPoint(new Vector3(hand.x * rt.rect.width, hand.y * rt.rect.height, 0f)));
                    if (index >= releaseFrame)
                    {
                        released = true;
                        release?.Invoke();
                    }
                }
                if (time >= profile.throwFrames.Length / fps) break;
                yield return null;
            }
        }
        finally
        {
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

    public IEnumerator Attack(RectTransform target, Action impact, Func<bool> cancelled, int power,
        Func<bool> finishingStrike = null, Action onSwing = null)
    {
        if (attacking || finished || body == null || profile == null) yield break;
        int version = poseVersion;
        bool landed = false;
        bool finisher = profile.enableFinishingStrike && finishingStrike != null && finishingStrike();
        attacking = true;
        try
        {
            float windup = Mathf.Max(0.02f, profile.windupDuration);
            if (finisher) windup *= Mathf.Clamp(profile.finisherWindupMultiplier, 1f, 3f);
            if (profile.windupFrames != null && profile.windupFrames.Length > 0)
            {
                for (float t = 0f; t < windup; t += Time.deltaTime)
                {
                    if (finished || version != poseVersion || cancelled()) yield break;
                    Show(SelectAttackPose(profile.windupFrames, t / windup, profile.windup));
                    yield return null;
                }
            }
            else
            {
                Show(profile.windup);
                yield return new WaitForSeconds(windup);
            }
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
            float jumpHeight = standingHeight * Mathf.Clamp(profile.finisherJumpHeight, 0.05f, 0.35f);
            const float slowArcEnd = 0.6f;
            // Local slow hop: cascades, input, counters and global Time.timeScale are untouched.
            // Keep the weapon sweep for the fast contact phase so it does not hang over the target.
            if (finisher)
            {
                float approach = Mathf.Clamp(profile.finisherApproachDuration, 0.05f, 1f);
                attackVfx.BeginTrail();
                for (float t = 0f; t < approach; t += Time.deltaTime)
                {
                    if (finished || version != poseVersion || cancelled()) yield break;
                    // Keep the raised weapon through the slow hop; swing only on the fast descent.
                    Show(profile.windup, FinisherArcOffset(reach, slowArcEnd * Mathf.Clamp01(t / approach), jumpHeight));
                    attackVfx.TickTrail(Time.deltaTime);
                    yield return null;
                }
                duration = Mathf.Clamp(profile.finisherBurstDuration, 0.03f, 0.15f);
            }
            if (finished || version != poseVersion || cancelled()) yield break;
            // Finisher audio starts with the fast strike, after the slow airborne approach.
            onSwing?.Invoke();
            attackVfx.BeginTrail();
            attackVfx.BeginSwing(power, GetStrikeContact(reach), target);
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (finished || version != poseVersion || cancelled()) yield break;
                float progress = Mathf.Clamp01(t / duration);
                Vector2 motion = finisher
                    ? FinisherArcOffset(reach, slowArcEnd + (1f - slowArcEnd) * progress, jumpHeight)
                    : Vector2.Lerp(Vector2.zero, reach, progress);
                Show(swingPose, motion);
                attackVfx.TickTrail(Time.deltaTime);
                attackVfx.TickSwing(Mathf.Clamp01(t / duration));
                yield return null;
            }
            if (finished || version != poseVersion || cancelled()) yield break;
            Show(profile.strike, reach);
            attackVfx.ReleaseSwing();
            // Shield pickups can arrive while the player continues swapping during anticipation.
            // Recheck before contact: do not celebrate a hit that is no longer lethal.
            bool finishingImpact = finisher && finishingStrike();
            attackVfx.PlayImpact(power, finishingImpact);
            landed = true;
            impact?.Invoke();
            if (finished) yield break;
            yield return new WaitForSeconds(finishingImpact
                ? Mathf.Clamp(profile.finisherImpactHold, 0.02f, 0.3f)
                : Mathf.Max(0.02f, profile.impactDuration));

            // Once the hit has landed, dash back even if it ended the wave or won the duel.
            // Defeat/reset can still interrupt; victory is displayed only after landing at home.
            duration = Mathf.Max(0.02f, profile.recoveryDuration);
            attackVfx.BeginTrail();
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                if (finished || version != poseVersion) yield break;
                float progress = Mathf.Clamp01(t / duration);
                Show(SelectAttackPose(profile.recoveryFrames, progress, profile.focused),
                    Vector2.Lerp(reach, Vector2.zero, progress));
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

    private static BossDuelCharacterProfile.Pose SelectAttackPose(
        BossDuelCharacterProfile.AttackPoseFrame[] frames, float progress, BossDuelCharacterProfile.Pose fallback)
    {
        if (frames == null || frames.Length == 0) return fallback;
        float total = 0f;
        foreach (var frame in frames)
            if (frame != null && frame.pose != null && frame.pose.sprite != null)
                total += Mathf.Max(0.01f, frame.weight);
        if (total <= 0f) return fallback;
        float remaining = Mathf.Clamp01(progress) * total;
        var last = fallback;
        foreach (var frame in frames)
        {
            if (frame == null || frame.pose == null || frame.pose.sprite == null) continue;
            last = frame.pose;
            remaining -= Mathf.Max(0.01f, frame.weight);
            if (remaining < 0f) return last;
        }
        return last;
    }

    private static Vector2 FinisherArcOffset(Vector2 reach, float progress, float height)
    {
        float t = Mathf.Clamp01(progress);
        // Continuous parabola: lift off at home, crest halfway, land exactly at the strike.
        return new Vector2(reach.x * t, reach.y * t + 4f * height * t * (1f - t));
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
