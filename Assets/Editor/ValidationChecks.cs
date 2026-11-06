// This file has no Unity dependency: the same checks run in the editor and .NET harness.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using NpcLab.Core;

namespace NpcLab.Verification
{
    public sealed class CheckReport
    {
        public readonly List<string> Names = new List<string>(), Details = new List<string>();
        public readonly List<bool> Passed = new List<bool>();
        public double MaximumPythonDifference;
        public int Failures { get { int n = 0; foreach (bool p in Passed) if (!p) n++; return n; } }
        public void Add(string name, Action check)
        { Names.Add(name); try { check(); Passed.Add(true); Details.Add("passed"); } catch (Exception e) { Passed.Add(false); Details.Add(e.Message); } }
        public string ToJson()
        {
            StringBuilder b = new StringBuilder("{\n  \"scope\":\"C# core executed; Unity Play mode and Player build are separate checks\",\n  \"coreVersion\":");
            b.Append(JsonText.Quote(TrialEngine.CoreVersion)).Append(",\n  \"failures\":").Append(Failures);
            b.Append(",\n  \"maximumDifferenceFromPython\":").Append(Angles.Number(MaximumPythonDifference));
            b.Append(",\n  \"checks\":[\n");
            for (int i = 0; i < Names.Count; ++i)
            { if (i > 0) b.Append(",\n"); b.Append("    {\"name\":").Append(JsonText.Quote(Names[i])).Append(",\"passed\":").Append(Passed[i] ? "true" : "false").Append(",\"detail\":").Append(JsonText.Quote(Details[i])).Append('}'); }
            return b.Append("\n  ]\n}\n").ToString();
        }
    }

    public static class ValidationChecks
    {
        static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
        static void Close(double a, double b, double tol = 1e-8) { Require(Math.Abs(a - b) <= tol, Angles.Number(a) + " differs from " + Angles.Number(b)); }
        static TrialEngine Complete(TrialDefinition d, ControllerKind k)
        { TrialEngine e = new TrialEngine(d, k, false); e.Start(); while (!e.Finished) e.Step(); return e; }
        static double Parse(string x) { return double.Parse(x, CultureInfo.InvariantCulture); }

