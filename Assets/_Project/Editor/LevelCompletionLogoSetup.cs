using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Level-sonu logo animasyonunun dört parçasını tek tıkla kurar.
///
/// Menü:  TinyFixers > Logo > Setup Level Completion Logo   (açık sahneye uygular)
/// Batch: -executeMethod LevelCompletionLogoSetup.SetupBatch (01_Game'i açar, kurar, kaydeder)
///
/// Parçalar (altta -> üstte): LogoFrame, LogoAnimal, LogoWonder, LogoFixers.
/// Hepsi aynı 607x627 tuvalde çizili olduğu için dördü de merkeze konur;
/// ayrı ayrı konumlandırma gerekmez.
///
/// Eski logo parçaları SİLİNMEZ, yalnızca kapatılır — yeni logo doğrulandıktan
/// sonra elle silinebilir. Overlay, VFX kökü ve skip butonu korunur.
/// </summary>
public static class LevelCompletionLogoSetup
{
    const string PartsDir = "Assets/_Project/Art/UI/Logos/LogoParts/";
    const string ScenePath = "Assets/_Project/Scenes/01_Game.unity";
    const float PartWidth = 607f;
    const float PartHeight = 627f;

    // Havuz: her oynatımda birisi random seçilir.
    static readonly string[] AnimalFiles = { "Piglogo", "BearLogo", "Ramlogo", "RabitLogo" };

    [MenuItem("TinyFixers/Logo/Setup Level Completion Logo")]
    public static void Setup()
    {
        bool ok = Run(out string message);
        EditorUtility.DisplayDialog("Logo Setup", message, "Tamam");

        if (ok)
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
    }

    /// <summary>
    /// Batch giriş noktası: sahneyi açar, kurar ve KAYDEDER.
    /// Unity kapalıyken komut satırından çalıştırılır.
    /// </summary>
    public static void SetupBatch()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        if (!scene.IsValid())
        {
            Debug.LogError("[LogoSetup] Sahne açılamadı: " + ScenePath);
            EditorApplication.Exit(1);
            return;
        }

        if (!Run(out string message))
        {
            Debug.LogError("[LogoSetup] " + message);
            EditorApplication.Exit(1);
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("[LogoSetup] Sahne kaydedilemedi: " + ScenePath);
            EditorApplication.Exit(1);
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[LogoSetup] BASARILI\n" + message);
        EditorApplication.Exit(0);
    }

