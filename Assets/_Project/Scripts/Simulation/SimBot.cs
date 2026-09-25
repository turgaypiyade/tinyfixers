using System.Collections.Generic;

/// <summary>
/// Hamle seçici — GERÇEK lookahead.
///
/// Eski bot tahtaya bakıp "bu takas iyi görünüyor" diye sezgisel puan veriyordu; special
/// etkilerinin ayrı bir TAHMİN kopyasını taşıyordu ve motorla sürekli ayrışıyordu.
/// Yeni bot her adayı motorun KENDİSİNDE oynayıp sonucu ölçer:
///   klonla → hamleyi oyna → hedef ilerlemesi / obstacle hasarı / tahta sağlığı ölç.
///
/// Refill belirsizliği: arama dallarında yeni taşlar "phantom" doğar (eşleşmez), yani bot
/// göremeyeceği rastgele cascade'lerden puan kazanamaz. Ölçtüğü şey oyuncunun da
/// görebildiği deterministik sonuçtur.
///
/// Bu sınıf ÖRNEK bazlıdır (static değil) — her oyun kendi botunu taşır, böylece binlerce
/// oyun paralel koşabilir.
/// </summary>
public sealed class SimBot
{
    private readonly SimPlayerProfile _profile;
    private readonly List<SimSwap> _moves = new(64);
    private readonly List<SimSwap> _childMoves = new(64);
    private readonly List<float> _scores = new(64);
    private readonly List<int> _order = new(64);
    private readonly List<SimSwap> _countBuf = new(64);

    private SimGame _scratchA;   // derinlik 1
    private SimGame _scratchB;   // derinlik 2

    public SimBot(SimPlayerProfile profile)
    {
        _profile = profile ?? SimPlayerProfile.Average;
    }

    /// <summary>Geçerli hamle yoksa null.</summary>
    public SimSwap? PickMove(SimGame game, System.Random rng)
    {
        game.FindMoves(_moves);
        if (_moves.Count == 0) return null;
        if (_moves.Count == 1) return _moves[0];

        // Dikkat dağınıklığı: düşünmeden oyna.
        if (_profile.BlunderChance > 0f && rng.NextDouble() < _profile.BlunderChance)
            return _moves[rng.Next(_moves.Count)];

        _scratchA ??= game.CreateScratch();

        var baseline = Snapshot.Of(game);

        _scores.Clear();
        for (int i = 0; i < _moves.Count; i++)
        {
            // "Bu hamleyi görmedim": aday elenir (ama hepsi elenirse yine de bir şey oynanır).
            if (_profile.MissChance > 0f && rng.NextDouble() < _profile.MissChance)
            {
                _scores.Add(float.NegativeInfinity);
                continue;
            }

            _scores.Add(EvaluateMove(game, _moves[i], baseline, rng, depth: _profile.Depth));
        }

        int best = ArgMax(_scores);
        if (best < 0)   // hepsi gözden kaçtı → rastgele
            return _moves[rng.Next(_moves.Count)];

        if (_profile.Temperature <= 0f)
            return _moves[best];

        return _moves[SoftmaxPick(_scores, _profile.Temperature, rng)];
    }

    // ── Değerlendirme ────────────────────────────────────────────────────────

    private float EvaluateMove(SimGame game, SimSwap swap, in Snapshot baseline, System.Random rng, int depth)
    {
        var branch = _scratchA ??= game.CreateScratch();
        branch.CopyFrom(game, rng.Next());
        branch.State.SearchMode = true;   // yeni taşlar phantom → sahte cascade puanı yok

        var stats = branch.PlayMove(swap);
        float score = Score(branch, baseline, stats);

        if (branch.Goals.AllMet)
            return score + 10000f;        // bu hamle leveli bitiriyor

        if (depth > 1 && branch.MovesLeft > 0)
        {
            float follow = BestFollowUp(branch, rng);
            score += follow * 0.55f;      // ikinci hamlenin değeri indirimli
        }

        return score;
    }

    /// <summary>Depth-2: daldan sonraki en iyi hamlenin (beam ile sınırlı) değeri.</summary>
    private float BestFollowUp(SimGame branch, System.Random rng)
    {
        branch.FindMoves(_childMoves);
        if (_childMoves.Count == 0) return -50f;   // deadlock riski cezası

        _scratchB ??= branch.CreateScratch();
        var baseline = Snapshot.Of(branch);

        int tried = 0;
        float best = float.NegativeInfinity;
        int step = _childMoves.Count > _profile.BeamWidth
            ? _childMoves.Count / _profile.BeamWidth : 1;

        for (int i = 0; i < _childMoves.Count && tried < _profile.BeamWidth; i += step, tried++)
        {
            _scratchB.CopyFrom(branch, rng.Next());
            _scratchB.State.SearchMode = true;
            var stats = _scratchB.PlayMove(_childMoves[i]);
            float sc = Score(_scratchB, baseline, stats);
            if (_scratchB.Goals.AllMet) sc += 10000f;
            if (sc > best) best = sc;
        }

        return best == float.NegativeInfinity ? 0f : best;
    }

