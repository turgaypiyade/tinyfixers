/// <summary>
/// Oyuncuya gösterilen zorluk etiketi (kullanıcı 2026-11-14): seviye tasarımındaki niyet — 40. seviyeden sonra
/// 5 ile biten seviyeler ZOR, 9 ile bitenler ÇOK ZOR. Seviye verisinde zorluk alanı yok; kural seviye numarasından.
/// Ana menü etiketi ve seviye sonu altını aynı kuralı kullanır.
/// </summary>
public enum LevelDifficulty { Normal, Hard, VeryHard }

public static class LevelDifficultyTag
{
    public const int FirstTaggedLevel = 40;

    public static LevelDifficulty For(int level)
    {
        if (level < FirstTaggedLevel) return LevelDifficulty.Normal;
        int last = level % 10;
        return last == 9 ? LevelDifficulty.VeryHard : last == 5 ? LevelDifficulty.Hard : LevelDifficulty.Normal;
    }
}
