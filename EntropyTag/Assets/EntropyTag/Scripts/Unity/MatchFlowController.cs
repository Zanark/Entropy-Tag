using System;
using EntropyTag.Application;
using EntropyTag.Domain;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    [DefaultExecutionOrder(-200)]
    public sealed class MatchFlowController : MonoBehaviour, IMatchArena
    {
        [SerializeField] private MatchFlowConfig configuration;
        [SerializeField] private PlayerInputSource input;
        [SerializeField] private MatchParticipant[] participants;
        private MatchSession session;
        private float scoreRefreshRemaining;

        public MatchFlowConfig Configuration => configuration;

        public MatchFlowRules Rules { get; private set; }

        public MatchSession Session => session;

        public MatchScoreSnapshot CurrentScore { get; private set; }

        public MatchParticipant PrimaryParticipant => participants[0];

        public int ParticipantCount => participants.Length;

        public MatchParticipant GetParticipant(int index) => participants[index];

        public event Action MatchReset;

        public event Action<MatchSessionState> StateChanged;

        public event Action<MatchPhase> PhaseChanged;

        public event Action<MatchResultSnapshot> MatchCompleted;

        public CircularArenaBoundary? PaintBoundary =>
            session != null && session.State == MatchSessionState.Active
                ? session.Boundary
                : (CircularArenaBoundary?)null;

        public void Configure(MatchFlowConfig config, PlayerInputSource playerInput, MatchParticipant[] players)
        {
            configuration = config != null ? config : throw new ArgumentNullException(nameof(config));
            input = playerInput != null ? playerInput : throw new ArgumentNullException(nameof(playerInput));
            participants = players != null ? (MatchParticipant[])players.Clone() : throw new ArgumentNullException(nameof(players));
        }

        public void StartMatch()
        {
            EnsureInitialized();
            session.StartMatch();
            RefreshScore();
        }

        public void RestartMatch()
        {
            EnsureInitialized();
            session.RestartMatch();
            RefreshScore();
        }

        public void Advance(float deltaSeconds)
        {
            EnsureInitialized();
            int previousMatchNumber = session.MatchNumber;
            double previousElapsed = session.ElapsedSeconds;
            session.Advance(deltaSeconds);
            if (session.State != MatchSessionState.Active)
            {
                return;
            }

            float activeDelta = (float)(session.MatchNumber == previousMatchNumber
                ? session.ElapsedSeconds - previousElapsed
                : session.ElapsedSeconds);
            for (int index = 0; index < participants.Length; index++)
            {
                participants[index].AdvancePressure(
                    session.Boundary, activeDelta, configuration.OutsideGraceSeconds);
            }

            scoreRefreshRemaining -= activeDelta;
            if (scoreRefreshRemaining <= 0f)
            {
                RefreshScore();
            }
        }

        public void RefreshScore()
        {
            EnsureInitialized();
            CurrentScore = session.CaptureScore();
            scoreRefreshRemaining = 0.2f;
        }

        public bool CanPaintAt(Vector3 point)
        {
            EnsureInitialized();
            return session.State == MatchSessionState.Waiting ||
                   (session.State == MatchSessionState.Active && session.Boundary.Contains(point.x, point.z));
        }

        void IMatchArena.Reset()
        {
            TerritorySurfaceRegistry.ResetAll();
            for (int index = 0; index < participants.Length; index++)
            {
                participants[index].ResetForMatch();
            }

            MatchReset?.Invoke();
        }

        MatchScoreSnapshot IMatchArena.CaptureScore(CircularArenaBoundary boundary)
        {
            return TerritorySurfaceRegistry.GetMatchScore(boundary);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Start()
        {
            RefreshScore();
            Debug.Log($"Match flow ready: {Rules.Timing.DurationSeconds:0}s, {participants.Length} participant(s), free play.", this);
        }

        private void Update()
        {
            if (input.WasConfirmMatchPressedThisFrame &&
                (session.State == MatchSessionState.Waiting || session.State == MatchSessionState.Results))
            {
                StartMatch();
            }
            else if (input.WasResetTerritoryPressedThisFrame && session.State != MatchSessionState.Waiting)
            {
                RestartMatch();
            }

            Advance(Time.deltaTime);
        }

        private void EnsureInitialized()
        {
            if (session != null)
            {
                return;
            }

            if (configuration == null || input == null || participants == null || participants.Length == 0)
            {
                throw new InvalidOperationException($"{name}: match flow requires configuration, input and at least one participant.");
            }

            Rules = configuration.CreateRules();
            for (int index = 0; index < participants.Length; index++)
            {
                if (participants[index] == null || Array.IndexOf(participants, participants[index]) != index)
                {
                    throw new InvalidOperationException($"{name}: match participants must be non-null and unique.");
                }

                participants[index].Initialize(this);
            }

            session = new MatchSession(Rules, this);
            session.StateChanged += HandleStateChanged;
            session.PhaseChanged += HandlePhaseChanged;
            session.MatchCompleted += HandleMatchCompleted;
            for (int index = 0; index < participants.Length; index++)
            {
                participants[index].ApplyState(MatchSessionState.Waiting);
            }
        }

        private void HandleStateChanged(MatchSessionState state)
        {
            for (int index = 0; index < participants.Length; index++)
            {
                participants[index].ApplyState(state);
            }

            RefreshScore();
            StateChanged?.Invoke(state);
        }

        private void HandlePhaseChanged(MatchPhase phase)
        {
            Debug.Log($"Match {session.MatchNumber}: {phase} at {session.ElapsedSeconds:0.00}s.", this);
            PhaseChanged?.Invoke(phase);
        }

        private void HandleMatchCompleted(MatchResultSnapshot result)
        {
            Debug.Log(
                $"Match {result.MatchNumber} complete: {result.Outcome.Kind}; " +
                $"Ice {result.Score.IceCoverage.Percentage:0.0}%, Fire {result.Score.FireCoverage.Percentage:0.0}%.", this);
            MatchCompleted?.Invoke(result);
        }
    }
}
