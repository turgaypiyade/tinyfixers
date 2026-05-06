using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private TMP_Text target;
    [SerializeField] private string key;

    private void Reset()
    {
        target = GetComponent<TMP_Text>();
    }

    private void Awake()
    {
        if (target == null)
            target = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        GameLocalization.OnLanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        GameLocalization.OnLanguageChanged -= Refresh;
    }

    public void SetKey(string localizationKey)
    {
        key = localizationKey;
        Refresh();
    }

    public void Refresh()
    {
        if (target == null)
            return;

        target.text = GameLocalization.Get(key);
    }
}
