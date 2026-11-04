using System;
using System.Collections.Generic;
using UnityEngine;
using SocialUniverse.Config;

namespace SocialUniverse.Mining
{
    public enum ActiveMiningStage { InProgress, Success, Failed }

    // Closing-ring tap minigame. One ring is live at a time: tapping it before it closes banks a
    // share of the asteroid by how early the tap landed (Perfect, Good, Late). A ring that closes
    // untapped, or a tap on empty space while a ring is live, is a miss. MaxErrors misses fails
    // the run with whatever was banked; resolving every target succeeds it.
    // A decoy can spawn beside a target. It is not a target: tapping it is a miss, ignoring it costs
    // nothing, and it never counts toward the run or holds back the next target.
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

        // The share of the asteroid earned so far, 0-1 against a clean sweep of Perfect hits.
        public float BankedFraction => _bankedShares / Targets;

        public ActiveMiningStage                 Stage       { get; private set; } = ActiveMiningStage.InProgress;
        public IReadOnlyList<ActiveMiningTarget> LiveTargets => _live;

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

            if (Stage == ActiveMiningStage.InProgress
                && !HasLiveTarget()
                && _spawned < Targets
                && _spawnGapRemaining <= 0f)
                SpawnTarget();
        }

        // The player tapped a live ring. One that isn't live (already tapped or already closed) is
        // ignored.
        public void TapTarget(int targetId)
        {
            if (Stage != ActiveMiningStage.InProgress) return;

            var target = FindLive(targetId);
            if (target == null) return;

            _graceRemaining = _settings.MissGraceSeconds;

            if (target.Kind == ActiveMiningTargetKind.Decoy)
                TapDecoy(target);
            else
                Resolve(target, GradeFor(target.ElapsedSeconds));
        }

        // The player tapped empty space. Counts as a miss while a target is live, unless it follows
        // right behind a hit; the ring itself carries on.
        public void TapOffTarget()
        {
            if (Stage != ActiveMiningStage.InProgress) return;
            if (!HasLiveTarget() || _graceRemaining > 0f) return;

            ErrorCount++;
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
            var target = new ActiveMiningTarget(_nextTargetId++, ActiveMiningTargetKind.Normal, _settings.Tier.RingSeconds);
            _spawned++;
            _live.Add(target);
            OnTargetSpawned?.Invoke(target);

            if (_settings.Tier.DecoyChance > 0f && _random() < _settings.Tier.DecoyChance)
                SpawnDecoy();
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

        // Inclusive bounds: a tap exactly on a cut-off earns the better grade.
        private ActiveMiningGrade GradeFor(float elapsedSeconds)
        {
            if (elapsedSeconds <= _settings.Tier.PerfectBySeconds) return ActiveMiningGrade.Perfect;
            if (elapsedSeconds <= _settings.Tier.GoodBySeconds)    return ActiveMiningGrade.Good;
            return ActiveMiningGrade.Late;
        }

        private void Resolve(ActiveMiningTarget target, ActiveMiningGrade grade)
        {
            _live.Remove(target);
            ResolvedTargets++;
            _spawnGapRemaining = _settings.SpawnGapSeconds;

            switch (grade)
            {
                case ActiveMiningGrade.Perfect: PerfectCount++; _bankedShares += _settings.PerfectShare; break;
                case ActiveMiningGrade.Good:    GoodCount++;    _bankedShares += _settings.GoodShare;    break;
                case ActiveMiningGrade.Late:    LateCount++;    _bankedShares += _settings.LateShare;    break;
                default:                        ErrorCount++;                                            break;
            }

            OnTargetResolved?.Invoke(target, grade);
            CheckFinished();
        }

        // A decoy leaves the run's counts and its spawn gap alone: it costs a miss and nothing else.
        private void TapDecoy(ActiveMiningTarget decoy)
        {
            _live.Remove(decoy);
            ErrorCount++;
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
