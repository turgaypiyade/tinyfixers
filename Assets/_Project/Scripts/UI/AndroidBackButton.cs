using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Android geri tuşu: ekranda en üstte duran popup'ın kapatma butonuna (BtnClose / CloseButton / BtnSClose …)
/// basılmış gibi davranır. Kapatılacak popup yoksa hiçbir şey yapmaz (oyun kapanmaz, seviyeden çıkılmaz).
/// Tek, kalıcı bileşen; tarama yalnız tuşa basıldığında yapılır (her kare iş yok).
/// Input System'de Android geri tuşu Keyboard.escapeKey olarak gelir (editörde Esc ile denenir).
/// </summary>
public sealed class AndroidBackButton : MonoBehaviour
{
    private static readonly List<Transform> PathA = new();
    private static readonly List<Transform> PathB = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (FindFirstObjectByType<AndroidBackButton>() != null) return;
        var go = new GameObject("AndroidBackButton");
        DontDestroyOnLoad(go);
        go.AddComponent<AndroidBackButton>();
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;
        var close = FindTopmostCloseButton();
        if (close != null) close.onClick.Invoke();
    }

    private static Button FindTopmostCloseButton()
    {
        Button best = null;
        foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            if (!IsCloseButton(button)) continue;
            if (best == null || DrawsAbove(button.transform, best.transform)) best = button;
        }
        return best;
    }

    private static bool IsCloseButton(Button button)
    {
        if (button == null || !button.isActiveAndEnabled || !button.IsInteractable()) return false;
        if (button.name.IndexOf("close", System.StringComparison.OrdinalIgnoreCase) < 0) return false;
        // Görünmez (kapanmış ama yok edilmemiş) popup'lar: CanvasGroup alpha 0.
        for (var t = button.transform; t != null; t = t.parent)
            if (t.TryGetComponent(out CanvasGroup group) && (group.alpha < 0.05f || !group.blocksRaycasts)) return false;
        return true;
    }

    // a, b'nin üstünde mi çizilir: önce kök Canvas (Overlay > Camera, sonra sortingOrder), sonra hiyerarşi sırası.
    private static bool DrawsAbove(Transform a, Transform b)
    {
        var ca = RootCanvas(a);
        var cb = RootCanvas(b);
        if (ca != cb && ca != null && cb != null)
        {
            bool oa = ca.renderMode == RenderMode.ScreenSpaceOverlay;
            bool ob = cb.renderMode == RenderMode.ScreenSpaceOverlay;
            if (oa != ob) return oa;
            if (ca.sortingOrder != cb.sortingOrder) return ca.sortingOrder > cb.sortingOrder;
        }
        // Aynı kökte: hiyerarşide sonra gelen üstte çizilir.
        FillPath(a, PathA);
        FillPath(b, PathB);
        int n = Mathf.Min(PathA.Count, PathB.Count);
        for (int i = 0; i < n; i++)
        {
            if (PathA[i] == PathB[i]) continue;
            return PathA[i].GetSiblingIndex() > PathB[i].GetSiblingIndex();
        }
        return PathA.Count > PathB.Count;
    }

    private static Canvas RootCanvas(Transform t)
    {
        var c = t.GetComponentInParent<Canvas>();
        return c != null ? c.rootCanvas : null;
    }

    private static void FillPath(Transform t, List<Transform> path)
    {
        path.Clear();
        for (; t != null; t = t.parent) path.Add(t);
        path.Reverse();
    }
}
