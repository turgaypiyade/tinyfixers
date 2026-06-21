using System.Collections;
using UnityEngine;
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
        SceneManager.LoadScene(nextSceneName);
    }
}
