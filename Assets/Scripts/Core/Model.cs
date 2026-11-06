using System;
using System.Collections.Generic;
using System.Globalization;

namespace NpcLab.Core
{
    [Serializable]
    public struct Vec2
    {
        public double x, z;
        public Vec2(double x, double z) { this.x = x; this.z = z; }
        public double Length { get { return Math.Sqrt(x * x + z * z); } }
        public Vec2 Normalized { get { double n = Length; return n < 1e-12 ? new Vec2() : this / n; } }
        public static Vec2 operator +(Vec2 a, Vec2 b) { return new Vec2(a.x + b.x, a.z + b.z); }
        public static Vec2 operator -(Vec2 a, Vec2 b) { return new Vec2(a.x - b.x, a.z - b.z); }
        public static Vec2 operator *(Vec2 a, double b) { return new Vec2(a.x * b, a.z * b); }
        public static Vec2 operator /(Vec2 a, double b) { return new Vec2(a.x / b, a.z / b); }
        public static double Dot(Vec2 a, Vec2 b) { return a.x * b.x + a.z * b.z; }
        public static double Distance(Vec2 a, Vec2 b) { return (a - b).Length; }
    }

    public static class Angles
    {
        public const double ToRadians = Math.PI / 180.0;
        public static double Wrap(double a) { double r = a * ToRadians; return Math.Atan2(Math.Sin(r), Math.Cos(r)) / ToRadians; }
        public static double Heading(Vec2 v) { return Math.Atan2(v.x, v.z) / ToRadians; }
        public static Vec2 Forward(double heading) { double r = heading * ToRadians; return new Vec2(Math.Sin(r), Math.Cos(r)); }
        public static double Clamp(double x, double lo, double hi) { return Math.Max(lo, Math.Min(hi, x)); }
        public static bool Finite(double n) { return !double.IsNaN(n) && !double.IsInfinity(n); }
        public static string Number(double n) { return n.ToString("R", CultureInfo.InvariantCulture); }
    }

    public enum ControllerKind { Proportional, Pfl3 }
    public enum ScenarioKind { Open, Obstacles, MovingGoal }
    public enum TrialState { Ready, Running, Paused, Success, Timeout, Aborted }

    [Serializable]
    public sealed class SimSettings
    {
        public double dt = 0.02, arenaSize = 20, maxSeconds = 60;
        public double maxSpeed = 1.5, maxTurn = 120, arrivalRadius = 0.5, agentRadius = 0.25;
        public double proportionalGain = 2, pfl3Gain = 1, tau = 0.1;
        public double lookAhead = 3, clearance = 0.2, reorientDegrees = 15, reorientDwell = 0.5;
        public SimSettings Copy() { return (SimSettings)MemberwiseClone(); }
        public void Validate()
        {
            double[] values = { dt, arenaSize, maxSeconds, maxSpeed, maxTurn, arrivalRadius, agentRadius,
                proportionalGain, pfl3Gain, tau, lookAhead, clearance, reorientDegrees, reorientDwell };
            foreach (double v in values) if (!Angles.Finite(v)) throw new ArgumentException("Non-finite setting.");
            if (dt <= 0 || dt > 0.1 || maxSeconds <= dt || maxSpeed <= 0 || maxTurn <= 0 ||
                arenaSize < 4 || agentRadius <= 0 || arrivalRadius <= 0 || clearance < 0 || tau < 0 ||
                proportionalGain <= 0 || pfl3Gain <= 0 || lookAhead <= 0 || reorientDegrees <= 0 ||
                reorientDwell <= 0 || arenaSize <= 4 * agentRadius)
                throw new ArgumentException("Invalid simulation settings.");
        }
    }

    [Serializable]
    public sealed class CircleObstacle
    {
        public int id;
        public Vec2 center;
        public double radius;
        public CircleObstacle() { }
        public CircleObstacle(int id, Vec2 center, double radius) { this.id = id; this.center = center; this.radius = radius; }
        public CircleObstacle Copy() { return new CircleObstacle(id, center, radius); }
    }

