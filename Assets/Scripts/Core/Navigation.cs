using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace NpcLab.Core
{
    public static class Geometry
    {
        // Swept point versus an expanded disc. Boundary motion away from the disc is free.
        public static bool SegmentCircle(Vec2 start, Vec2 end, Vec2 center, double radius, out double fraction)
        {
            Vec2 m = start - center, v = end - start;
            double a = Vec2.Dot(v, v), b = Vec2.Dot(m, v), c = Vec2.Dot(m, m) - radius * radius;
            fraction = 1;
            if (a < 1e-20) return false;
            if (c <= 1e-9) { if (b >= 0) return false; fraction = 0; return true; }
            double disc = b * b - a * c;
            if (disc < 0 || b >= 0) return false;
            double t = (-b - Math.Sqrt(disc)) / a;
            if (t < 0 || t > 1) return false;
            fraction = t; return true;
        }
        public static Vec2 Move(Vec2 from, Vec2 to, TrialDefinition d, HashSet<int> contacts)
        {
            double earliest = 1;
            foreach (CircleObstacle o in d.obstacles)
            {
                double hit;
                if (SegmentCircle(from, to, o.center, o.radius + d.settings.agentRadius, out hit))
                    earliest = Math.Min(earliest, hit);
            }
            double safeFraction = Math.Max(0, earliest - (earliest < 1 ? 1e-7 / Math.Max(1e-12, Vec2.Distance(from, to)) : 0));
            Vec2 result = from + (to - from) * safeFraction;
            double h = d.settings.arenaSize / 2 - d.settings.agentRadius;
            if (result.x < -h) { result.x = -h; contacts.Add(-1); }
            if (result.x > h) { result.x = h; contacts.Add(-2); }
            if (result.z < -h) { result.z = -h; contacts.Add(-3); }
            if (result.z > h) { result.z = h; contacts.Add(-4); }
            // Preserve contact identity while tangent/blocked, avoiding one collision per tick.
            foreach (CircleObstacle o in d.obstacles)
                if (Vec2.Distance(result, o.center) <= o.radius + d.settings.agentRadius + 1e-5) contacts.Add(o.id);
            if (Math.Abs(result.x + h) < 1e-8) contacts.Add(-1);
            if (Math.Abs(result.x - h) < 1e-8) contacts.Add(-2);
            if (Math.Abs(result.z + h) < 1e-8) contacts.Add(-3);
            if (Math.Abs(result.z - h) < 1e-8) contacts.Add(-4);
            return result;
        }
    }

    // The same geometric local-avoidance algorithm and parameters are used in both arms.
    public sealed class SharedNavigator
    {
        int tracked = -1, turnSide = 1;
        public double MinimumClearance { get; private set; }
        public double Reference(Vec2 position, double heading, Vec2 goal, TrialDefinition d)
        {
            Vec2 delta = goal - position;
            if (delta.Length < 1e-10) return heading;
            Vec2 direction = delta.Normalized;
            CircleObstacle threat = null;
            double nearest = double.MaxValue;
            MinimumClearance = d.settings.lookAhead;
            foreach (CircleObstacle o in d.obstacles)
            {
                double distance = Vec2.Distance(position, o.center);
                MinimumClearance = Math.Min(MinimumClearance, Math.Max(0, distance - o.radius - d.settings.agentRadius));
                double t;
                Vec2 end = position + direction * Math.Min(delta.Length, d.settings.lookAhead);
                if (Geometry.SegmentCircle(position, end, o.center,
                    o.radius + d.settings.agentRadius + d.settings.clearance, out t) && t < nearest)
                { threat = o; nearest = t; }
            }
            if (threat == null) { tracked = -1; return Angles.Heading(delta); }
            Vec2 toward = threat.center - position;
            double desired = Angles.Heading(delta), centerHeading = Angles.Heading(toward);
            double radius = threat.radius + d.settings.agentRadius + d.settings.clearance;
            double offset = Math.Asin(Angles.Clamp(radius / Math.Max(radius, toward.Length), 0, 1)) / Angles.ToRadians + 8;
            if (tracked != threat.id)
            {
                double left = Angles.Wrap(centerHeading - offset), right = Angles.Wrap(centerHeading + offset);
                double l = Math.Abs(Angles.Wrap(left - heading)) + 0.25 * Math.Abs(Angles.Wrap(left - desired));
                double r = Math.Abs(Angles.Wrap(right - heading)) + 0.25 * Math.Abs(Angles.Wrap(right - desired));
                turnSide = r <= l ? 1 : -1; tracked = threat.id;
            }
            return Angles.Wrap(centerHeading + turnSide * offset);
        }
    }

    public sealed class TrialSample
    {
        public double time, dt, x, z, heading, goalX, goalZ, reference, error, goalError;
        public double omega, speed, controllerMs, leftActivity, rightActivity;
        public bool afterSwitch, waiting;
    }
    public sealed class TrialEvent
    {
        public double time;
        public string kind;
        public TrialEvent(double time, string kind) { this.time = time; this.kind = kind; }
    }

    public sealed class TrialEngine
    {
        public const string CoreVersion = "0.2.0-pec2";
        public readonly TrialDefinition Definition;
        public readonly ControllerKind Kind;
        public readonly List<TrialSample> Samples = new List<TrialSample>();
        public readonly List<TrialEvent> Events = new List<TrialEvent>();
        public TrialState State { get; private set; }
        public Vec2 Position { get; private set; }
        public Vec2 Goal { get; private set; }
        public double Heading { get; private set; }
        public double Elapsed { get; private set; }
        public double ReferenceHeading { get; private set; }
        public double RouteLength { get; private set; }
        public int Collisions { get; private set; }
        public bool GoalSwitched { get; private set; }
        public double? ReorientationSeconds { get; private set; }
        public double? ArrivalSeconds { get { return State == TrialState.Success ? (double?)Elapsed : null; } }
        public bool TimingEnabled { get { return measureTime; } }
        public double LeftActivity { get { return controller.LeftActivity; } }
        public double RightActivity { get { return controller.RightActivity; } }
        public double MeanError { get { return weightedTime == 0 ? 0 : integratedError / weightedTime; } }
        public double ActiveMeanError { get { return activeTime == 0 ? 0 : activeError / activeTime; } }
        public double PreSwitchMeanError { get { return beforeTime == 0 ? 0 : beforeError / beforeTime; } }
        public double PostSwitchMeanError { get { return afterTime == 0 ? 0 : afterError / afterTime; } }
        public double ActiveSeconds { get { return activeTime; } }
        public double BeforeSwitchSeconds { get { return beforeTime; } }
        public double AfterSwitchSeconds { get { return afterTime; } }
        public bool Finished { get { return State == TrialState.Success || State == TrialState.Timeout || State == TrialState.Aborted; } }
        readonly IHeadingController controller;
        readonly SharedNavigator navigator = new SharedNavigator();
        readonly HashSet<int> previousContacts = new HashSet<int>();
        readonly bool measureTime;
        int steps;
        double integratedError, weightedTime, activeError, activeTime, beforeError, beforeTime, afterError, afterTime;
        double inBandSince = double.NaN;

        public TrialEngine(TrialDefinition definition, ControllerKind kind, bool measureTime = true)
        {
            definition.Validate(); Definition = definition.Copy(); Kind = kind; this.measureTime = measureTime;
            Position = Definition.start; Heading = Definition.initialHeading; Goal = Definition.goal;
            ReferenceHeading = Heading; State = TrialState.Ready;
            // Warm a different instance so JIT/cache warm-up never advances the live state.
            IHeadingController warm = ControllerFactory.Create(kind, Definition.settings);
            for (int i = 0; i < 32; ++i) warm.Step(20, 60, Definition.settings.dt);
            controller = ControllerFactory.Create(kind, Definition.settings);
        }
        public void Start()
        {
            if (State != TrialState.Ready) throw new InvalidOperationException("Trial already started.");
            State = TrialState.Running; Events.Add(new TrialEvent(Elapsed, "start"));
            if (Definition.scenario != 2 && Vec2.Distance(Position, Goal) <= Definition.settings.arrivalRadius)
            { State = TrialState.Success; Events.Add(new TrialEvent(0, "success")); }
        }
        public void Pause() { if (State == TrialState.Running) { State = TrialState.Paused; Events.Add(new TrialEvent(Elapsed, "pause")); } }
        public void Resume() { if (State == TrialState.Paused) { State = TrialState.Running; Events.Add(new TrialEvent(Elapsed, "resume")); } }
        public void Abort() { if (!Finished) { State = TrialState.Aborted; Events.Add(new TrialEvent(Elapsed, "abort")); } }

        public void Step()
        {
            if (State != TrialState.Running) return;
            SimSettings s = Definition.settings;
            double time = steps * s.dt;
            double dt = Math.Min(s.dt, s.maxSeconds - time);
            if (dt <= 1e-12) { State = TrialState.Timeout; return; }
            if (Definition.scenario == 2 && !GoalSwitched && time + 1e-10 >= Definition.goalChangeSeconds)
            { Goal = Definition.secondGoal; GoalSwitched = true; Events.Add(new TrialEvent(time, "goal_switch")); }
            bool waiting = Definition.scenario == 2 && !GoalSwitched && Vec2.Distance(Position, Goal) <= s.arrivalRadius;
            double direct = Vec2.Distance(Position, Goal) < 1e-10 ? Heading : Angles.Heading(Goal - Position);
            ReferenceHeading = waiting ? Heading : navigator.Reference(Position, Heading, Goal, Definition);
            double error = Angles.Wrap(ReferenceHeading - Heading);
            long begin = measureTime ? Stopwatch.GetTimestamp() : 0;
            double omega = controller.Step(Heading, ReferenceHeading, dt);
            double milliseconds = measureTime ? (Stopwatch.GetTimestamp() - begin) * 1000.0 / Stopwatch.Frequency : 0;
            double speed = waiting ? 0 : s.maxSpeed * (0.15 + 0.85 * Math.Max(0, Math.Cos(error * Angles.ToRadians)));
            TrialSample sample = new TrialSample {
                time = time, dt = dt, x = Position.x, z = Position.z, heading = Heading,
                goalX = Goal.x, goalZ = Goal.z, reference = ReferenceHeading, error = error,
                goalError = Angles.Wrap(direct - Heading), omega = omega, speed = speed,
                controllerMs = milliseconds, afterSwitch = GoalSwitched, waiting = waiting,
                leftActivity = controller.LeftActivity, rightActivity = controller.RightActivity
            };
            Samples.Add(sample);
            integratedError += Math.Abs(error) * dt; weightedTime += dt;
            if (!waiting) { activeError += Math.Abs(error) * dt; activeTime += dt; }
            if (GoalSwitched) { afterError += Math.Abs(error) * dt; afterTime += dt; }
            else { beforeError += Math.Abs(error) * dt; beforeTime += dt; }
            if (GoalSwitched && !ReorientationSeconds.HasValue)
            {
                if (Math.Abs(error) < s.reorientDegrees)
                {
                    if (double.IsNaN(inBandSince)) inBandSince = time;
                    if (time + dt - inBandSince + 1e-10 >= s.reorientDwell)
                        ReorientationSeconds = Math.Max(0, inBandSince - Definition.goalChangeSeconds);
                }
                else inBandSince = double.NaN;
            }
            Heading = Angles.Wrap(Heading + dt * omega);
            HashSet<int> contacts = new HashSet<int>();
            Vec2 next = Geometry.Move(Position, Position + Angles.Forward(Heading) * (speed * dt), Definition, contacts);
            foreach (int id in contacts) if (!previousContacts.Contains(id)) Collisions++;
            previousContacts.Clear(); previousContacts.UnionWith(contacts);
            RouteLength += Vec2.Distance(Position, next); Position = next;
            steps++; Elapsed = Math.Min(steps * s.dt, s.maxSeconds);
            if (!Angles.Finite(Heading) || !Angles.Finite(Position.x) || !Angles.Finite(Position.z))
                throw new ArithmeticException("Non-finite simulation state.");
            bool finalGoal = Definition.scenario != 2 || GoalSwitched;
            if (finalGoal && Elapsed < s.maxSeconds && Vec2.Distance(Position, Goal) <= s.arrivalRadius)
            { State = TrialState.Success; Events.Add(new TrialEvent(Elapsed, "success")); }
            else if (Elapsed + 1e-10 >= s.maxSeconds)
            { State = TrialState.Timeout; Events.Add(new TrialEvent(Elapsed, "timeout")); }
        }

        public double MeanControllerMs
        { get { if (Samples.Count == 0) return 0; double sum = 0; foreach (TrialSample s in Samples) sum += s.controllerMs; return sum / Samples.Count; } }
        public double P95ControllerMs
        {
            get {
                if (Samples.Count == 0) return 0;
                List<double> v = new List<double>(); foreach (TrialSample s in Samples) v.Add(s.controllerMs); v.Sort();
                double q = 0.95 * (v.Count - 1); int lo = (int)Math.Floor(q), hi = (int)Math.Ceiling(q);
                return v[lo] + (q - lo) * (v[hi] - v[lo]);
            }
        }
    }
}