    // -------------------------------------------------------------------------
    /// <summary>Asıl iş. Sahneyi açmaz, kaydetmez, dialog göstermez.</summary>
    static bool Run(out string message)
    {
        var anim = Object.FindFirstObjectByType<LevelCompletionLogoAnimation>(FindObjectsInactive.Include);
        if (anim == null)
        {
            message = "Aktif sahnede LevelCompletionLogoAnimation bulunamadı.\n" +
                      "01_Game sahnesini açıp tekrar dene.";
            return false;
        }

        var frameSprite = Load("LogoFrame");
        var wonderSprite = Load("wonderlogo");
        var fixersSprite = Load("FixersLogo");

        if (frameSprite == null || wonderSprite == null || fixersSprite == null)
        {
            message = "Logo sprite'ları yüklenemedi (" + PartsDir + ").\n" +
                      "Texture Type = Sprite (2D and UI) olduğundan emin ol.";
            return false;
        }

        var animals = new List<Sprite>();
        foreach (var file in AnimalFiles)
        {
            var sprite = Load(file);
            if (sprite != null)
                animals.Add(sprite);
        }

        if (animals.Count == 0)
        {
            message = "Hiçbir hayvan sprite'ı yüklenemedi: " + PartsDir;
            return false;
        }

        var root = anim.transform;
        var so = new SerializedObject(anim);

        // Bu ikisi korunacak; eski parçaları kapatırken bunlara dokunulmamalı.
        var overlay = so.FindProperty("overlayImage").objectReferenceValue as Image;
        var vfxRoot = so.FindProperty("vfxRoot").objectReferenceValue as RectTransform;

        var frame = EnsurePart(root, "LogoFrame", frameSprite);
        var animal = EnsurePart(root, "LogoAnimal", animals[0]);
        var wonder = EnsurePart(root, "LogoWonder", wonderSprite);
        var fixers = EnsurePart(root, "LogoFixers", fixersSprite);

        // Katman sırası: altta Frame, üstte Fixers.
        frame.SetAsLastSibling();
        animal.SetAsLastSibling();
        wonder.SetAsLastSibling();
        fixers.SetAsLastSibling();

        // VFX kökü logonun üstünde kalsın ki havai fişek önde patlasın.
        if (vfxRoot != null && vfxRoot.parent == root)
            vfxRoot.SetAsLastSibling();

        int disabled = DisableStaleParts(root, overlay, vfxRoot, frame, animal, wonder, fixers);

        so.FindProperty("frameImage").objectReferenceValue = frame;
        so.FindProperty("animalImage").objectReferenceValue = animal;
        so.FindProperty("wonderImage").objectReferenceValue = wonder;
        so.FindProperty("fixersImage").objectReferenceValue = fixers;

        var pool = so.FindProperty("animalSprites");
        pool.arraySize = animals.Count;
        for (int i = 0; i < animals.Count; i++)
            pool.GetArrayElementAtIndex(i).objectReferenceValue = animals[i];

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(anim);

        message = "Kuruldu.\n\n" +
                  "Parçalar: LogoFrame > LogoAnimal > LogoWonder > LogoFixers\n" +
                  "Hayvan havuzu: " + animals.Count + " sprite\n" +
                  "Kapatılan eski parça: " + disabled;
        return true;
    }

    // -------------------------------------------------------------------------
    static RectTransform EnsurePart(Transform root, string name, Sprite sprite)
    {
        var existing = root.Find(name);
        GameObject go = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));

        if (existing == null)
            go.transform.SetParent(root, false);

        // Screen Space Camera canvas'ta yanlış layer görünmezliğe yol açar.
        go.layer = root.gameObject.layer;
        go.SetActive(true);

        if (!go.TryGetComponent<RectTransform>(out var rt))
            rt = go.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localRotation = Quaternion.identity;
        rt.sizeDelta = new Vector2(PartWidth, PartHeight);

        if (!go.TryGetComponent<CanvasGroup>(out var group))
            group = go.AddComponent<CanvasGroup>();
        group.alpha = 1f;
        group.interactable = false;
        group.blocksRaycasts = false;

        if (!go.TryGetComponent<Image>(out var image))
            image = go.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        image.enabled = true;

        return rt;
    }

    /// <summary>
    /// Eski logo parçalarını kapatır. Overlay, VFX kökü ve içinde herhangi bir
    /// buton barındıran çocuklar (bonus tur skip butonu) korunur.
    /// </summary>
    static int DisableStaleParts(Transform root, Image overlay, RectTransform vfxRoot,
        params RectTransform[] keep)
    {
        int disabled = 0;

        foreach (Transform child in root)
        {
            bool isKeeper = false;
            for (int i = 0; i < keep.Length; i++)
            {
                if (keep[i] != null && child == keep[i].transform)
                {
                    isKeeper = true;
                    break;
                }
            }

            if (isKeeper)
                continue;

            if (vfxRoot != null && (child == vfxRoot.transform || vfxRoot.IsChildOf(child)))
                continue;

            if (overlay != null && (child == overlay.transform || overlay.transform.IsChildOf(child)))
                continue;

            // Skip butonu bu kökün altında yaşayabiliyor; tıklanabilir hiçbir şeyi kapatma.
            if (child.GetComponentInChildren<Selectable>(true) != null)
                continue;

            if (child.GetComponentInChildren<Image>(true) == null)
                continue;

            child.gameObject.SetActive(false);
            disabled++;
        }

        return disabled;
    }

    static Sprite Load(string file)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(PartsDir + file + ".png");
    }
}