    /// <summary>
    /// Hamlenin değeri. Ana terim HEDEF İLERLEMESİ; gerisi oyuncunun gerçekten
    /// umursadığı ikincil sinyaller (obstacle hasarı, special biriktirme, tahta sağlığı).
    /// </summary>
    private float Score(SimGame after, in Snapshot before, in SimMoveStats stats)
    {
        float score = 0f;

        // 1) Hedef tamamlanma oranı — baskın terim.
        score += (after.Goals.Completion - before.Completion) * 1200f;

        // 2) Kalan hedef birimi (oran kabaysa ayrıntıyı bu yakalar).
        // 3) Hedef obstacle'a verilen KISMİ hasar (henüz kırılmadıysa bile ilerlemedir).
        // İKİSİ DE "nişan alma" terimidir: botun hesapla bulduğu, oyuncunun ancak kısmen gördüğü
        // ilerleme. GoalFocus ile profile göre kısılır — yoksa bot her levelı hedefe kilitlenmiş
        // kusursuz nişancı gibi oynar ve köşedeki/dar kanaldaki hedefler olduğundan kolay çıkar.
        float aim = _profile.GoalFocus;
        score += (before.RemainingUnits - after.Goals.RemainingUnits) * 3f * aim;
        score += (before.GoalDamage - after.Obstacles.GoalDamageRemaining(after.Goals)) * 18f * aim;

        // 4) Special biriktirme: yeni special tahtada potansiyeldir, harcamak anlık kazançtır.
        score += (after.SpecialPotential() - before.SpecialPotential) * 9f * _profile.SpecialPatience;
        score += stats.SpecialsCreated * 12f;
        score += stats.CombosActivated * 20f;

        // 5) Tahta sağlığı. DİKKAT: arama dalında yeni taşlar phantom (eşleşmez) doğduğu için
        // büyük temizlikler hamle sayısını yapay olarak düşürür — düz "hamle sayısı × ağırlık"
        // iyi hamleleri cezalandırırdı. O yüzden yalnız KRİTİK azlık cezalandırılır.
        int movesAfter = MoveCount(after);
        if (movesAfter <= 2) score -= (3 - movesAfter) * 30f;

        // 6) Hedefe yakın oynamak (hiçbir ilerleme yokken yön veren zayıf sinyal) — bu da nişan.
        score += ProximityBonus(after) * aim;

        // 7) Çok zayıf tiebreak: iş yapan hamle, hiçbir şey yapmayana yeğdir.
        score += stats.TilesCleared * 0.12f;
        score += stats.CascadeSteps * 0.5f;

        return score;
    }

    private int MoveCount(SimGame game)
    {
        game.FindMoves(_countBuf);
        return _countBuf.Count;
    }

    private static float ProximityBonus(SimGame game)
    {
        var cells = game.ClearedCells;
        if (cells.Count == 0) return 0f;

        float sum = 0f;
        int sampled = 0;
        for (int i = 0; i < cells.Count && sampled < 24; i++, sampled++)
        {
            int d = game.Obstacles.DistanceToNearestGoalObstacle(cells[i].x, cells[i].y, game.Goals, 3);
            if (d >= 0) sum += 3f / (d + 1);
        }
        return sum;
    }

    // ── Seçim yardımcıları ───────────────────────────────────────────────────

    private static int ArgMax(List<float> scores)
    {
        int best = -1;
        float bestValue = float.NegativeInfinity;
        for (int i = 0; i < scores.Count; i++)
            if (scores[i] > bestValue) { bestValue = scores[i]; best = i; }
        return bestValue == float.NegativeInfinity ? -1 : best;
    }

    /// <summary>
    /// Softmax: en iyi hamleye en yüksek şans, ama "neredeyse aynı iyilikte" hamleler de
    /// seçilebilir. Gerçek oyuncu kararsızlığı böyle görünür — sabit %20 zar atmaktan farklı.
    /// </summary>
    private int SoftmaxPick(List<float> scores, float temperature, System.Random rng)
    {
        float max = float.NegativeInfinity;
        for (int i = 0; i < scores.Count; i++)
            if (scores[i] > max) max = scores[i];

        // Sıcaklık puan ölçeğine göre normalize edilir, yoksa 1200'lük terimler zarı ezer.
        float scale = System.Math.Max(1f, System.Math.Abs(max)) * temperature;

        _order.Clear();
        double total = 0;
        for (int i = 0; i < scores.Count; i++)
        {
            if (float.IsNegativeInfinity(scores[i])) { _order.Add(0); continue; }
            double w = System.Math.Exp((scores[i] - max) / scale);
            total += w;
            _order.Add((int)(w * 1_000_000));
        }

        if (total <= 0) return 0;

        long roll = (long)(rng.NextDouble() * SumOf(_order));
        long acc = 0;
        for (int i = 0; i < _order.Count; i++)
        {
            acc += _order[i];
            if (roll < acc) return i;
        }
        return _order.Count - 1;
    }

    private static long SumOf(List<int> values)
    {
        long sum = 0;
        for (int i = 0; i < values.Count; i++) sum += values[i];
        return sum < 1 ? 1 : sum;
    }

    // ── Karşılaştırma anı ────────────────────────────────────────────────────

    private readonly struct Snapshot
    {
        public readonly float Completion;
        public readonly int RemainingUnits;
        public readonly int GoalDamage;
        public readonly int SpecialPotential;

        private Snapshot(float completion, int remainingUnits, int goalDamage, int specialPotential)
        {
            Completion = completion;
            RemainingUnits = remainingUnits;
            GoalDamage = goalDamage;
            SpecialPotential = specialPotential;
        }

        public static Snapshot Of(SimGame g) => new(
            g.Goals.Completion,
            g.Goals.RemainingUnits,
            g.Obstacles.GoalDamageRemaining(g.Goals),
            g.SpecialPotential());
    }
}
