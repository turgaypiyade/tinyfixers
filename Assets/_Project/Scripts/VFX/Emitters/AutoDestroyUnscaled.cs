using UnityEngine;

public class AutoDestroyUnscaled : MonoBehaviour
{
    public float lifetime = 0.2f;

    private float t = 0f;

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (t >= lifetime)
            Destroy(gameObject);
    }
}