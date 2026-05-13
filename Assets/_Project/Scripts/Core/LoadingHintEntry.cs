using System;
using UnityEngine;

[Serializable]
public class LoadingHintEntry
{
    public Sprite image;
    public string titleLocalizationKey;
    public string descriptionLocalizationKey;
    public string loadingLocalizationKey = "loading_hint_loading";
}
