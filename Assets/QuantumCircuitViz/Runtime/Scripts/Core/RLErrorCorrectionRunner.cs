using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CSharpNumerics.Engines.Quantum;
using CSharpNumerics.ML.ReinforcementLearning.Algorithms.ValueBased;
using CSharpNumerics.ML.ReinforcementLearning.Core;
using CSharpNumerics.Numerics.Objects;
using CSharpNumerics.Physics.Quantum;

namespace QuantumCircuitViz.Core
{
    /// <summary>
    /// Runs DQN/DoubleDQN training against CSharpNumerics QuantumEnvironment
    /// on a background thread. Reports per-episode fidelity and loss for
    /// real-time visualization.
    /// </summary>
    public class RLErrorCorrectionRunner
    {
        public event Action<EpisodeReport> OnEpisodeComplete;
        public event Action<TrainingSummary> OnTrainingComplete;

        private DQN _agent;
        private QuantumEnvironment _env;
        private CancellationTokenSource _cts;
        private Task _trainingTask;

        public bool IsTraining => _trainingTask != null && !_trainingTask.IsCompleted;
        public int CompletedEpisodes { get; private set; }

        /// <summary>
        /// Start training on a background thread.
        /// targetCircuit: the ideal circuit the agent should learn to reproduce.
        /// </summary>
        public void StartTraining(
            int qubitCount,
            QuantumCircuit targetCircuit,
            int episodes,
            int maxGates,
            float fidelityThreshold,
            float learningRate,
            float gamma,
            bool useDoubleDQN = true)
        {
            Stop();
            CompletedEpisodes = 0;

            _env = QuantumEnvironment.Create(qubitCount)
                .WithTargetCircuit(targetCircuit)
                .WithMaxGates(maxGates)
                .WithFidelityThreshold(fidelityThreshold)
                .Build();

            _agent = useDoubleDQN ? new DoubleDQN() : new DQN();
            _agent.LearningRate = learningRate;
            _agent.Gamma = gamma;
            _agent.HiddenLayers = new[] { 64, 64 };
            _agent.BatchSize = 32;
            _agent.MinBufferSize = 64;
            _agent.TargetUpdateFrequency = 50;
            _agent.Initialize(_env.ObservationSize, _env.ActionSize);

            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            int epCount = episodes;

            _trainingTask = Task.Run(() => TrainLoop(epCount, token), token);
        }

        /// <summary>Stop training if running.</summary>
        public void Stop()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            _trainingTask = null;
        }

        /// <summary>
        /// Run one episode with the trained agent (no exploration) and return
        /// the final state + fidelity for visualization.
        /// </summary>
        public EpisodeReplay Replay()
        {
            if (_agent == null || _env == null) return null;

            var (obs, _) = _env.Reset();
            var steps = new List<ReplayStep>();
            bool done = false;
            double totalReward = 0;

            while (!done)
            {
                int action = _agent.SelectAction(obs);
                var (nextObs, reward, d, info) = _env.Step(action);

                steps.Add(new ReplayStep
                {
                    Action = action,
                    Reward = reward,
                    StateProbabilities = CopyVector(nextObs)
                });

                totalReward += reward;
                done = d;
                obs = nextObs;
            }

            return new EpisodeReplay
            {
                Steps = steps,
                TotalReward = totalReward,
                FinalFidelity = steps.Count > 0 ? steps[steps.Count - 1].Reward : 0
            };
        }

        private void TrainLoop(int episodes, CancellationToken token)
        {
            var reports = new List<EpisodeReport>();
            double bestFidelity = 0;

            for (int ep = 0; ep < episodes; ep++)
            {
                if (token.IsCancellationRequested) break;

                var (obs, _) = _env.Reset();
                var transitions = new List<Transition>();
                bool done = false;
                double totalReward = 0;
                int steps = 0;

                while (!done)
                {
                    if (token.IsCancellationRequested) break;

                    int action = _agent.SelectAction(obs);
                    var (nextObs, reward, d, info) = _env.Step(action);

                    var transition = new Transition(obs, action, reward, nextObs, d);
                    _agent.Train(transition);
                    transitions.Add(transition);

                    totalReward += reward;
                    done = d;
                    obs = nextObs;
                    steps++;
                }

                var episode = new Episode();
                foreach (var t in transitions) episode.Transitions.Add(t);
                _agent.EndEpisode(episode);

                double fidelity = totalReward; // env reward ≈ fidelity improvement
                if (fidelity > bestFidelity) bestFidelity = fidelity;

                CompletedEpisodes = ep + 1;
                var report = new EpisodeReport
                {
                    Episode = ep + 1,
                    TotalReward = totalReward,
                    Steps = steps,
                    Fidelity = fidelity,
                    BestFidelity = bestFidelity
                };
                reports.Add(report);
                OnEpisodeComplete?.Invoke(report);
            }

            if (!token.IsCancellationRequested)
            {
                OnTrainingComplete?.Invoke(new TrainingSummary
                {
                    TotalEpisodes = reports.Count,
                    BestFidelity = bestFidelity,
                    AverageReward = AverageOf(reports),
                    Reports = reports
                });
            }
        }

        private static double AverageOf(List<EpisodeReport> reports)
        {
            double sum = 0;
            foreach (var r in reports) sum += r.TotalReward;
            return sum / reports.Count;
        }

        private static double[] CopyVector(VectorN v)
        {
            var arr = new double[v.Length];
            for (int i = 0; i < v.Length; i++) arr[i] = v[i];
            return arr;
        }
    }

    public class EpisodeReport
    {
        public int Episode { get; set; }
        public double TotalReward { get; set; }
        public int Steps { get; set; }
        public double Fidelity { get; set; }
        public double BestFidelity { get; set; }
    }

    public class TrainingSummary
    {
        public int TotalEpisodes { get; set; }
        public double BestFidelity { get; set; }
        public double AverageReward { get; set; }
        public List<EpisodeReport> Reports { get; set; }
    }

    public class EpisodeReplay
    {
        public List<ReplayStep> Steps { get; set; }
        public double TotalReward { get; set; }
        public double FinalFidelity { get; set; }
    }

    public class ReplayStep
    {
        public int Action { get; set; }
        public double Reward { get; set; }
        public double[] StateProbabilities { get; set; }
    }
}
