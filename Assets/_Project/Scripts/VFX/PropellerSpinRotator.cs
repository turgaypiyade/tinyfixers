using UnityEngine;

public sealed class PropellerSpinRotator : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 720f;

    private RectTransform rt;

    private void Awake() => rt = GetComponent<RectTransform>();

    private void Update()
    {
        rt.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime);
    }

    public void SetSpeed(float dps) => degreesPerSecond = dps;
}
