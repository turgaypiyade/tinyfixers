using UnityEngine;

[CreateAssetMenu(fileName = "BorderSpriteSet", menuName = "TinyFixers/Grid/Border Sprite Set")]
public class BorderSpriteSet : ScriptableObject
{
    [Header("Identity")]
    public BorderColorId colorId = BorderColorId.Orange;

    [Header("Straight Edge Prefabs")]
    public GameObject edgeTopPrefab;
    public GameObject edgeBottomPrefab;
    public GameObject edgeLeftPrefab;
    public GameObject edgeRightPrefab;

    [Header("Outer Corner Prefabs")]
    public GameObject outerLTPrefab;
    public GameObject outerRTPrefab;
    public GameObject outerLBPrefab;
    public GameObject outerRBPrefab;

    [Header("Inner Corner Prefabs")]
    public GameObject innerLTPrefab;
    public GameObject innerRTPrefab;
    public GameObject innerLBPrefab;
    public GameObject innerRBPrefab;

    public bool IsComplete()
    {
        return edgeTopPrefab != null && edgeBottomPrefab != null &&
               edgeLeftPrefab != null && edgeRightPrefab != null &&
               outerLTPrefab != null && outerRTPrefab != null &&
               outerLBPrefab != null && outerRBPrefab != null &&
               innerLTPrefab != null && innerRTPrefab != null &&
               innerLBPrefab != null && innerRBPrefab != null;
    }

    public void ApplyTo(DynamicBoardBorder border)
    {
        if (border == null)
            return;

        border.use3DBorderPrefabs = true;

        border.edgeTopPrefab = edgeTopPrefab;
        border.edgeBottomPrefab = edgeBottomPrefab;
        border.edgeLeftPrefab = edgeLeftPrefab;
        border.edgeRightPrefab = edgeRightPrefab;

        border.outerLTPrefab = outerLTPrefab;
        border.outerRTPrefab = outerRTPrefab;
        border.outerLBPrefab = outerLBPrefab;
        border.outerRBPrefab = outerRBPrefab;

        border.innerLTPrefab = innerLTPrefab;
        border.innerRTPrefab = innerRTPrefab;
        border.innerLBPrefab = innerLBPrefab;
        border.innerRBPrefab = innerRBPrefab;
    }
}
