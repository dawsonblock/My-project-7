using System.Collections;
using Escape.Core;
using Escape.Gameplay;
using UnityEngine;
using UnityEngine.AI;

namespace Escape.AI
{
    public enum GuardState { Patrol, Investigate, Search, Alert, ReturnToPatrol }

    /// <summary>
    /// Guard finite state machine. Patrol → Investigate → Search → Alert →
    /// ReturnToPatrol. Vision/hearing are separate components; this brain
    /// only decides where to go and when to escalate.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class GuardBrain : MonoBehaviour
    {
        [SerializeField] private PatrolRoute route;
        [SerializeField] private string guardId = "guard";

        private NavMeshAgent _agent;
        private GuardVision _vision;
        private IGameStateService _state;
        private IGameCommandDispatcher _dispatcher;
        private IGameEventBus _events;
        private Data.StealthTuning _tuning;
        private Transform _player;

        private int _wpIndex;
        private float _waitUntil;
        private float _searchUntil;
        private float _lostSightAt = -1f;
        private Vector3 _investigateTarget;
        private Vector3 _searchCenter;
        private bool _alertWasSystem;

        public GuardState State { get; private set; } = GuardState.Patrol;
        public Vector3 InvestigateTarget => _investigateTarget;

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _vision = GetComponent<GuardVision>();
        }

        private void Start()
        {
            var services = GameRoot.Instance.Services;
            _state = services.Get<IGameStateService>();
            _dispatcher = services.Get<IGameCommandDispatcher>();
            _events = services.Get<IGameEventBus>();
            _tuning = services.Get<IContentDatabase>().Tuning;
            _events.Subscribe<AlertChangedEvent>(OnAlertChanged);
            var p = FindAnyObjectByType<PlayerState>();
            if (p != null) _player = p.transform;
        }

        private void OnDestroy()
        {
            _events?.Unsubscribe<AlertChangedEvent>(OnAlertChanged);
        }

        private void OnAlertChanged(AlertChangedEvent e)
        {
            if (e.Alert && State != GuardState.Alert)
            {
                _alertWasSystem = true;
                EnterAlert();
            }
            else if (!e.Alert && State == GuardState.Alert && _alertWasSystem)
            {
                EnterReturn();
            }
        }

        /// <summary>Called by GuardHearing when a noise reaches this guard.</summary>
        public void HearNoise(Vector3 position, float strength, NoiseType type)
        {
            if (State == GuardState.Alert) return;
            if (strength < 0.08f) return;
            _investigateTarget = position;
            EnterInvestigate();
        }

        private void Update()
        {
            if (_tuning == null) return;

            // Vision escalates independently of state.
            if (_vision != null && _vision.Perception > 0.6f && _vision.TimeSinceSeen < 0.1f)
            {
                _lostSightAt = -1f;
                if (State != GuardState.Alert && _detectionIsHigh())
                {
                    _alertWasSystem = false;
                    _dispatcher.Dispatch(new SetAlertCommand(true, guardId));
                    EnterAlert();
                }
                else if (State == GuardState.Patrol || State == GuardState.ReturnToPatrol)
                {
                    _investigateTarget = _vision.LastKnownPosition;
                    if (State != GuardState.Investigate) EnterInvestigate();
                }
            }

            switch (State)
            {
                case GuardState.Patrol: TickPatrol(); break;
                case GuardState.Investigate: TickInvestigate(); break;
                case GuardState.Search: TickSearch(); break;
                case GuardState.Alert: TickAlert(); break;
                case GuardState.ReturnToPatrol: TickReturn(); break;
            }
        }

        private bool _detectionIsHigh() =>
            _state.State.Detection >= _tuning.ImminentThreshold * 0.7f;

        // ---------- Patrol ----------

