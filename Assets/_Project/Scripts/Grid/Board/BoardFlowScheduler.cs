using System;

/// <summary>
/// Board akışının TEK OTORİTESİ (Docs/UnifiedSpecialFlow_Plan.md Faz 1).
///
/// PROBLEM: "taş ne zaman düşer / board ne zaman oturdu" sorusunun 7 ayrı sahibi vardı
/// (ActionSequencer.IsPlaying, BlockingBackgroundJobs, DetachedActionsInFlight, IsBusy,
/// IsSpecialActivationPhase, ActiveBackgroundJobs, her davranışın kendi while/safety döngüsü).
/// Her special/combo/clear bunları farklı karıştırdığı için lost-wakeup (edge-triggered resolve),
/// self-count deadlock (kendi job'unu bekleyen döngü), stuck-flag ve tek-frame spike'lar doğuyordu.
///
/// FAZ 1 (bu sınıf) = **DAVRANIŞ KORUYAN ADAPTER**: sinyalleri tek API arkasına alır, semantiği
/// DEĞİŞTİRMEZ. Böylece:
///   - Yeni kod tek yere sorar (`IsSettling`, `CanStartResolveStep`, `IsSpecialVisualInFlight`).
///   - Activity kaydı (Begin/Count) hazır durur; sonraki fazlarda special/combo/clear tek tek buraya
///     taşınır (o zaman eski sinyaller ölür).
///   - `Pump()` sürekli (edge değil) çalışır → lost-wakeup sınıfı yapısal olarak kapalı.
///
/// KURAL (sonraki fazlar için): kimse GLOBAL sayaç üzerinde `while(count>0)` beklememeli. Kendi
/// başlattığı işi beklemek isteyen, aldığı handle'ı (veya scope'unu) bekler — self-count imkânsız.
/// </summary>
public sealed class BoardFlowScheduler
{
    private readonly BoardController board;

    public BoardFlowScheduler(BoardController board)
    {
        this.board = board;
    }

    /// <summary>
    /// Akışı ilgilendiren iş tipleri. Faz 1'de kayıt defteri BOŞ (kimse taşınmadı) → davranış birebir
    /// eski hâli. Fazlar ilerledikçe special/combo/clear bu kayda geçer.
    /// </summary>
    public enum ActivityKind
    {
        Fall = 0,            // taş düşüşü (gravity/cascade)
        Clear = 1,           // eşleşme/patlama temizliği
        SpecialSweep = 2,    // line/pulse/override süpürme (taş kırar → akışı etkiler)
        ComboStep = 3,       // combo adımı
        ObstacleSpread = 4,  // oil/barrel yayılımı
        Flight = 5,          // orb/patchbot/key uçuşu (akışı bloklamaz, level-end bekler)
        Presentation = 6     // saf görsel efekt (clear'lar commit edilmiş)
    }

    private const int KindCount = 7;
    private readonly int[] counts = new int[KindCount];
    private int epoch;

    /// <summary>
    /// Bir aktiviteyi kaydeder. Dönen handle bir kez Dispose edilmeli (`using`/try-finally).
    /// Dispose EDGE'inde otomatik `Pump()` çalışır → iş bitince akış kendiliğinden ilerler
    /// (kimsenin "uyandırma" çağırması gerekmez; lost-wakeup imkânsız).
    /// </summary>
    public IDisposable Begin(ActivityKind kind) => new Handle(this, kind);

    public int Count(ActivityKind kind) => counts[(int)kind];

    /// <summary>Akışı bekleten (blocking) aktiviteler: uçuş ve saf sunum HARİÇ.</summary>
    public int BlockingActivityCount =>
        counts[(int)ActivityKind.Fall] +
        counts[(int)ActivityKind.Clear] +
        counts[(int)ActivityKind.SpecialSweep] +
        counts[(int)ActivityKind.ComboStep] +
        counts[(int)ActivityKind.ObstacleSpread];

    /// <summary>
    /// Board hâlâ akıyor mu (mantık VEYA görsel)? Faz 1: eski sinyallerin birleşimi + yeni kayıt.
    /// Tek otorite — yeni kod `IsBusy || BlockingBackgroundJobs > 0 || IsPlaying` diye ELLE
    /// birleştirmemeli, buraya sormalı.
    /// </summary>
    public bool IsSettling =>
        board != null &&
        (board.IsBusy
         || board.BlockingBackgroundJobs > 0
         || board.IsActionSequencePlaying
         || board.DetachedSequencerActions > 0
         || BlockingActivityCount > 0);

    /// <summary>
    /// Faz 7C: sütun-bazlı gravity/fall sinyali. Bu, global IsSettling'in yerine geçmez; Faz 8
    /// input gating'i "dokunulan hücrelerin sütunları boşta mı?" sorusunu buradan soracak.
    /// </summary>
    public bool IsColumnSettling(int x) =>
        board != null && board.IsColumnBusy(x);

    public bool AreColumnsSettling(int a, int b) =>
        IsColumnSettling(a) || IsColumnSettling(b);

    /// <summary>
    /// Bir special GÖRSEL olarak uçuşta mı (süpürme/dash/detached sunum)? Overlap kapısı bunu okur:
    /// açıkken detached fall başlatmak, süpürme bitmeden refill düşürme bug'ını geri getirir.
    /// </summary>
    public bool IsSpecialVisualInFlight =>
        board != null &&
        (board.IsSpecialVisualInFlight || counts[(int)ActivityKind.SpecialSweep] > 0);

    /// <summary>
    /// Bekleyen resolve şu an başlayabilir mi? (Girdi/kilit durumu çağıranda kalır.)
    /// </summary>
    public bool CanStartResolveStep =>
        board != null
        && !board.IsBusy
        && board.BlockingBackgroundJobs == 0
        && !board.IsActionSequencePlaying
        && BlockingActivityCount == 0;

    /// <summary>
    /// Sürekli pompa: bekleyen bir resolve varsa ve board boşaldıysa başlatır. Her frame + her
    /// activity bitişinde çağrılır → "iş sessizce bitti ama kimse uyandırmadı" (lost-wakeup) olamaz.
    /// </summary>
    public void Pump()
    {
        board?.TryStartResolveAfterActionSequence();
    }

    /// <summary>Tüm kayıtları geçersiz kılar (level teardown / force-drain). Sızan handle'lar no-op olur.</summary>
    public void DrainAll()
    {
        epoch++;
        Array.Clear(counts, 0, counts.Length);
    }

    private sealed class Handle : IDisposable
    {
        private BoardFlowScheduler owner;
        private readonly ActivityKind kind;
        private readonly int handleEpoch;
        private bool disposed;

        public Handle(BoardFlowScheduler owner, ActivityKind kind)
        {
            this.owner = owner;
            this.kind = kind;
            handleEpoch = owner.epoch;
            owner.counts[(int)kind]++;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            var o = owner;
            owner = null;
            if (o == null || handleEpoch != o.epoch) return;   // DrainAll sonrası: no-op

            int i = (int)kind;
            if (o.counts[i] > 0) o.counts[i]--;

            // İş bitti → akış ilerleyebilir mi diye HEMEN bak (edge-triggered bekleme yok).
            o.Pump();
        }
    }
}
