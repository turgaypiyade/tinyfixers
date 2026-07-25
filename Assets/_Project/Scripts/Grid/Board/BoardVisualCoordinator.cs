using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Faz 1 decoupled-resolve playback koordinatörü. Amaç: normal cascade fall TAM bitmeden,
/// match'i oluşturan taşlar hücrelerine VARINCA (event-driven, timed sync YOK) clear'ı başlatmak
/// (overlap). Böylece referans oyundaki "son taş gride girerken match başlar" hissi elde edilir.
///
/// Tek-blocking-sequencer kısıtı (bkz. Docs/DecoupledResolve_Plan.md §6): ExecuteClearPass
/// clear'ı ActionSequencer'a MatchClearAction olarak enqueue eder ve FallAction Blocking'tir →
/// düşüş oynarken clear kuyruğa girse düşüşün TAMAMI bitene kadar bekler, overlap olmaz. Çözüm:
/// overlap path'inde fall'u sequencer DIŞINDA (detached StartCoroutine) koştururuz; ana sequencer
/// boş kalır, ExecuteClearPass onu clear için kullanır → clear, fall tail'iyle PARALEL oynar.
///
/// KARAR mantığı (hangi match temizlenir, hangi special oluşur/aktive olur) DEĞİŞMEZ; yalnız
/// görsel başlangıç zamanı bağımlılık kuralına bağlanır. Zor yollar (special aktivasyon, obstacle)
/// çağıran tarafta gate ile bugünkü seri yola düşer.
/// </summary>
public class BoardVisualCoordinator
{
    private readonly BoardController board;
    private readonly ActionSequencer sequencer;

    public BoardVisualCoordinator(BoardController board, ActionSequencer sequencer)
    {
        this.board = board;
        this.sequencer = sequencer;
    }

    /// <summary>
    /// Cascade fall action'larını ana sequencer'ın DIŞINDA koşturur; match taşları hücrelerine
    /// vardıklarında (TileView.FallArrived event'i — timed/polling senkron YOK) runClear'ı paralel
    /// başlatır. Hem fall hem clear bitince döner (resolve loop state'i tutarlı kalsın).
    /// </summary>
    public IEnumerator PlayFallWithOverlappedClear(
        List<BoardAction> fallActions,
        HashSet<TileView> matchTiles,
        Func<IEnumerator> runClear)
    {
        // Event-driven arrival: her match taşı hücresine oturunca FallArrived atar → pending'den düşer.
        var pending = new HashSet<TileView>();
        if (matchTiles != null)
            foreach (var t in matchTiles)
                if (t != null && t) pending.Add(t);

        Action<TileView> onArrived = null;
        onArrived = (tile) => { pending.Remove(tile); };
        foreach (var t in pending)
            t.FallArrived += onArrived;

        bool fallDone = false;
        board.StartCoroutine(RunActionsDetached(fallActions, () => fallDone = true));

        // Match taşları event'le varana kadar bekle (poll DEĞİL — handler pending'i boşaltır).
        // Güvenlik: bu cascade'de HAREKET ETMEYEN (event atmayan, zaten oturmuş) match taşı varsa
        // fallDone ile geç → deadlock yok.
        while (!fallDone && pending.Count > 0)
            yield return null;

        foreach (var t in matchTiles)
            if (t != null && t) t.FallArrived -= onArrived;

        bool clearDone = false;
        board.StartCoroutine(Wrap(runClear != null ? runClear() : null, () => clearDone = true));

        while (!fallDone || !clearDone)
            yield return null;
    }

    // ActionSequencer.PlaySequence'in blocking semantiğini taklit eder ama ANA kuyruktan
    // bağımsız (detached) koşar → resolve loop bunu beklerken sequencer clear için serbest kalır.
    private IEnumerator RunActionsDetached(List<BoardAction> actions, Action onDone)
    {
        if (actions != null)
        {
            foreach (var a in actions)
            {
                if (a == null) continue;
                if (a.Blocking)
                    yield return board.StartCoroutine(a.ExecuteVisuals(sequencer));
                else
                    board.StartCoroutine(a.ExecuteVisuals(sequencer));
            }
        }
        onDone?.Invoke();
    }

    // Coroutine'i istisna-güvenli adımlar; bitince onDone çağırır (RunTogether deseni).
    private IEnumerator Wrap(IEnumerator inner, Action onDone)
    {
        if (inner != null)
        {
            while (true)
            {
                bool hasNext;
                try { hasNext = inner.MoveNext(); }
                catch (Exception ex) { Debug.LogException(ex); break; }
                if (!hasNext) break;
                yield return inner.Current;
            }
        }
        onDone?.Invoke();
    }
}
