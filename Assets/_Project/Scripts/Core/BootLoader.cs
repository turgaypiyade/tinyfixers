using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public class BootLoader : MonoBehaviour
{
    [SerializeField] float waitSeconds = 2f;
    [SerializeField] string nextSceneName = "01_Game";

    void Start()
    {
        PlayerStats.EnsureInitialized();   // oyuna ilk giriş tarihini bir kez kaydet
        StartCoroutine(LoadNext());
    }

    IEnumerator LoadNext()
    {
        yield return new WaitForSeconds(waitSeconds);

        // Splash sahnesindeki post-FX Volume'ları sahne yüklemeden ÖNCE devre dışı bırak:
        // LoadScene(single) Volume'u yok ederken VolumeManager hâlâ ona erişip
        // "Volume has been destroyed but you are still trying to access it" hatası veriyordu.
        // enabled=false, Volume'u OnDisable'da manager'dan temiz çıkarır → hata biter.
        foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None))
            if (v != null) v.enabled = false;

        SceneManager.LoadScene(nextSceneName);
    }
}
