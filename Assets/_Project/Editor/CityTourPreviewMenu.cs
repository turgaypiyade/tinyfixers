using UnityEditor;
using UnityEngine;

// Uses the real city view without changing wonder completion, rewards or saved settings.
public static class CityTourPreviewMenu
{
    [MenuItem("TinyFixers/Preview/World Tour/01 Pisa/Tamamlanmis (Play Mode)")]
    private static void Preview1Tamamlanmis() => Show("mis1", true);

    [MenuItem("TinyFixers/Preview/World Tour/01 Pisa/Insaat (Play Mode)")]
    private static void Preview1Insaat() => Show("mis1", false);

    [MenuItem("TinyFixers/Preview/World Tour/02 Golden Gate/Tamamlanmis (Play Mode)")]
    private static void Preview2Tamamlanmis() => Show("mis2", true);

    [MenuItem("TinyFixers/Preview/World Tour/02 Golden Gate/Insaat (Play Mode)")]
    private static void Preview2Insaat() => Show("mis2", false);

    [MenuItem("TinyFixers/Preview/World Tour/03 Eyfel/Tamamlanmis (Play Mode)")]
    private static void Preview3Tamamlanmis() => Show("mis3", true);

    [MenuItem("TinyFixers/Preview/World Tour/03 Eyfel/Insaat (Play Mode)")]
    private static void Preview3Insaat() => Show("mis3", false);

    [MenuItem("TinyFixers/Preview/World Tour/04 Piramit/Tamamlanmis (Play Mode)")]
    private static void Preview4Tamamlanmis() => Show("mis4", true);

    [MenuItem("TinyFixers/Preview/World Tour/04 Piramit/Insaat (Play Mode)")]
    private static void Preview4Insaat() => Show("mis4", false);

    [MenuItem("TinyFixers/Preview/World Tour/05 Cin Seddi/Tamamlanmis (Play Mode)")]
    private static void Preview5Tamamlanmis() => Show("mis5", true);

    [MenuItem("TinyFixers/Preview/World Tour/05 Cin Seddi/Insaat (Play Mode)")]
    private static void Preview5Insaat() => Show("mis5", false);

    [MenuItem("TinyFixers/Preview/World Tour/06 Buyuk Kanyon/Tamamlanmis (Play Mode)")]
    private static void Preview6Tamamlanmis() => Show("mis6", true);

    [MenuItem("TinyFixers/Preview/World Tour/06 Buyuk Kanyon/Insaat (Play Mode)")]
    private static void Preview6Insaat() => Show("mis6", false);

    [MenuItem("TinyFixers/Preview/World Tour/07 Ayasofya/Tamamlanmis (Play Mode)")]
    private static void Preview7Tamamlanmis() => Show("mis7", true);

    [MenuItem("TinyFixers/Preview/World Tour/07 Ayasofya/Insaat (Play Mode)")]
    private static void Preview7Insaat() => Show("mis7", false);

    [MenuItem("TinyFixers/Preview/World Tour/08 Rodos/Tamamlanmis (Play Mode)")]
    private static void Preview8Tamamlanmis() => Show("mis8", true);

    [MenuItem("TinyFixers/Preview/World Tour/08 Rodos/Insaat (Play Mode)")]
    private static void Preview8Insaat() => Show("mis8", false);

    [MenuItem("TinyFixers/Preview/World Tour/09 Big Ben/Tamamlanmis (Play Mode)")]
    private static void Preview9Tamamlanmis() => Show("mis9", true);

    [MenuItem("TinyFixers/Preview/World Tour/09 Big Ben/Insaat (Play Mode)")]
    private static void Preview9Insaat() => Show("mis9", false);

    [MenuItem("TinyFixers/Preview/World Tour/10 Sidney/Tamamlanmis (Play Mode)")]
    private static void Preview10Tamamlanmis() => Show("mis10", true);

    [MenuItem("TinyFixers/Preview/World Tour/10 Sidney/Insaat (Play Mode)")]
    private static void Preview10Insaat() => Show("mis10", false);

    private static void Show(string wonderId, bool completed)
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Şehir Gezisi Önizleme",
                "MainMenu sahnesinde Play Mode'a gir, sonra bu menüyü seç. Kayıtlı görev ilerlemesi değiştirilmez.", "Tamam");
            return;
        }
        var catalog = AssetDatabase.LoadAssetAtPath<WonderCatalog>("Assets/_Project/Settings/Wonders/WonderCatalog.asset");
        if (catalog != null)
            for (int i = 0; i < catalog.Count; i++)
            {
                var wonder = catalog.Get(i);
                if (wonder == null || wonder.wonderId != wonderId) continue;
                if (!CityTourView.HasArt(wonder)) break;
                CityTourView.ShowPreview(catalog, i, completed);
                return;
            }
        EditorUtility.DisplayDialog("Şehir Gezisi Önizleme", "Harika veya şehir görseli bulunamadı: " + wonderId, "Tamam");
    }
}
