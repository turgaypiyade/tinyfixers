/// <summary>
/// Editörde test oynarken HERKESE AÇIK Firestore tablolarına (liderlik, oyuncu dizini, takım profil kartı)
/// yazılmaz — gerçek oyuncular test hesabını görmesin, retention ölçümü kirlenmesin. Bulut kaydı (kişiye özel)
/// etkilenmez. Liderlik testi için: TinyFixers ▸ Debug ▸ Editörde Liderliğe Yaz (aç/kapa).
/// Cihaz build'lerinde her zaman serbest.
/// </summary>
public static class EditorBackendGuard
{
#if UNITY_EDITOR
    private const string AllowKey = "TF_AllowPublicWritesInEditor";
    public static bool BlockPublicWrites => !UnityEditor.EditorPrefs.GetBool(AllowKey, false);
    public static bool AllowPublicWrites
    {
        get => UnityEditor.EditorPrefs.GetBool(AllowKey, false);
        set => UnityEditor.EditorPrefs.SetBool(AllowKey, value);
    }
#else
    public static bool BlockPublicWrites => false;
#endif
}