        public static CheckReport Run(string root)
        {
            CheckReport r = new CheckReport();
            r.Add("circular_angles", () => { Close(Angles.Wrap(-358), 2); Close(Angles.Wrap(358), -2); Close(Math.Abs(Angles.Wrap(180)),180); });
            r.Add("unity_xz_convention", () => { Close(Angles.Forward(90).x,1); Close(Angles.Forward(0).z,1); Close(Angles.Heading(new Vec2(1,0)),90); });
            r.Add("published_rates_match_python", () => {
                string[] lines = File.ReadAllLines(Path.Combine(root,"Validation/Golden/rates_python.csv"));
                Require(lines.Length > 200, "Missing golden rates.");
                for (int row = 1; row < lines.Length; ++row)
                {
                    string[] c = lines[row].Split(','); double h = Parse(c[0]), g = Parse(c[1]);
                    for (int i = 0; i < 24; ++i)
                    { double diff = Math.Abs(Pfl3Controller.Rate(h,g,i % 12,i >= 12) - Parse(c[i+2])); r.MaximumPythonDifference = Math.Max(r.MaximumPythonDifference,diff); Require(diff < 1e-9,"Rate parity failed."); }
                }
            });
            r.Add("closed_loop_matches_block1_python", () => {
                string[] lines = File.ReadAllLines(Path.Combine(root,"Validation/Golden/angular_python.csv"));
                IHeadingController controller = null; double h = 0; string previous = "";
                for (int row = 1; row < lines.Length; ++row)
                {
                    string[] c = lines[row].Split(','); string initial = c[0];
                    if (initial != previous) { controller = new Pfl3Controller(new SimSettings()); h = -Parse(initial); previous = initial; }
                    double time = Parse(c[1]), goal = Parse(c[3]);
                    Close(h,Parse(c[2]),1e-7);
                    double omega = time >= 10 - 1e-9 ? 0 : controller.Step(h,goal,.02);
                    Close(omega,Parse(c[5]),1e-7); Close(Angles.Wrap(goal-h),Parse(c[4]),1e-7);
                    h = Angles.Wrap(h + .02 * omega);
                }
            });
            r.Add("antipodal_limitation_is_preserved", () => {
                Pfl3Controller c = new Pfl3Controller(new SimSettings()); double h = -180;
                for (int i=0;i<500;++i) h = Angles.Wrap(h + .02 * c.Step(h,0,.02));
                Require(Math.Abs(Angles.Wrap(-h)) > 179,"The antipodal failure was silently altered.");
            });
            r.Add("all_90_configurations_are_valid_and_reproducible", () => {
                for(int s=0;s<3;++s) for(int i=0;i<30;++i)
                { TrialDefinition a=ScenarioFactory.Create((ScenarioKind)s,i),b=ScenarioFactory.Create((ScenarioKind)s,i); a.Validate(); Require(JsonText.Configuration(a)==JsonText.Configuration(b),"Seed replay mismatch."); }
            });
            r.Add("paired_initial_information", () => {
                TrialDefinition d=ScenarioFactory.Create(ScenarioKind.Obstacles,0);
                TrialEngine a=new TrialEngine(d,ControllerKind.Pfl3,false),b=new TrialEngine(d,ControllerKind.Proportional,false);
                a.Start();b.Start();a.Step();b.Step();Close(a.Samples[0].reference,b.Samples[0].reference,0);
                Close(a.Samples[0].x,b.Samples[0].x,0);Close(a.Samples[0].heading,b.Samples[0].heading,0);
            });
            r.Add("pause_does_not_advance_time_or_state", () => {
                TrialEngine e=new TrialEngine(ScenarioFactory.Create(ScenarioKind.Open,0),ControllerKind.Pfl3,false);
                e.Start();e.Step();double t=e.Elapsed,h=e.Heading;e.Pause();for(int i=0;i<50;++i)e.Step();Close(e.Elapsed,t,0);Close(e.Heading,h,0);e.Resume();e.Step();Close(e.Elapsed,t+.02);
            });
            r.Add("first_goal_does_not_finish_scenario_c", () => {
                TrialDefinition d=ScenarioFactory.Create(ScenarioKind.MovingGoal,0);d.start=d.goal;
                TrialEngine e=new TrialEngine(d,ControllerKind.Pfl3,false);e.Start();for(int i=0;i<500;++i)e.Step();
                Require(e.State==TrialState.Running&&!e.GoalSwitched,"Initial goal terminated C.");
                e.Step();Require(e.GoalSwitched,"Goal did not switch at 10 seconds.");
                while(!e.Finished)e.Step();int n=0;foreach(TrialEvent ev in e.Events)if(ev.kind=="goal_switch")n++;
                Require(n==1,"Goal switched more than once.");Require(e.Elapsed>10,"Impossible final arrival time.");
            });
            r.Add("continuous_collision_prevents_tunnelling", () => {
                TrialDefinition d=ScenarioFactory.Create(ScenarioKind.Open,0);d.obstacles=new[]{new CircleObstacle(0,new Vec2(0,0),1)};
                HashSet<int> contacts=new HashSet<int>();Vec2 p=Geometry.Move(new Vec2(-5,0),new Vec2(5,0),d,contacts);
                Require(p.x<=-1.25&&contacts.Contains(0),"Swept collision missed the obstacle.");
            });
            r.Add("replay_is_deterministic_without_timing", () => {
                TrialDefinition d=ScenarioFactory.Create(ScenarioKind.Obstacles,3);
                TrialEngine a=Complete(d,ControllerKind.Pfl3),b=Complete(d,ControllerKind.Pfl3);
                Require(TrialExport.Summary(a)==TrialExport.Summary(b),"Summary replay mismatch.");
                Require(a.Samples.Count==b.Samples.Count,"Sample count replay mismatch.");
                for(int i=0;i<a.Samples.Count;++i){Close(a.Samples[i].x,b.Samples[i].x,0);Close(a.Samples[i].z,b.Samples[i].z,0);}
            });
            r.Add("abort_is_retained_and_arrival_is_null", () => {
                TrialEngine e=new TrialEngine(ScenarioFactory.Create(ScenarioKind.Open,0),ControllerKind.Pfl3,false);e.Start();e.Step();e.Abort();e.Step();
                Require(e.State==TrialState.Aborted&&!e.ArrivalSeconds.HasValue&&e.Samples.Count==1,"Interrupted trial lost.");
            });
            r.Add("invalid_configuration_is_rejected", () => {
                TrialDefinition d=ScenarioFactory.Create(ScenarioKind.Open,0);d.settings.maxSpeed=double.NaN;
                bool rejected=false;try{d.Validate();}catch(ArgumentException){rejected=true;}Require(rejected,"NaN accepted.");
            });
            r.Add("csv_is_invariant_under_spanish_locale", () => {
                CultureInfo previous=CultureInfo.CurrentCulture;string temp=Path.Combine(Path.GetTempPath(),"npclab-check-"+Guid.NewGuid().ToString("N"));
                try {
                    CultureInfo.CurrentCulture=new CultureInfo("es-ES");TrialEngine e=new TrialEngine(ScenarioFactory.Create(ScenarioKind.Open,0),ControllerKind.Pfl3,false);e.Start();e.Step();e.Abort();
                    TrialExport.Write(temp,new[]{e},"verification");string[] rows=File.ReadAllLines(Path.Combine(temp,"trace_Pfl3.csv"));
                    Require(rows[1].Contains("0.02")&&rows[1].Split(',').Length==17,"Locale broke CSV columns.");
                } finally {CultureInfo.CurrentCulture=previous;if(Directory.Exists(temp))Directory.Delete(temp,true);}
            });
            r.Add("showcase_scenarios_reach_final_goal", () => {
                for(int s=0;s<3;++s)foreach(ControllerKind k in new[]{ControllerKind.Proportional,ControllerKind.Pfl3})
                {TrialEngine e=Complete(ScenarioFactory.Create((ScenarioKind)s,0),k);Require(e.State==TrialState.Success,((ScenarioKind)s)+" "+k+" did not reach the goal.");}
            });
            r.Add("fractional_final_step_uses_actual_dt", () => {
                TrialDefinition d=ScenarioFactory.Create(ScenarioKind.Open,0);d.settings.maxSeconds=.025;
                TrialEngine e=Complete(d,ControllerKind.Pfl3);Require(e.State==TrialState.Timeout&&e.Samples.Count==2,"Fractional timeout failed.");
                Close(e.Samples[1].dt,.005);Close(e.Elapsed,.025);Require(!e.ArrivalSeconds.HasValue,"Timeout reported as arrival.");
            });
            r.Add("initial_final_goal_succeeds_without_fake_samples", () => {
                TrialDefinition d=ScenarioFactory.Create(ScenarioKind.Open,0);d.start=d.goal;
                TrialEngine e=Complete(d,ControllerKind.Proportional);Require(e.State==TrialState.Success&&e.Samples.Count==0,"Zero-duration arrival failed.");Close(e.ArrivalSeconds.Value,0);
            });
            r.Add("weighted_errors_recompute_from_trace", () => {
                TrialEngine e=Complete(ScenarioFactory.Create(ScenarioKind.MovingGoal,0),ControllerKind.Pfl3);
                double all=0,t=0,active=0,ta=0,post=0,tp=0;
                foreach(TrialSample s in e.Samples){all+=Math.Abs(s.error)*s.dt;t+=s.dt;if(!s.waiting){active+=Math.Abs(s.error)*s.dt;ta+=s.dt;}if(s.afterSwitch){post+=Math.Abs(s.error)*s.dt;tp+=s.dt;}}
                Close(e.MeanError,all/t);Close(e.ActiveMeanError,active/ta);Close(e.PostSwitchMeanError,post/tp);
            });
            r.Add("far_obstacles_are_not_counted_as_contacts", () => {
                TrialDefinition d=ScenarioFactory.Create(ScenarioKind.Open,0);d.obstacles=new[]{new CircleObstacle(0,new Vec2(-2,0),.5),new CircleObstacle(1,new Vec2(2,0),.5)};
                HashSet<int> contacts=new HashSet<int>();Geometry.Move(new Vec2(-5,0),new Vec2(5,0),d,contacts);
                Require(contacts.Contains(0)&&!contacts.Contains(1),"Unreached obstacle counted as contact.");
            });
            r.Add("timing_disabled_is_missing_not_zero_cost", () => {
                TrialEngine e=Complete(ScenarioFactory.Create(ScenarioKind.Open,0),ControllerKind.Proportional);
                string[] c=TrialExport.Summary(e).Split(',');Require(c[16]==""&&c[17]==""&&c[19]=="false","Disabled timing reported as measured zero.");
            });
            return r;
        }
    }
}
