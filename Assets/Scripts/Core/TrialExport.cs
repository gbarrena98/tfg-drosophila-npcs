using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace NpcLab.Core
{
    public static class JsonText
    {
        public static string Quote(string value)
        {
            StringBuilder b = new StringBuilder("\"");
            foreach (char c in value ?? "")
                switch (c) {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default: if (c < 32) b.Append("\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture)); else b.Append(c); break;
                }
            return b.Append('"').ToString();
        }
        public static string Vec(Vec2 v) { return "{\"x\":" + Angles.Number(v.x) + ",\"z\":" + Angles.Number(v.z) + "}"; }
        public static string Configuration(TrialDefinition d)
        {
            SimSettings s = d.settings;
            StringBuilder b = new StringBuilder();
            b.Append("{\n  \"schemaVersion\":\"1\",\n  \"configurationId\":").Append(Quote(d.configurationId));
            b.Append(",\n  \"datasetRole\":").Append(Quote(d.datasetRole));
            b.Append(",\n  \"scenario\":").Append(d.scenario).Append(",\n  \"index\":").Append(d.index);
            b.Append(",\n  \"seed\":").Append(d.seed.ToString(CultureInfo.InvariantCulture));
            b.Append(",\n  \"start\":").Append(Vec(d.start)).Append(",\n  \"goal\":").Append(Vec(d.goal));
            b.Append(",\n  \"secondGoal\":").Append(Vec(d.secondGoal));
            b.Append(",\n  \"initialHeading\":").Append(Angles.Number(d.initialHeading));
            b.Append(",\n  \"goalChangeSeconds\":").Append(Angles.Number(d.goalChangeSeconds));
            b.Append(",\n  \"settings\":{");
            string[] names = { "dt","arenaSize","maxSeconds","maxSpeed","maxTurn","arrivalRadius","agentRadius",
                "proportionalGain","pfl3Gain","tau","lookAhead","clearance","reorientDegrees","reorientDwell" };
            double[] values = { s.dt,s.arenaSize,s.maxSeconds,s.maxSpeed,s.maxTurn,s.arrivalRadius,s.agentRadius,
                s.proportionalGain,s.pfl3Gain,s.tau,s.lookAhead,s.clearance,s.reorientDegrees,s.reorientDwell };
            for (int i = 0; i < names.Length; ++i) { if (i > 0) b.Append(','); b.Append(Quote(names[i])).Append(':').Append(Angles.Number(values[i])); }
            b.Append("},\n  \"obstacles\":[");
            for (int i = 0; i < d.obstacles.Length; ++i)
            {
                CircleObstacle o = d.obstacles[i]; if (i > 0) b.Append(',');
                b.Append("{\"id\":").Append(o.id).Append(",\"center\":").Append(Vec(o.center));
                b.Append(",\"radius\":").Append(Angles.Number(o.radius)).Append('}');
            }
            return b.Append("]\n}\n").ToString();
        }
    }

    public static class TrialExport
    {
        static string Csv(string text) { return "\"" + (text ?? "").Replace("\"", "\"\"") + "\""; }
        static string N(double n) { return Angles.Number(n); }
        static string Nullable(double? n) { return n.HasValue ? N(n.Value) : ""; }
        public const string SummaryHeader = "core_version,configuration_id,dataset_role,scenario,seed,controller,state,elapsed_s,arrival_s,route_m,collisions,mae_reference_deg,mae_active_deg,mae_before_switch_deg,mae_after_switch_deg,reorientation_s,controller_mean_ms,controller_p95_ms,control_samples,controller_timing_enabled,flywire_weights_used";
        public static string Summary(TrialEngine e)
        {
            return string.Join(",", new[] { Csv(TrialEngine.CoreVersion), Csv(e.Definition.configurationId), Csv(e.Definition.datasetRole),
                e.Definition.scenario.ToString(CultureInfo.InvariantCulture), e.Definition.seed.ToString(CultureInfo.InvariantCulture),
                Csv(e.Kind.ToString()), Csv(e.State.ToString()), N(e.Elapsed), Nullable(e.ArrivalSeconds), N(e.RouteLength),
                e.Collisions.ToString(CultureInfo.InvariantCulture), e.Samples.Count > 0 ? N(e.MeanError) : "", e.ActiveSeconds > 0 ? N(e.ActiveMeanError) : "",
                e.BeforeSwitchSeconds > 0 ? N(e.PreSwitchMeanError) : "", e.AfterSwitchSeconds > 0 ? N(e.PostSwitchMeanError) : "", Nullable(e.ReorientationSeconds),
                e.TimingEnabled && e.Samples.Count > 0 ? N(e.MeanControllerMs) : "", e.TimingEnabled && e.Samples.Count > 0 ? N(e.P95ControllerMs) : "",
                e.Samples.Count.ToString(CultureInfo.InvariantCulture), e.TimingEnabled ? "true" : "false", "false" });
        }
        public static void Write(string folder, IList<TrialEngine> trials, string environment)
        {
            if (trials == null || trials.Count == 0) throw new ArgumentException("No trials to export.");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "configuration.json"), JsonText.Configuration(trials[0].Definition), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "session.json"), "{\"coreVersion\":" + JsonText.Quote(TrialEngine.CoreVersion) +
                ",\"createdUtc\":" + JsonText.Quote(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)) +
                ",\"environment\":" + JsonText.Quote(environment) +
                ",\"controllerTimingEnabled\":" + (trials[0].TimingEnabled ? "true" : "false") +
                ",\"model\":\"published_PFL3_24_unit_functional_model\",\"flywireWeightsUsed\":false,\"finalResearchEvaluation\":false}\n", new UTF8Encoding(false));
            using (StreamWriter summary = new StreamWriter(Path.Combine(folder, "summary.csv"), false, new UTF8Encoding(false)))
            {
                summary.WriteLine(SummaryHeader);
                foreach (TrialEngine e in trials)
                {
                    summary.WriteLine(Summary(e));
                    using (StreamWriter w = new StreamWriter(Path.Combine(folder, "trace_" + e.Kind + ".csv"), false, new UTF8Encoding(false)))
                    {
                        w.WriteLine("time_s,dt_s,x_m,z_m,heading_deg,goal_x_m,goal_z_m,reference_deg,error_reference_deg,error_goal_deg,omega_deg_s,speed_m_s,controller_ms,left_activity,right_activity,after_switch,waiting_for_switch");
                        foreach (TrialSample s in e.Samples)
                            w.WriteLine(string.Join(",", new[] { N(s.time),N(s.dt),N(s.x),N(s.z),N(s.heading),N(s.goalX),N(s.goalZ),
                                N(s.reference),N(s.error),N(s.goalError),N(s.omega),N(s.speed),N(s.controllerMs),N(s.leftActivity),N(s.rightActivity),
                                s.afterSwitch ? "true" : "false", s.waiting ? "true" : "false" }));
                    }
                    using (StreamWriter w = new StreamWriter(Path.Combine(folder, "events_" + e.Kind + ".csv"), false, new UTF8Encoding(false)))
                    { w.WriteLine("time_s,event"); foreach (TrialEvent ev in e.Events) w.WriteLine(N(ev.time) + "," + Csv(ev.kind)); }
                }
            }
        }
    }
}
