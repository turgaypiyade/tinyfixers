using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI VFX objeleri için TEK ortak depo (anahtar başına yığın). Kısa ömürlü efekt parçaları her seferinde
/// new GameObject/Destroy yerine buradan alınıp geri verilir (taş kırma, PatchBot kıvılcımı, roket izi,
/// Override lazer çarpması, PulseCore fitil kıvılcımı). Sönme sürücüsü gereken akışlar: UiFxPool.
/// </summary>
public static class UiVfxPool
{
    private const int DefaultMaxPerKey = 96;
    private static readonly Dictionary<string, Stack<GameObject>> pools = new();
    private static Transform poolRoot;
    private static bool stopping;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        // Also reset static state when Enter Play Mode has domain reload disabled.
        stopping = false;
        pools.Clear();
        poolRoot = null;
        Application.quitting -= StopSession;
        Application.quitting += StopSession;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= PlayModeChanged;
        UnityEditor.EditorApplication.playModeStateChanged += PlayModeChanged;
#endif
    }

    private static void StopSession()
    {
        stopping = true;
        pools.Clear();
    }

#if UNITY_EDITOR
    private static void PlayModeChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode) StopSession();
    }
#endif

    public static GameObject RentRect(string key, Transform parent, string objectName)
    {
        if (stopping || !Application.isPlaying) return null;
        var go = Rent(key);
        if (go == null)
            go = new GameObject(objectName, typeof(RectTransform));

        Prepare(go, parent, objectName);
        return go;
    }

    /// <summary>Havuzda obje varsa hazırlayıp verir, yoksa null (çağıran prefab'tan kendisi üretir).</summary>
    public static GameObject TryRent(string key, Transform parent, string objectName)
    {
        var go = Rent(key);
        if (go != null)
            Prepare(go, parent, objectName);
        return go;
    }

    public static Image RentImage(string key, Transform parent, string objectName)
    {
        if (stopping || !Application.isPlaying) return null;
        var go = Rent(key);
        if (go == null)
            go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));

        Prepare(go, parent, objectName);

        var image = go.GetComponent<Image>();
        if (image == null)
            image = go.AddComponent<Image>();

        image.enabled = true;
        image.sprite = null;
        image.material = null;
        image.color = Color.white;
        image.raycastTarget = false;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
        return image;
    }

    public static void Return(string key, GameObject go, int maxPerKey = DefaultMaxPerKey)
    {
        if (go == null)
            return;

        // Quit/Play-stop objects stay in their owning hierarchy for Unity to destroy.
        // In particular, OnDisable/OnDestroy must never create a new persistent root.
        if (stopping || !Application.isPlaying) return;
        if (poolRoot == null)
        {
            Object.Destroy(go);
            return;
        }

        if (!pools.TryGetValue(key, out var stack))
            pools[key] = stack = new Stack<GameObject>();

        if (stack.Count >= Mathf.Max(1, maxPerKey))
        {
            Object.Destroy(go);
            return;
        }

        go.SetActive(false);
        go.transform.SetParent(poolRoot, false);
        stack.Push(go);
    }

    private static GameObject Rent(string key)
    {
        if (stopping || !Application.isPlaying) return null;
        // Create the root only during acquisition, never while returning a dying scene's objects.
        GetPoolRoot();
        if (pools.TryGetValue(key, out var stack))
        {
            while (stack.Count > 0)
            {
                var go = stack.Pop();
                if (go != null)
                    return go;
            }
        }

        return null;
    }

    private static void Prepare(GameObject go, Transform parent, string objectName)
    {
        go.name = objectName;
        go.transform.SetParent(parent, false);
        go.SetActive(true);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        if (go.transform is RectTransform rt)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = Vector2.zero;
        }
    }

    private static Transform GetPoolRoot()
    {
        if (poolRoot != null)
            return poolRoot;

        var root = new GameObject("[UiVfxPool]");
        Object.DontDestroyOnLoad(root);
        poolRoot = root.transform;
        return poolRoot;
    }
}
