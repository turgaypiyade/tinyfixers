using UnityEngine;

public class LightningTestSpawner : MonoBehaviour
{
    public LightningBeam lightningPrefab;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Vector3 start = new Vector3(-3f, 0f, 0f);
            Vector3 end   = new Vector3( 3f, 2f, 0f);

            var beam = Instantiate(lightningPrefab);
            beam.Init(start, end);
        }
    }
}
