using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bir WonderDefinition'dan sahneyi runtime kurar: arka plan (reveal shader'lı) +
/// kaynakçı + karakterler (her biri kendi yolu). Ana menüde/mission sahnesinde
/// bu prefab bir definition alır, gerisini kendisi doğurur. [[project_wonder_reveal_background]]
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class WonderScene : MonoBehaviour
{
    [Header("Kaynaklar (prefab'da ata)")]
    public Shader revealShader;
    public Sprite weldLightSprite;

    [Header("Veri")]
    public WonderDefinition definition;
    public bool buildOnStart = false;

    [Header("Davranış")]
    [Tooltip("Açık: karakterler hemen yürür (test). Kapalı: reveal %100 olunca.")]
    public bool charactersWalkImmediately = false;
    [Tooltip("Kapalı: karakterleri HİÇ kurma (reveal overlay'inde robot/dron olmasın)")]
    public bool includeCharacters = true;

    public WonderRevealView View { get; private set; }
    RectTransform _rt;

    void Awake() => _rt = (RectTransform)transform;

    void Start()
    {
        if (buildOnStart && definition != null) Build(definition);
    }

    /// <summary>Mevcut içeriği temizler, verilen definition'dan sahneyi kurar.</summary>
    public WonderRevealView Build(WonderDefinition def)
    {
        if (_rt == null) _rt = (RectTransform)transform;
        definition = def;
        ClearBuilt();

        var shader = revealShader != null ? revealShader : Shader.Find("UI/WonderReveal");

        // --- Arka plan (tam ekran cover + reveal) ---------------------
        var bg = NewChild("WonderBackground", typeof(Image), typeof(AspectRatioFitter), typeof(WonderRevealView));
        var bgImg = bg.GetComponent<Image>();
        bgImg.sprite = def.backgroundSprite;
        bgImg.preserveAspect = false;
        if (shader != null) bgImg.material = new Material(shader) { name = $"WonderReveal_{def.wonderId}" };
        var fitter = bg.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        if (def.backgroundSprite != null)
            fitter.aspectRatio = (float)def.backgroundSprite.texture.width / def.backgroundSprite.texture.height;

        var view = bg.GetComponent<WonderRevealView>();
        view.wonderId = def.wonderId;
        view.totalStages = def.TaskCount;
        view.welderFrames = def.welderFrames;
        view.welderFps = def.welderFps;
        view.previewReveal = 0f;
        var bgRt = (RectTransform)bg.transform;

        // --- Kaynakçı -------------------------------------------------
        BuildWelder(bgRt, view, def);

        // --- Karakterler ----------------------------------------------
        var agents = new List<WonderAmbientAgent>();
        if (includeCharacters && def.characters != null)
            foreach (var c in def.characters)
                agents.Add(BuildCharacter(bgRt, c));
        view.ambientAgents = agents.ToArray();

        View = view;
        return view;
    }

    void BuildWelder(RectTransform parent, WonderRevealView view, WonderDefinition def)
    {
        if (def.welderFrames == null || def.welderFrames.Length == 0) return;

        var container = NewChild("WelderRobot", parent);
        var cRt = (RectTransform)container.transform;
        cRt.sizeDelta = def.welderSize;
        cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);
        cRt.anchoredPosition = def.welderHome;

        // Robot (opak)
        var robot = NewChild("RobotSprite", cRt, typeof(Image));
        Stretch((RectTransform)robot.transform);
        var robotImg = robot.GetComponent<Image>();
        robotImg.sprite = def.welderFrames[0];
        robotImg.preserveAspect = true;
        robotImg.raycastTarget = false;

        // Kaynak arkı ışığı (önde, titreşir)
        if (weldLightSprite != null)
        {
            var light = NewChild("WeldLight", cRt, typeof(Image));
            var lRt = (RectTransform)light.transform;
            lRt.sizeDelta = new Vector2(150, 150);
            lRt.anchoredPosition = new Vector2(18, -78);
            var lImg = light.GetComponent<Image>();
            lImg.sprite = weldLightSprite;
            lImg.color = view.weldLightColor;
            lImg.raycastTarget = false;
            light.SetActive(false);
            view.weldLight = lImg;
        }

        view.welderRobot = cRt;
        view.welderImage = robotImg;
        container.SetActive(false);   // kaynak başlayınca AnimateTo açar (ortada flaş olmasın)
    }

    WonderAmbientAgent BuildCharacter(RectTransform parent, WonderCharacter c)
    {
        // Konteyner inaktif kur → alanları set et → aktifleştir (Awake veriyle çalışsın)
        var containerGo = new GameObject(string.IsNullOrEmpty(c.name) ? "Character" : c.name,
            typeof(RectTransform), typeof(WonderAmbientAgent));
        containerGo.SetActive(false);
        var cRt = (RectTransform)containerGo.transform;
        cRt.SetParent(parent, false);
        cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);

        var visual = NewChild("Visual", cRt, typeof(Image));
        var vRt = (RectTransform)visual.transform;
        vRt.sizeDelta = new Vector2(c.visualSize, c.visualSize);
        var vImg = visual.GetComponent<Image>();
        vImg.preserveAspect = true;
        vImg.raycastTarget = false;
        vImg.sprite = FirstFrame(c);

        var agent = containerGo.GetComponent<WonderAmbientAgent>();
        agent.visual = vRt;
        agent.visualImage = vImg;
        agent.facingMode = c.facingMode;
        agent.frontFrames = c.frontFrames;
        agent.backFrames = c.backFrames;
        agent.walkFrames = c.walkFrames;
        agent.walkFps = c.walkFps;
        agent.mirrorBySide = c.mirrorBySide;
        agent.speed = c.speed;
        agent.bobAmplitude = c.bobAmplitude;
        agent.bobFrequency = c.bobFrequency;
        agent.pingPong = c.pingPong;
        agent.pauseAtPoint = c.pauseAtPoint;
        agent.pathPoints = c.path;
        agent.startWalking = charactersWalkImmediately;
        if (c.path != null && c.path.Length > 0) cRt.anchoredPosition = c.path[0];

        containerGo.SetActive(true);
        return agent;
    }

    static Sprite FirstFrame(WonderCharacter c)
    {
        if (c.frontFrames != null && c.frontFrames.Length > 0) return c.frontFrames[0];
        if (c.walkFrames != null && c.walkFrames.Length > 0) return c.walkFrames[0];
        if (c.backFrames != null && c.backFrames.Length > 0) return c.backFrames[0];
        return null;
    }

    void ClearBuilt()
    {
        for (int i = _rt.childCount - 1; i >= 0; i--)
        {
            var ch = _rt.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(ch); else DestroyImmediate(ch);
        }
    }

    // ---- UI yardımcıları ----------------------------------------------
    GameObject NewChild(string name, params System.Type[] comps)
        => NewChild(name, _rt, comps);

    // Merkez-anchor'lı çocuk oluşturur (stretch YOK). Gerekirse çağıran Stretch eder.
    static GameObject NewChild(string name, Transform parent, params System.Type[] comps)
    {
        var types = new List<System.Type> { typeof(RectTransform) };
        types.AddRange(comps);
        var go = new GameObject(name, types.ToArray()) { layer = parent.gameObject.layer };
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        return go;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
