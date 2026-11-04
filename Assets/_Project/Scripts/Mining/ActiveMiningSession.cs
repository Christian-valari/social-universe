using System;
using System.Collections.Generic;
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public enum ActiveMiningStage { InProgress, Success, Failed }

    // Closing-ring tap minigame. Tapping a live ring before it closes banks a share of the asteroid
    // by how early the tap landed (Perfect, Good, Late). A ring that closes untapped, or a tap on
    // empty space while a ring is live, is a miss. MaxErrors misses fails the run with whatever
    // was banked; resolving every target succeeds it.
    // A tier holds one ring live at a time, or more where its LiveTargets says so: the next target
    // joins once the newest ring has run SecondTargetStagger of its way, and each ring is resolved
    // on its own. Either way a target also waits out the spawn gap that each resolve restarts.
    // A decoy can spawn beside a target. It is not a target: tapping it is a miss, ignoring it costs
    // nothing, and it never counts toward the run or holds back the next target.
    // A tier can make every VeinEvery-th target a vein, which is dragged along a line instead of
    // tapped; the view reports the outcome with CompleteVein or FailVein, and a ring that closes
    // first is a miss. A vein has the screen to itself: it waits for every live target to resolve
    // (dismissing live decoys at no cost), and nothing else spawns until it resolves.