    [Serializable]
    public sealed class TrialDefinition
    {
        public string schemaVersion = "1", configurationId = "A-00", datasetRole = "development-pilot";
        public int scenario, index;
        public uint seed;
        public Vec2 start, goal, secondGoal;
        public double initialHeading, goalChangeSeconds = -1;
        public CircleObstacle[] obstacles = new CircleObstacle[0];
        public SimSettings settings = new SimSettings();
        public TrialDefinition Copy()
        {
            TrialDefinition d = (TrialDefinition)MemberwiseClone();
            d.settings = settings.Copy(); d.obstacles = new CircleObstacle[obstacles.Length];
            for (int i = 0; i < obstacles.Length; ++i) d.obstacles[i] = obstacles[i].Copy();
            return d;
        }
        public void Validate()
        {
            if (settings == null || obstacles == null) throw new ArgumentException("Missing settings or obstacles.");
            settings.Validate();
            if (scenario < 0 || scenario > 2 || !Angles.Finite(initialHeading)) throw new ArgumentException("Invalid scenario or heading.");
            double half = settings.arenaSize / 2 - settings.agentRadius;
            foreach (Vec2 v in new[] { start, goal, secondGoal })
                if (!Angles.Finite(v.x) || !Angles.Finite(v.z) || Math.Abs(v.x) > half || Math.Abs(v.z) > half)
                    throw new ArgumentException("Position outside the arena.");
            if (!Angles.Finite(goalChangeSeconds) || (scenario == 2 && (goalChangeSeconds <= 0 || goalChangeSeconds >= settings.maxSeconds)))
                throw new ArgumentException("The goal switch must precede timeout.");
            HashSet<int> ids = new HashSet<int>();
            foreach (CircleObstacle o in obstacles)
            {
                if (o == null || o.id < 0 || !ids.Add(o.id) || !Angles.Finite(o.radius) || o.radius <= 0 ||
                    !Angles.Finite(o.center.x) || !Angles.Finite(o.center.z)) throw new ArgumentException("Invalid obstacle.");
                if (Math.Abs(o.center.x) + o.radius > settings.arenaSize / 2 || Math.Abs(o.center.z) + o.radius > settings.arenaSize / 2)
                    throw new ArgumentException("Obstacle extends outside the arena.");
                foreach (Vec2 v in new[] { start, goal, secondGoal })
                    if (Vec2.Distance(v, o.center) < o.radius + settings.agentRadius + 0.05)
                        throw new ArgumentException("Start or goal overlaps an obstacle.");
            }
        }
    }

    public interface IHeadingController
    {
        double Step(double heading, double reference, double dt);
        double LeftActivity { get; }
        double RightActivity { get; }
    }

    public sealed class ProportionalController : IHeadingController
    {
        readonly SimSettings settings;
        public ProportionalController(SimSettings settings) { this.settings = settings.Copy(); }
        public double LeftActivity { get { return 0; } }
        public double RightActivity { get { return 0; } }
        public double Step(double heading, double reference, double dt)
        { return Angles.Clamp(settings.proportionalGain * Angles.Wrap(reference - heading), -settings.maxTurn, settings.maxTurn); }
    }

