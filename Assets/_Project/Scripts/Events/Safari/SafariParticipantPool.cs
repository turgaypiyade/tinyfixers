using System.Collections.Generic;
using UnityEngine;

/// <summary>Safari yarışındaki tek bir katılımcı (oyuncu veya bot).</summary>
public struct SafariParticipant
{
    public string id;
    public string displayName;
    public Sprite avatar;
    public int    level;
    public bool   isPlayer;
}

/// <summary>
/// Safari yarışı için katılımcı listesi üretir. Oyuncu HER ZAMAN listenin başında (en önde çizilir).
/// Botlar oyuncunun level'ına yakın seçilir (spec: "yakın leveldaki kullanıcılar"); oyun büyüyünce
/// bu havuz gerçek kullanıcılarla değiştirilir — arayüz aynı kalır.
///
/// Avatarlar <see cref="PlayerAvatarProvider"/> üzerinden gelir: oyuncu <c>Current</c>, botlar
/// <c>PickForSeed(id)</c> ile deterministik. İsimler <see cref="BotNameGenerator"/> ile üretilir.
/// </summary>
public static class SafariParticipantPool
{
    /// <summary>
    /// <paramref name="total"/> katılımcı üretir; ilki oyuncu, kalanı oyuncunun level'ına yakın botlar.
    /// </summary>
    public static List<SafariParticipant> Build(int total, int playerLevel, int seed = 0)
    {
        total = Mathf.Max(1, total);
        var result = new List<SafariParticipant>(total);

        // Oyuncu — her zaman ilk (en önde).
        result.Add(new SafariParticipant
        {
            id          = "player",
            displayName = PlayerProfile.PlayerName,
            avatar      = PlayerAvatarProvider.Current,
            level       = playerLevel,
            isPlayer    = true
        });

        var lang = Application.systemLanguage == SystemLanguage.Turkish
            ? BotNameLanguage.Turkish
            : BotNameLanguage.English;

        // Deterministik dağılım için ayrı bir RNG (global Random state'ini kirletme).
        var rng = new System.Random(seed == 0 ? playerLevel * 7919 + 13 : seed);

        for (int i = 1; i < total; i++)
        {
            string id = $"safari_bot_{seed}_{i}";
            // Oyuncuya yakın level: ±3 bandında, en az 1.
            int offset = rng.Next(-3, 4);
            int level  = Mathf.Max(1, playerLevel + offset);

            result.Add(new SafariParticipant
            {
                id          = id,
                displayName = BotNameGenerator.Generate(lang),
                avatar      = PlayerAvatarProvider.PickForSeed(id),
                level       = level,
                isPlayer    = false
            });
        }

        // Botları level yakınlığına göre sırala (oyuncu ilk sırada kalır).
        result.Sort((a, b) =>
        {
            if (a.isPlayer) return -1;
            if (b.isPlayer) return 1;
            return Mathf.Abs(a.level - playerLevel).CompareTo(Mathf.Abs(b.level - playerLevel));
        });

        return result;
    }
}
