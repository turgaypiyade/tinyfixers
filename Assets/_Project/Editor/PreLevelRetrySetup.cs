using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Fail sonrası "Tekrar Dene" akışı için pre-level popup'ı game sahnesinde ANINDA (sahne yüklemeden)
/// açabilmek amacıyla, popup'ı tek kaynak (prefab) yapıp game sahnesine yerleştirir.
///
/// İki adım (script mevcut AÇIK sahne üzerinde çalışır — kod tabanının diğer setup'larıyla aynı desen):
///   1) MainMenu sahnesi açıkken: TinyFixers ▸ Setup ▸ PreLevel Retry ▸ 1. Prefab Oluştur
///      → sahnedeki PreLevelSpecialPopup'ı prefab'a çevirir (instance bağlı kalır, MainMenu çalışır).
///   2) 01_Game sahnesi açıkken: TinyFixers ▸ Setup ▸ PreLevel Retry ▸ 2. Game Sahnesine Ekle
///      → prefab'ı levelend canvas'ına, tam ekran + en öne yerleştirir.
///
/// Sonra: level kaybet → fail → cancel → cancel → popup anında levelend üstünde açılır.
/// (Runtime kodu LevelEndSimplePopupController + PreLevelSpecialPopupController içinde zaten hazır.)
/// </summary>
public static class PreLevelRetrySetup
{
    private const string PrefabDir  = "Assets/_Project/Prefabs/UI";
    private const string PrefabPath = PrefabDir + "/PreLevelSpecialPopup.prefab";

    [MenuItem("TinyFixers/Setup/PreLevel Retry/1. Prefab Oluştur (MainMenu açıkken)")]
    public static void CreatePrefab()
    {
        var popup = Object.FindFirstObjectByType<PreLevelSpecialPopupController>(FindObjectsInactive.Include);
        if (popup == null)
        {
            EditorUtility.DisplayDialog("PreLevel Retry",
                "Sahnede PreLevelSpecialPopupController bulunamadı.\nMainMenu sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        MockupUI.EnsureFolder(PrefabDir);

        GameObject root = popup.gameObject;
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            root, PrefabPath, InteractionMode.UserAction, out bool success);

        if (!success || prefab == null)
        {
            EditorUtility.DisplayDialog("PreLevel Retry",
                "Prefab oluşturulamadı. Konsolu kontrol et.", "Tamam");
            return;
        }

        EditorSceneManager.MarkSceneDirty(popup.gameObject.scene);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog("PreLevel Retry",
            $"Prefab oluşturuldu:\n{PrefabPath}\n\nMainMenu sahnesini kaydet (Cmd+S).\n" +
            "Sonra 01_Game sahnesini aç ve 2. adımı çalıştır.", "Tamam");
    }

    [MenuItem("TinyFixers/Setup/PreLevel Retry/2. Game Sahnesine Ekle (01_Game açıkken)")]
    public static void AddToGameScene()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("PreLevel Retry",
                "Prefab yok. Önce MainMenu açıkken 1. adımı çalıştır.", "Tamam");
            return;
        }

        // Zaten eklenmişse tekrar ekleme.
        var existing = Object.FindFirstObjectByType<PreLevelSpecialPopupController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            EditorUtility.DisplayDialog("PreLevel Retry",
                "Bu sahnede zaten bir PreLevelSpecialPopup var. Yeni eklenmedi.", "Tamam");
            return;
        }

        // Levelend popup'ın canvas'ını hedefle → popup onun üstünde (aynı canvas, en öne) açılır.
        var levelEnd = Object.FindFirstObjectByType<LevelEndSimplePopupController>(FindObjectsInactive.Include);
        Canvas canvas = levelEnd != null ? levelEnd.GetComponentInParent<Canvas>() : null;
        if (canvas == null)
            canvas = Object.FindFirstObjectByType<Canvas>();

        if (canvas == null)
        {
            EditorUtility.DisplayDialog("PreLevel Retry",
                "Sahnede Canvas bulunamadı. 01_Game sahnesini aç ve tekrar dene.", "Tamam");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, canvas.transform);
        instance.name = "PreLevelSpecialPopup";
        instance.SetActive(true);

        if (instance.transform is RectTransform rt)
        {
            MockupUI.Stretch(rt);
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling();   // levelend'in üstünde render olsun
        }

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        EditorUtility.DisplayDialog("PreLevel Retry",
            "Pre-level popup 01_Game sahnesine eklendi (levelend canvas'ı, tam ekran, en önde).\n" +
            "Sahneyi kaydet (Cmd+S).\n\nTest: level kaybet → fail → cancel → cancel → popup anında açılır.",
            "Tamam");

        Selection.activeGameObject = instance;
    }
}