    // The published 24-unit functional model. FlyWire edge weights are NOT used here.
    // Array geometry and constants: MaimonLab/ModelLAL.m, commit 0a9d5f64, MIT.
    public sealed class Pfl3Controller : IHeadingController
    {
        public const double A = 29.2282, B = 2.1736, C = -0.7011, D = 0.6299;
        static readonly double[] Left = { 67.5,22.5,-22.5,-22.5,-67.5,-112.5,-112.5,-157.5,157.5,157.5,112.5,67.5 };
        static readonly double[] Right = { -67.5,-112.5,-157.5,-157.5,157.5,112.5,112.5,67.5,22.5,22.5,-22.5,-67.5 };
        readonly double[] state = new double[24];
        readonly SimSettings settings;
        public double LeftActivity { get; private set; }
        public double RightActivity { get; private set; }
        public Pfl3Controller(SimSettings settings) { this.settings = settings.Copy(); }
        public static double Softplus(double x) { return Math.Max(x, 0) + Math.Log(1 + Math.Exp(-Math.Abs(x))); }
        public static double Rate(double h, double g, int i, bool right)
        {
            double theta = right ? Right[i] : Left[i], phi = -(15 + 30 * i);
            return A * Softplus(B * (Math.Cos((h - theta) * Angles.ToRadians) +
                D * Math.Cos((g - phi) * Angles.ToRadians) + C));
        }
        public double Step(double heading, double reference, double dt)
        {
            double alpha = settings.tau == 0 ? 1 : 1 - Math.Exp(-dt / settings.tau);
            LeftActivity = 0; RightActivity = 0;
            for (int i = 0; i < 12; ++i)
            {
                state[i] += alpha * (Rate(heading, reference, i, false) - state[i]);
                state[i + 12] += alpha * (Rate(heading, reference, i, true) - state[i + 12]);
                LeftActivity += state[i]; RightActivity += state[i + 12];
            }
            return Angles.Clamp(settings.pfl3Gain * (RightActivity - LeftActivity), -settings.maxTurn, settings.maxTurn);
        }
    }

    public static class ControllerFactory
    {
        public static IHeadingController Create(ControllerKind kind, SimSettings s)
        { return kind == ControllerKind.Pfl3 ? (IHeadingController)new Pfl3Controller(s) : new ProportionalController(s); }
    }

    public struct StableRandom
    {
        uint state;
        public StableRandom(uint seed) { state = seed == 0 ? 1u : seed; }
        public double Next() { uint x = state; x ^= x << 13; x ^= x >> 17; x ^= x << 5; state = x; return x / 4294967296.0; }
        public double Range(double lo, double hi) { return lo + (hi - lo) * Next(); }
    }

    public static class ScenarioFactory
    {
        public static TrialDefinition Create(ScenarioKind kind, int index, uint seedBase = 1200, string role = "development-pilot")
        {
            uint seed = seedBase + (uint)kind * 1000u + (uint)index;
            StableRandom r = new StableRandom(seed);
            double bearing = r.Range(-180, 180);
            Vec2 along = Angles.Forward(bearing), across = new Vec2(along.z, -along.x);
            TrialDefinition d = new TrialDefinition {
                scenario = (int)kind, index = index, seed = seed, datasetRole = role,
                configurationId = ((char)('A' + (int)kind)).ToString() + "-" + index.ToString("D2", CultureInfo.InvariantCulture),
                start = along * -6.0 + across * r.Range(-0.7, 0.7),
                goal = along * 6.0 + across * r.Range(-0.7, 0.7),
                secondGoal = along * 3.0 + across * 6.0,
                goalChangeSeconds = kind == ScenarioKind.MovingGoal ? 10 : -1
            };
            double error = index == 0 ? 90 : r.Range(-150, 150);
            if (kind == ScenarioKind.Obstacles)
                d.obstacles = new[] {
                    new CircleObstacle(0, along * -1.7 + across * 0.1, 0.85),
                    new CircleObstacle(1, along * 1.5 + across * 0.65, 0.75),
                    new CircleObstacle(2, along * 3.2 + across * -1.2, 0.65)
                };
            if (index == 0 && kind == ScenarioKind.Open) { d.start = new Vec2(-6,-6); d.goal = new Vec2(6,6); }
            if (index == 0 && kind == ScenarioKind.MovingGoal)
            { d.start = new Vec2(0,-6); d.goal = new Vec2(0,3); d.secondGoal = new Vec2(6,6); error = 0; }
            d.initialHeading = Angles.Wrap(Angles.Heading(d.goal - d.start) - error);
            d.Validate(); return d;
        }
    }
}
