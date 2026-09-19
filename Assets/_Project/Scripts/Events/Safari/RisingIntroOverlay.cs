using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Rising'e özel giriş/matching overlay'i. Popup "Devam"dan sonra açılır, katılımcı sayacını doldurur,
/// sonra oluşan avatar grubunu RisingMapScreen'deki hazır pozisyona taşır.
/// </summary>
public sealed class RisingIntroOverlay : MonoBehaviour
{
    [Header("Kök")]
    [SerializeField] private GameObject root;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private RisingMapScreen mapScreen;

    [Header("Görseller")]
    [SerializeField] private Image goldPileImage;
    [SerializeField] private Image flagImage;
    [SerializeField] private Image liftImage;

    [Header("Metin")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text counterText;
    [SerializeField] private TMP_Text tapText;

    [Header("Kalabalık")]
    [SerializeField] private SafariAvatarStackView crowdStack;
    [SerializeField] private RectTransform crowdAnchor;
    [SerializeField, Min(1)] private int maxVisibleCrowdAvatars = 8;
    [SerializeField, Min(16f)] private float crowdAvatarSize = 112f;
    [SerializeField, Min(0f)] private float crowdSpread = 58f;
    [SerializeField, Min(16f)] private float transferTargetAvatarSize = 112f;

    [Header("Animasyon")]
    [SerializeField, Min(0.1f)] private float countDuration = 2.4f;
    [SerializeField, Min(0.1f)] private float transferDuration = 0.9f;
    [SerializeField, Min(0f)] private float transferHop = 130f;

    private SafariEventController controller;
    private Coroutine active;
    private bool showRequested;   // Show() ile mi uyandık, yoksa editörde aktif mi bırakıldık?
    private readonly List<RectTransform> transferringAvatars = new();

    private void Awake()
    {
        if (root != null && root != gameObject)
            root.SetActive(false);
        // root == bu obje: editörde aktif bırakılmışsa ana menüyü kapatmasın (Show yolu showRequested ile muaf).
        else if (!showRequested)
            gameObject.SetActive(false);
        ApplyStaticText();
    }

    public void Show(SafariEventController owner)
    {
        controller = owner;
        showRequested = true;
        gameObject.SetActive(true);
        if (root != null) root.SetActive(true);
        transform.SetAsLastSibling();

        if (active != null)
            StopCoroutine(active);
        ClearTransferringAvatars();
        active = StartCoroutine(Run());
    }

    public void Hide()
    {
        if (active != null)
        {
            StopCoroutine(active);
            active = null;
        }
        ClearTransferringAvatars();
        if (root != null) root.SetActive(false);
    }

    private void OnDisable()
    {
        if (active != null) { StopCoroutine(active); active = null; }
        ClearTransferringAvatars();
    }

    private void ClearTransferringAvatars()
    {
        foreach (var avatar in transferringAvatars)
            if (avatar != null) Destroy(avatar.gameObject);
        transferringAvatars.Clear();
    }

    private IEnumerator Run()
    {
        ApplyStaticText();
        SetBackgroundAlpha(1f);
        SetDecorationsVisible(true);
        if (tapText != null)
            tapText.gameObject.SetActive(false);

        int total = TotalParticipants();
        var participants = SafariParticipantPool.Build(total, CurrentLevel.Global, seed: 1);
        if (crowdStack != null)
        {
            if (crowdAnchor != null)
                crowdStack.Container.position = crowdAnchor.position;
            crowdStack.Build(participants, Mathf.Min(maxVisibleCrowdAvatars, total), crowdAvatarSize, crowdSpread);
        }

        yield return CountParticipants(total);

        if (tapText != null)
            tapText.gameObject.SetActive(true);

        yield return WaitForTapBlink();
        yield return TransferToMap();
    }

    private IEnumerator CountParticipants(int total)
    {
        if (counterText != null)
            counterText.text = $"0 / {total}\n<size=36>OYUNCULAR HAZIRLANIYOR</size>";

        var avatars = crowdStack != null ? crowdStack.SnapshotAvatars() : new List<RectTransform>();
        var scales = new Vector3[avatars.Count];
        for (int i = 0; i < avatars.Count; i++)
            if (avatars[i] != null)
                scales[i] = avatars[i].localScale;
        HideCrowd();

        int shown = 0;
        float t = 0f;
        while (t < countDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / countDuration);
            float e = Mathf.SmoothStep(0f, 1f, k);
            int value = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1, total, e)), 1, total);

            if (counterText != null)
                counterText.text = $"{value} / {total}\n<size=36>OYUNCULAR HAZIRLANIYOR</size>";

            int visibleTarget = Mathf.Clamp(Mathf.CeilToInt(value / (float)total * avatars.Count), 0, avatars.Count);
            for (int i = shown; i < visibleTarget; i++)
            {
                if (avatars[i] == null) continue;
                avatars[i].localScale = scales[i];
                SetAlpha(avatars[i], 1f);
            }
            shown = Mathf.Max(shown, visibleTarget);
            yield return null;
        }

        if (counterText != null)
            counterText.text = $"{total} / {total}\n<size=36>YARIŞ BAŞLIYOR!</size>";
        for (int i = 0; i < avatars.Count; i++)
        {
            if (avatars[i] == null) continue;
            avatars[i].localScale = scales[i];
            SetAlpha(avatars[i], 1f);
        }
    }

    private IEnumerator WaitForTapBlink()
    {
        float t = 0f;
        while (!WasTap())
        {
            t += Time.unscaledDeltaTime;
            if (tapText != null)
            {
                var c = tapText.color;
                c.a = Mathf.Lerp(0.32f, 1f, (Mathf.Sin(t * 5.5f) + 1f) * 0.5f);
                tapText.color = c;
            }
            yield return null;
        }
    }

    private IEnumerator TransferToMap()
    {
        if (mapScreen == null || crowdStack == null)
        {
            controller?.OpenMapFromIntro();
            active = null;
            Hide();
            yield break;
        }

        Vector3 sourceCenter = crowdStack.Container.position;
        Vector3 targetCenter = mapScreen.PrepareIntroTarget(controller);
        Transform host = root != null ? root.transform : transform;
        var movers = crowdStack.DetachAll(host);
        if (movers.Count == 0)
        {
            controller?.OpenMapFromIntro();
            active = null;
            Hide();
            yield break;
        }

        transferringAvatars.AddRange(movers);
        SetDecorationsVisible(false);

        var starts = new Vector3[movers.Count];
        var targets = new Vector3[movers.Count];
        var rotations = new Quaternion[movers.Count];
        var startScales = new Vector3[movers.Count];
        var targetScales = new Vector3[movers.Count];
        float transferScale = transferTargetAvatarSize / Mathf.Max(1f, crowdAvatarSize);
        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] == null) continue;
            starts[i] = movers[i].localPosition;
            targets[i] = host.InverseTransformPoint(targetCenter)
                + (starts[i] - host.InverseTransformPoint(sourceCenter)) * transferScale;
            rotations[i] = movers[i].localRotation;
            startScales[i] = movers[i].localScale;
            targetScales[i] = startScales[i] * transferScale;
        }

        for (int i = movers.Count - 1; i >= 0; i--) movers[i].SetAsLastSibling();
        float duration = Mathf.Max(0.55f, transferDuration);
        const float stagger = 0.045f;
        float totalDuration = duration + (movers.Count - 1) * stagger;
        var tookOff = new bool[movers.Count];
        var landed = new bool[movers.Count];
        float t = 0f;
        while (t < totalDuration)
        {
            t += Time.unscaledDeltaTime;
            SetBackgroundAlpha(1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration)));
            for (int i = 0; i < movers.Count; i++)
            {
                if (movers[i] == null) continue;
                float k = Mathf.Clamp01((t - i * stagger) / duration);
                Vector3 scale = Vector3.Lerp(startScales[i], targetScales[i], Mathf.SmoothStep(0f, 1f, k));
                RisingMapScreen.ApplyJumpPose(movers[i], starts[i], targets[i], rotations[i], scale, k, transferHop);
                if (k >= 0.12f && !tookOff[i])
                {
                    tookOff[i] = true;
                    if (i % 2 == 0) mapScreen.PlayJumpSound(false, i / 2);
                }
                if (k >= 0.82f && !landed[i])
                {
                    landed[i] = true;
                    if (i % 2 == 0) mapScreen.PlayJumpSound(true, i / 2);
                }
            }
            yield return null;
        }
        for (int i = 0; i < movers.Count; i++)
        {
            if (movers[i] == null) continue;
            movers[i].localPosition = targets[i];
            movers[i].localRotation = rotations[i];
            movers[i].localScale = targetScales[i];
        }
        transferringAvatars.Clear();

        mapScreen.AdoptIntroCrowd(movers);
        active = null;
        Hide();
    }

    private int TotalParticipants()
    {
        var cfg = controller != null ? controller.Config : null;
        return Mathf.Max(1, cfg != null ? cfg.participantVisualCount : 100);
    }

    private void ApplyStaticText()
    {
        if (titleText != null)
            titleText.text = LocalizedText("rising_intro_title", "SAFARİ BAŞLIYOR!");
        if (tapText != null)
            tapText.text = LocalizedText("rising_intro_ready", "ZİRVEYE HAZIR MISIN?\n<size=38>Devam etmek için dokun</size>");
    }

    private static string LocalizedText(string key, string fallback)
    {
        string value = GameLocalization.Get(key);
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }

    private void HideCrowd()
    {
        var avatars = crowdStack.SnapshotAvatars();
        for (int i = 0; i < avatars.Count; i++)
        {
            if (avatars[i] == null) continue;
            avatars[i].localScale = Vector3.zero;
            SetAlpha(avatars[i], 0f);
        }
    }

    private void SetDecorationsVisible(bool visible)
    {
        if (goldPileImage != null) goldPileImage.gameObject.SetActive(visible);
        if (flagImage != null) flagImage.gameObject.SetActive(visible);
        if (liftImage != null) liftImage.gameObject.SetActive(visible);
        if (titleText != null) titleText.gameObject.SetActive(visible);
        if (counterText != null) counterText.gameObject.SetActive(visible);
        if (tapText != null) tapText.gameObject.SetActive(visible && tapText.gameObject.activeSelf);
    }

    private void SetBackgroundAlpha(float alpha)
    {
        if (backgroundImage == null) return;
        var c = backgroundImage.color;
        c.a = Mathf.Clamp01(alpha);
        backgroundImage.color = c;
    }

    private static void SetAlpha(RectTransform rt, float alpha)
    {
        var images = rt.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            var c = images[i].color;
            c.a = alpha;
            images[i].color = c;
        }
    }

    private static bool WasTap()
    {
        if (Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame)
            return true;
        if (Touchscreen.current != null)
        {
            var touch = Touchscreen.current.primaryTouch;
            if (touch.press.wasReleasedThisFrame)
                return true;
        }
        return false;
    }
}
