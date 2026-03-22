using UnityEngine;
using System.Collections;

[ExecuteAlways]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rt;
    Rect lastSafe;

    void OnEnable()
    {
        rt = GetComponent<RectTransform>();

        if (Application.isPlaying)
        { 
            StartCoroutine(ApplyDelayed());
        } else
        {
            Apply();
        }
    }

    void Update()
    {
        if (rt == null) return;
        if (Screen.safeArea != lastSafe) Apply();
            
    }
// Bu fonksiyon render bitene kadar bekler
    IEnumerator ApplyDelayed()
    {
        yield return new WaitForEndOfFrame();
        Apply();
    }

    void Apply()
    {
        if (rt == null) return;

        if(Screen.width == 0 || Screen.height == 0) return;

        lastSafe = Screen.safeArea;

        Vector2 anchorMin = lastSafe.position;
        Vector2 anchorMax = lastSafe.position + lastSafe.size;

        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        if (rt.anchorMin != anchorMin || rt.anchorMax != anchorMax)
        {
 
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        }
    }
}