// A vein's three times (ring, Perfect by, Good by) scale with its length: it spawns with those of the
// reference length, and the view that places it reports the length it measured with SetVeinLength
// before the ring starts aging.
    public sealed class ActiveMiningSession
    {
        // Engineering limit: a hitch can't age a ring, or burn the miss limit, in a single frame.
        private const float MaxTickSeconds = 0.1f;

        private readonly ActiveMiningRunSettings _settings;
        private readonly Func<float>              _random;
        private readonly List<ActiveMiningTarget> _live = new List<ActiveMiningTarget>();

        private float _bankedShares;
        private int   _spawned;
        private int   _nextTargetId = 1;
        private float _spawnGapRemaining;
        private float _graceRemaining;

        public int   Targets         { get; }
        public int   ResolvedTargets { get; private set; }
        public int   MaxErrors       { get; }
        public int   ErrorCount      { get; private set; }
        public int   PerfectCount    { get; private set; }
        public int   GoodCount       { get; private set; }
        public int   LateCount       { get; private set; }

        // Where the misses came from; they sum to ErrorCount. A vein whose ring closes is a ring miss.
        public int   RingMisses      { get; private set; }
        public int   DecoyTaps       { get; private set; }
        public int   WrongTaps       { get; private set; }
        public int   VeinFails       { get; private set; }

        // The share of the asteroid earned so far, 0-1 against a clean sweep of Perfect hits.
        public float BankedFraction => _bankedShares / Targets;

        public ActiveMiningStage                 Stage       { get; private set; } = ActiveMiningStage.InProgress;
        public IReadOnlyList<ActiveMiningTarget> LiveTargets => _live;
        public bool                              IsVeinLive  => FindLiveVein() != null;

        public event Action<ActiveMiningStage>                     OnStageChanged;
        public event Action<ActiveMiningTarget>                    OnTargetSpawned;
        public event Action<ActiveMiningTarget, ActiveMiningGrade> OnTargetResolved;
        public event Action                                        OnWrongTap;
        public event Action<ActiveMiningTarget>                    OnDecoyExpired;

        // random returns a roll from 0 up to 1 and decides whether a target gets a decoy.
        public ActiveMiningSession(ActiveMiningRunSettings settings, Func<float> random = null)
        {
            _settings = settings;
            _random   = random ?? (() => UnityEngine.Random.value);
            Targets   = Mathf.Max(1, settings.Tier.Targets);
            MaxErrors = Mathf.Max(1, settings.Tier.MaxMisses);

            // The player's thumb is still on Start, so the first ring waits out a lead-in.
            _spawnGapRemaining = settings.FirstTargetDelaySeconds;
        }

        // Call every frame while Stage == InProgress. Rings age first and the next target spawns
        // after, so a target spawned on a tick starts with its ring fully open.
        public void Tick(float deltaTime)
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            float step = Mathf.Clamp(deltaTime, 0f, MaxTickSeconds);
            _graceRemaining    -= step;
            _spawnGapRemaining -= step;

            AgeLiveTargets(step);

            if (Stage == ActiveMiningStage.InProgress && _spawned < Targets && NextIsVein() && !HasLiveTarget())
                DismissLiveDecoys();

            while (Stage == ActiveMiningStage.InProgress && _spawned < Targets && ShouldSpawn())
                SpawnTarget();
        }

        // The player tapped a live ring. One that isn't live (already tapped or already closed) is
        // ignored.
        public void TapTarget(int targetId)
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            var target = FindLive(targetId);
            if (target == null || target.Kind == ActiveMiningTargetKind.Vein) return;

            _graceRemaining = _settings.MissGraceSeconds;

            if (target.Kind == ActiveMiningTargetKind.Decoy)
                TapDecoy(target);
            else
                Resolve(target, GradeFor(target.ElapsedSeconds));
        }

        // Takes a live decoy off the board at no cost, for a view that has nowhere to show it clear of
        // the other markers. A real target, an unknown id, or a finished run is left alone.
        public void DismissDecoy(int targetId)
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            var target = FindLive(targetId);
            if (target == null || target.Kind != ActiveMiningTargetKind.Decoy) return;

            ExpireDecoy(target);
        }

        // The player dragged the vein from end to end; it grades on how long that took. An id that
        // isn't a live vein, or a finished run, is ignored.
        public void CompleteVein(int targetId)
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            var vein = FindLive(targetId);
            if (vein == null || vein.Kind != ActiveMiningTargetKind.Vein) return;

            _graceRemaining = _settings.MissGraceSeconds;
            Resolve(vein, GradeForVein(vein));
        }

        // The player lifted early or strayed off the line. The vein is used up as a miss, with no
        // retry. An id that isn't a live vein, or a finished run, is ignored.
        public void FailVein(int targetId)
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            var vein = FindLive(targetId);
            if (vein == null || vein.Kind != ActiveMiningTargetKind.Vein) return;

            _graceRemaining = _settings.MissGraceSeconds;
            Resolve(vein, ActiveMiningGrade.Miss, failedVein: true);
        }

        // The view measured a vein, in canvas units, as it placed it; a longer vein gets longer to drag. The
        // times are fixed once the ring starts aging, so this can't be used to extend one. An id that isn't
        // a live vein, or a finished run, is ignored.
        public void SetVeinLength(int targetId, float length)
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            var vein = FindLive(targetId);
            if (vein == null || vein.Kind != ActiveMiningTargetKind.Vein || vein.ElapsedSeconds > 0f) return;

            SetVeinTimes(vein, length);
        }

        // The player tapped empty space. Counts as a miss while a target is live, unless it follows
        // right behind a hit; the ring itself carries on.
        public void TapOffTarget()
        {
            if (Stage != ActiveMiningStage.InProgress) return;
            if (!HasLiveTarget() || _graceRemaining > 0f) return;

            ErrorCount++;
            WrongTaps++;
            OnWrongTap?.Invoke();
            CheckFinished();
        }

        private void AgeLiveTargets(float step)
        {
            for (int i = 0; i < _live.Count; i++)
                _live[i].ElapsedSeconds += step;

            int next = 0;
            while (next < _live.Count && Stage == ActiveMiningStage.InProgress)
            {
                var target = _live[next];
                if (target.ElapsedSeconds < target.RingSeconds)
                    next++;
                else if (target.Kind == ActiveMiningTargetKind.Decoy)
                    ExpireDecoy(target); // leaves the list, so the next one slides into `next`
                else
                    Resolve(target, ActiveMiningGrade.Miss);
            }
        }

        private void SpawnTarget()
        {
            bool vein = NextIsVein();
            var target = vein
                ? NewVein()
                : new ActiveMiningTarget(_nextTargetId++, ActiveMiningTargetKind.Normal, _settings.Tier.RingSeconds);
            _spawned++;
            _live.Add(target);
            OnTargetSpawned?.Invoke(target);

            // A vein has the screen to itself, so it rolls for no decoy.
            if (!vein && _settings.Tier.DecoyChance > 0f && _random() < _settings.Tier.DecoyChance)
                SpawnDecoy();
        }

        // A vein spawns with the times for the reference length; a view that measures it calls SetVeinLength.
        private ActiveMiningTarget NewVein()
        {
            var vein = new ActiveMiningTarget(_nextTargetId++, ActiveMiningTargetKind.Vein, _settings.VeinRingSeconds);
            SetVeinTimes(vein, _settings.VeinReferenceLength);
            return vein;
        }

        private void SetVeinTimes(ActiveMiningTarget vein, float length)
        {
            vein.RingSeconds      = _settings.VeinRingSecondsFor(length);
            vein.PerfectBySeconds = _settings.VeinPerfectBySecondsFor(length);
            vein.GoodBySeconds    = _settings.VeinGoodBySecondsFor(length);
        }

        // Decoys have their own ids but add nothing to _spawned, so Targets stays a count of real ones.
        private void SpawnDecoy()
        {
            var decoy = new ActiveMiningTarget(_nextTargetId++, ActiveMiningTargetKind.Decoy, _settings.Tier.RingSeconds);
            _live.Add(decoy);
            OnTargetSpawned?.Invoke(decoy);
        }

        private bool HasLiveTarget()
        {
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].Kind == ActiveMiningTargetKind.Normal) return true;
            return false;
        }

        private bool NextIsVein()
        {
            int every = _settings.Tier.VeinEvery;
            return every > 0 && (_spawned + 1) % every == 0;
        }

        private ActiveMiningTarget FindLiveVein()
        {
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].Kind == ActiveMiningTargetKind.Vein) return _live[i];
            return null;
        }

        private void DismissLiveDecoys()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
                if (_live[i].Kind == ActiveMiningTargetKind.Decoy) ExpireDecoy(_live[i]);
        }

        // Decoys count for nothing here: only real targets fill the limit or set the stagger. A vein
        // waits for every real target to resolve and holds the screen until it does itself.
        private bool ShouldSpawn()
        {
            if (IsVeinLive) return false;

            int liveReal = 0;
            ActiveMiningTarget newest = null;
            for (int i = 0; i < _live.Count; i++)
            {
                if (_live[i].Kind != ActiveMiningTargetKind.Normal) continue;
                liveReal++;
                newest = _live[i];
            }

            if (_spawnGapRemaining > 0f) return false;
            if (liveReal == 0) return true;
            if (NextIsVein()) return false;

            return liveReal < Mathf.Max(1, _settings.Tier.LiveTargets)
                && newest.Progress >= _settings.SecondTargetStagger;
        }

        // Inclusive bounds: a tap exactly on a cut-off earns the better grade.
        private ActiveMiningGrade GradeFor(float elapsedSeconds) =>
            GradeAt(elapsedSeconds, _settings.Tier.PerfectBySeconds, _settings.Tier.GoodBySeconds);

        private static ActiveMiningGrade GradeForVein(ActiveMiningTarget vein) =>
            GradeAt(vein.ElapsedSeconds, vein.PerfectBySeconds, vein.GoodBySeconds);

        private static ActiveMiningGrade GradeAt(float elapsedSeconds, float perfectBySeconds, float goodBySeconds)
        {
            if (elapsedSeconds <= perfectBySeconds) return ActiveMiningGrade.Perfect;
            if (elapsedSeconds <= goodBySeconds)    return ActiveMiningGrade.Good;
            return ActiveMiningGrade.Late;
        }

        // A vein that was failed is told apart from one whose ring closed; both are one miss.
        private void Resolve(ActiveMiningTarget target, ActiveMiningGrade grade, bool failedVein = false)
        {
            _live.Remove(target);
            ResolvedTargets++;
            _spawnGapRemaining = _settings.SpawnGapSeconds;

            switch (grade)
            {
                case ActiveMiningGrade.Perfect: PerfectCount++; _bankedShares += _settings.PerfectShare; break;
                case ActiveMiningGrade.Good:    GoodCount++;    _bankedShares += _settings.GoodShare;    break;
                case ActiveMiningGrade.Late:    LateCount++;    _bankedShares += _settings.LateShare;    break;
                default:
                    ErrorCount++;
                    if (failedVein) VeinFails++; else RingMisses++;
                    break;
            }

            OnTargetResolved?.Invoke(target, grade);
            CheckFinished();
        }

        // A decoy leaves the run's counts and its spawn gap alone: it costs a miss and nothing else.
        private void TapDecoy(ActiveMiningTarget decoy)
        {
            _live.Remove(decoy);
            ErrorCount++;
            DecoyTaps++;
            OnTargetResolved?.Invoke(decoy, ActiveMiningGrade.Miss);
            CheckFinished();
        }

        private void ExpireDecoy(ActiveMiningTarget decoy)
        {
            _live.Remove(decoy);
            OnDecoyExpired?.Invoke(decoy);
        }

        // The miss limit is checked first, so a miss on the last target is a failure, not a clear.
        private void CheckFinished()
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            if (ErrorCount >= MaxErrors)
                SetStage(ActiveMiningStage.Failed);
            else if (ResolvedTargets >= Targets)
                SetStage(ActiveMiningStage.Success);
        }

        private ActiveMiningTarget FindLive(int targetId)
        {
            for (int i = 0; i < _live.Count; i++)
                if (_live[i].Id == targetId) return _live[i];
            return null;
        }

        private void SetStage(ActiveMiningStage stage)
        {
            Stage = stage;
            OnStageChanged?.Invoke(stage);
        }
    }
}
