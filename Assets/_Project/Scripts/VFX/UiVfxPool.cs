using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class UiVfxPool
{
    private const int DefaultMaxPerKey = 96;
    private static readonly Dictionary<string, Stack<GameObject>> pools = new();
    private static Transform poolRoot;

    public static GameObject RentRect(string key, Transform parent, string objectName)
    {
        var go = Rent(key);
        if (go == null)
            go = new GameObject(objectName, typeof(RectTransform));

        Prepare(go, parent, objectName);
        return go;
    }

    public static Image RentImage(string key, Transform parent, string objectName)
    {
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

        if (!pools.TryGetValue(key, out var stack))
            pools[key] = stack = new Stack<GameObject>();

        if (stack.Count >= Mathf.Max(1, maxPerKey))
        {
            Object.Destroy(go);
            return;
        }

        go.SetActive(false);
        go.transform.SetParent(GetPoolRoot(), false);
        stack.Push(go);
    }

    private static GameObject Rent(string key)
    {
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
