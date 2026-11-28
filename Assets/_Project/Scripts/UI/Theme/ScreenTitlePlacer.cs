using UnityEngine;

/// <summary>
/// Ekran başlığını (Journey / Ranks / Team) ekran kökünün üstünden sabit mesafeye oturtur;
/// etkinleşince ve ekran/ebeveyn boyutu değişince tekrar uygular (ilk açılışta rect henüz 0 olabilir).
/// ScreenTitleStyle.ApplyToScreen ekler.
/// </summary>
[DisallowMultipleComponent]
public sealed class ScreenTitlePlacer : MonoBehaviour
{
    private RectTransform root;
    private Vector2 lastRootSize;
    private float extraDown;

    public void Init(RectTransform screenRoot, float extraDown = 0f)
    {
        root = screenRoot;
        this.extraDown = extraDown;
        lastRootSize = Vector2.zero;
        Place();
    }

    private void OnEnable() => Place();

    // Kök boyutu değişince (ilk layout, çözünürlük/safe-area değişimi) yeniden oturt.
    private void LateUpdate()
    {
        if (root == null) return;
        var size = root.rect.size;
        if (size != lastRootSize) Place();
    }

    private void Place()
    {
        if (root == null) return;
        lastRootSize = root.rect.size;
        ScreenTitleStyle.PlaceAtCommonPosition((RectTransform)transform, root, extraDown);
    }
}