        private void TickPatrol()
        {
            if (route == null || route.Count == 0) return;
            var wp = route.Get(_wpIndex);
            if (wp == null || wp.Point == null) { NextWaypoint(); return; }

            if (Time.time < _waitUntil) return;
            _agent.speed = _tuning.GuardWalkSpeed;
            _agent.SetDestination(wp.Point.position);
            if (!_agent.pathPending && _agent.remainingDistance < 0.6f)
            {
                _waitUntil = Time.time + wp.WaitSeconds;
                NextWaypoint();
            }
        }

        private void NextWaypoint()
        {
            _wpIndex++;
            if (_wpIndex >= route.Count) _wpIndex = route.Loop ? 0 : route.Count - 1;
        }

        // ---------- Investigate ----------

        private void EnterInvestigate()
        {
            State = GuardState.Investigate;
            _agent.speed = _tuning.GuardInvestigateSpeed;
            _agent.SetDestination(_investigateTarget);
        }

        private void TickInvestigate()
        {
            if (_agent.pathPending) return;
            if (_agent.remainingDistance > 0.8f)
            {
                _agent.SetDestination(_investigateTarget);
                return;
            }
            // Arrived: look for the player around the stimulus point.
            _searchCenter = _investigateTarget;
            _searchUntil = Time.time + _tuning.SearchDuration;
            State = GuardState.Search;
            PickSearchPoint();
        }

        // ---------- Search ----------

        private void PickSearchPoint()
        {
            for (int i = 0; i < 6; i++)
            {
                Vector3 candidate = _searchCenter + UnityEngine.Random.insideUnitSphere * _tuning.SearchRadius;
                candidate.y = _searchCenter.y;
                if (NavMesh.SamplePosition(candidate, out var hit, _tuning.SearchRadius, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                    return;
                }
            }
        }

        private void TickSearch()
        {
            if (Time.time >= _searchUntil)
            {
                EnterReturn();
                return;
            }
            if (!_agent.pathPending && _agent.remainingDistance < 0.8f)
                PickSearchPoint();
        }

        // ---------- Alert ----------

        private void EnterAlert()
        {
            State = GuardState.Alert;
            _agent.speed = _tuning.GuardChaseSpeed;
            _lostSightAt = -1f;
        }

        private void TickAlert()
        {
            if (_player == null) return;

            bool sees = _vision != null && _vision.TimeSinceSeen < 0.25f;
            if (sees)
            {
                _lostSightAt = -1f;
                _agent.SetDestination(_player.position);
            }
            else
            {
                if (_lostSightAt < 0f) _lostSightAt = Time.time;
                _agent.SetDestination(_vision != null ? _vision.LastKnownPosition : _player.position);
                if (Time.time - _lostSightAt > _tuning.LoseSightAfterSeconds && !_state.State.Lockdown)
                {
                    _dispatcher.Dispatch(new SetAlertCommand(false, guardId));
                    EnterReturn();
                    return;
                }
            }

            if (!_agent.pathPending && _agent.remainingDistance < _tuning.GuardCatchDistance)
                _events.Publish(new PlayerCaughtEvent(guardId));
        }

        // ---------- Return ----------

        private void EnterReturn()
        {
            State = GuardState.ReturnToPatrol;
            _agent.speed = _tuning.GuardWalkSpeed;
            if (route != null && route.Count > 0)
            {
                int nearest = 0;
                float best = float.MaxValue;
                for (int i = 0; i < route.Count; i++)
                {
                    var p = route.Get(i)?.Point;
                    if (p == null) continue;
                    float d = (p.position - transform.position).sqrMagnitude;
                    if (d < best) { best = d; nearest = i; }
                }
                _wpIndex = nearest;
                _agent.SetDestination(route.Get(nearest).Point.position);
            }
        }

        private void TickReturn()
        {
            if (!_agent.pathPending && _agent.remainingDistance < 0.8f)
            {
                State = GuardState.Patrol;
                _waitUntil = 0f;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.magenta;
            if (State == GuardState.Investigate || State == GuardState.Search)
                Gizmos.DrawWireSphere(_investigateTarget, 0.4f);
        }
    }
}
