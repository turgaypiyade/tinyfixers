using UnityEditor;

// Editörde herkese açık Firestore yazılarını (liderlik, oyuncu dizini) aç/kapa. Varsayılan: KAPALI (yazılmaz).
public static class EditorBackendGuardMenu
{
    private const string MenuPath = "TinyFixers/Debug/Editörde Liderliğe Yaz";

    [MenuItem(MenuPath)]
    private static void Toggle() => EditorBackendGuard.AllowPublicWrites = !EditorBackendGuard.AllowPublicWrites;

    [MenuItem(MenuPath, true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, EditorBackendGuard.AllowPublicWrites);
        return true;
    }
}
