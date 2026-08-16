using System.Collections.Generic;
using UnityEngine;

// Faz 7 (Docs/Match3_MasterRoadmap.md A7 · UnifiedSpecialFlow_Plan.md "Faz 7 — Per-column async").
//
// Bu dosya, gravity/refill simülasyonunu SÜTUN-BAZLI bir sürücüye taşır. Milestone 7A =
// DAVRANIŞ-BİREBİR: çıktı (FallAction / final board) whole-board yolla bit-for-bit aynı olmalı.
// Tek yapısal fark:
//   1) vertical adımı yalnız "dirty" (bu iterasyonda değişebilecek) sütunlar için koşar — settled
//      bir sütunun vertical'ı zaten no-op döndüğü için ATLAMAK çıktıyı değiştirmez, sadece CPU'yu
//      aktif sütunlara yoğunlaştırır;
//   2) hangi sütunların hareket ettiği (ColumnBusy) izlenir → Faz 8'de "düşerken hamle" input
//      gating'inin okuyacağı zemin (7A'da yalnız ÜRETİLİR, tüketen yok).
//
// Diagonal, sütunları birbirine bağlar (kolon x, x±1'den taş çeker). Bu yüzden diagonal adımı
// burada da whole-board ve ungated koşar (orijinalle birebir); yalnız etkilediği sütunları
// "dirty" işaretler ki bir sonraki iterasyonda onların vertical'ı yeniden değerlendirilsin.
//
// 7B (sonraki milestone) bu sürücüyü async'e çevirecek: her sütun kendi zaman çizgisinde otursun.
// 7A kasıtla senkron kalır → önce ayrıştırmanın DOĞRU olduğu (feel değişmeden) kanıtlanır.
public partial class CascadeLogic
{
    // Sürücü için yeniden kullanılan tamponlar (per-call GC üretme; whole-board yol da lokal alloc'lar
    // kullanıyor, bunları field'da tutmak flag açıkken perf'i kötüleştirmez).
    private bool[] _pcDirty;
    private readonly HashSet<int> _pcNextDirty = new HashSet<int>();
    private readonly HashSet<int> _pcDiagAffected = new HashSet<int>();
    private readonly HashSet<int> _pcActiveColumns = new HashSet<int>();

    // Son RunPerColumnSimulation çağrısında hareket eden sütunlar (ColumnBusy kaynağı).
    internal IReadOnlyCollection<int> LastActiveColumns => _pcActiveColumns;

    // Whole-board simülasyon while-loop'unun (CalculateCascades) sütun-bazlı, davranış-birebir eşi.
    private void RunPerColumnSimulation(
        VirtualTile[,] virtualBoard,
        HashSet<Vector2Int> verticalOnlyGaps,
        ref bool spawnedMovableThisPass,
        Dictionary<ObstacleId, int> spawnedMovableCounts)
    {
        int w = board.Width;
        if (_pcDirty == null || _pcDirty.Length != w)
            _pcDirty = new bool[w];

        // İlk iterasyonda her sütun işlenir (whole-board yolun ilk iterasyonuyla birebir): vertical
        // kompaktlama + spawn herkes için bir kez koşmalı.
        for (int x = 0; x < w; x++) _pcDirty[x] = true;
        _pcActiveColumns.Clear();

        const int MAX_ITERATIONS = 32;
        int iter = 0;
        bool changed = true;

        while (changed && iter < MAX_ITERATIONS)
        {
            changed = false;
            iter++;
            _pcNextDirty.Clear();

            // Step 1: Vertical Collapse & Spawn — yalnız dirty sütunlar.
            // (Settled sütunun ProcessVerticalGravityAndSpawn'ı false döner → atlamak no-op'tur.)
            for (int x = 0; x < w; x++)
            {
                if (!_pcDirty[x]) continue;
                if (ProcessVerticalGravityAndSpawn(virtualBoard, x, ref spawnedMovableThisPass, spawnedMovableCounts))
                {
                    changed = true;
                    _pcNextDirty.Add(x);
                    _pcActiveColumns.Add(x);
                }
            }

            // Step 2: Diagonal Slide — whole-board, ungated (orijinalle birebir; önce normal taşlar,
            // sonra son çare special). Etkilenen sütunlar bir sonraki iterasyon için dirty.
            bool slided = false;
            if (!SuppressDiagonalSlides)
            {
                _pcDiagAffected.Clear();
                slided = DoDiagonalSlidePass(virtualBoard, verticalOnlyGaps, skipSpecials: true, _pcDiagAffected);
                if (!slided)
                    slided = DoDiagonalSlidePass(virtualBoard, verticalOnlyGaps, skipSpecials: false, _pcDiagAffected);

                if (slided)
                {
                    foreach (var c in _pcDiagAffected)
                    {
                        _pcNextDirty.Add(c);
                        _pcActiveColumns.Add(c);
                    }
                }
            }
            changed |= slided;

            PruneVerticalOnlyGaps(virtualBoard, verticalOnlyGaps);

            // Bir sonraki iterasyonun dirty seti = bu iterasyonda vertical'ı hareket eden + diagonal'in
            // dokunduğu sütunlar. Bir sütun ancak bu iki yolla değişir (vertical yalnız kendi içinde
            // kompaktlar, diagonal from/to sütunlarını raporlar) → gating tam, çıktı değişmez.
            for (int x = 0; x < w; x++) _pcDirty[x] = false;
            foreach (var c in _pcNextDirty)
                if (c >= 0 && c < w) _pcDirty[c] = true;
        }
    }
}
