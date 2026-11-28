using System;

/// <summary>
/// Pençe Makinesi saf kuralları (UI ve durumdan bağımsız, test edilebilir). Fizik yok: sonuç İNDİR anında
/// pençenin 2.5D konumundan (yatay u, derinlik w; ikisi 0..1) belirlenir, sunum bu sonucu oynatır.
/// 2026-11-28 (kullanıcı): beceri tabanlı — gizli garanti yok. Oyuncak merkezine elips mesafe:
/// çekirdek bölge kesin yakalar, kenar bölgesi küçük şansla tutar (çoğunlukla kayar), dışı boş.
/// "Güçlü pençe" (Pençe Gücü dolu) kenar bölgesini de kesin yakalar; nişan yine şarttır.
/// </summary>
public static class ClawMachineRules
{
    public enum Outcome { Empty, Slip, Catch }

    public const int Rows = 3;

    /// Oyuncak yuvasının konumu: u (yatay) ve w (derinlik; 0 = ön, 1 = arka). Sıra = slot % 3 (0 ön, 1 orta,
    /// 2 arka); sıralar yatayda kaydırılır (iç içe) — her bölgede yakalanacak bir şey olur.
    public static void Slot(int slot, int count, out float u, out float w)
    {
        int row = Row(slot);
        int cols = Math.Max(1, (count + Rows - 1) / Rows);
        int col = slot / Rows;
        const float margin = 0.1f;
        float shift = row == 1 ? 0.5f : row == 2 ? 0.25f : 0f;
        u = cols <= 1 ? 0.5f : margin + (1f - 2f * margin) * (col + shift) / (cols - 1 + 0.5f);
        w = RowDepth(row);
    }
    public static int Row(int slot) => slot % Rows;
    public static float RowDepth(int row) => row == 0 ? 0.12f : row == 1 ? 0.5f : 0.88f;

    /// İndirilen noktaya en yakın yuva ve isabet (1 = tam merkez, 0 = yarıçap sınırı/dışı). Elips mesafe:
    /// halfU yatay, halfW derinlik yarıçapı. Eşitlikte öndeki (pençeye ilk değen) kazanır.
    public static int Nearest(float u, float w, int count, float halfU, float halfW, out float accuracy)
    {
        int best = -1; float bestD = float.MaxValue;
        float hu = Math.Max(0.001f, halfU), hw = Math.Max(0.001f, halfW);
        for (int i = 0; i < count; i++)
        {
            Slot(i, count, out float su, out float sw);
            float du = (u - su) / hu, dw = (w - sw) / hw;
            float d = (float)Math.Sqrt(du * du + dw * dw);
            if (d < bestD - 1e-5f || (Math.Abs(d - bestD) <= 1e-5f && best >= 0 && Row(i) < Row(best)))
            { bestD = d; best = i; }
        }
        accuracy = best < 0 ? 0f : Math.Max(0f, 1f - bestD);
        return best;
    }

    /// İsabetten sonuç. accuracy ≥ coreAccuracy → kesin yakalar; slipAccuracy..core arası kenar: tutma şansı
    /// 0 → edgeGrip doğrusal (tutmazsa kayar); slipAccuracy altı → boş. strong: kenar da kesin yakalar.
    public static Outcome Resolve(float accuracy, float coreAccuracy, float slipAccuracy, float edgeGrip, bool strong, double roll)
    {
        if (accuracy <= 0f || accuracy < slipAccuracy) return Outcome.Empty;
        if (accuracy >= coreAccuracy || strong) return Outcome.Catch;
        float t = (accuracy - slipAccuracy) / Math.Max(0.001f, coreAccuracy - slipAccuracy);
        return roll < t * edgeGrip ? Outcome.Catch : Outcome.Slip;
    }

    /// Basit deterministik RNG (durumda saklanır; kapat-aç ile tekrar denenip sonuç değiştirilemez).
    public static int Next(ref int state)
    {
        unchecked
        {
            uint x = state == 0 ? 0x9E3779B9u : (uint)state;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            state = (int)x;
            return (int)(x & 0x7FFFFFFF);
        }
    }
    public static double NextDouble(ref int state) => Next(ref state) / 2147483648.0;

    public static int Weighted(ref int state, int[] weights)
    {
        int total = 0;
        foreach (int w in weights) total += Math.Max(0, w);
        if (total <= 0) return -1;
        int pick = Next(ref state) % total;
        for (int i = 0; i < weights.Length; i++)
        {
            pick -= Math.Max(0, weights[i]);
            if (pick < 0) return i;
        }
        return weights.Length - 1;
    }
}
