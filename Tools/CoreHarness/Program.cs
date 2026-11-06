using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NpcLab.Core;
using NpcLab.Verification;

internal static class Program
{
    static int Main(string[] args)
    {
        try {
            string command = args.Length == 0 ? "check" : args[0];
            string root = Directory.GetCurrentDirectory();
            for (int i = 1; i + 1 < args.Length; ++i) if (args[i] == "--root") root = Path.GetFullPath(args[++i]);
            if (command == "check")
            {
                CheckReport report = ValidationChecks.Run(root);
                string path = Path.Combine(root, "Validation", "Executed", "core_checks.json");
                Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, report.ToJson());
                Console.WriteLine(report.ToJson()); return report.Failures == 0 ? 0 : 1;
            }
            if (command == "configurations")
            {
                string folder = Path.Combine(root, "Assets", "Resources", "Configurations"); Directory.CreateDirectory(folder);
                for (int scenario = 0; scenario < 3; ++scenario) for (int i = 0; i < 30; ++i)
                {
                    TrialDefinition d = ScenarioFactory.Create((ScenarioKind)scenario, i);
                    File.WriteAllText(Path.Combine(folder, d.configurationId + ".json"), JsonText.Configuration(d));
                }
                Console.WriteLine("90 development configurations written. They are not a frozen final study dataset."); return 0;
            }
            if (command == "pilot")
            {
                string folder = Path.Combine(root, "Validation", "Executed"); Directory.CreateDirectory(folder);
                using (StreamWriter w = new StreamWriter(Path.Combine(folder, "development_pilot.csv")))
                {
                    w.WriteLine(TrialExport.SummaryHeader);
                    for (int scenario = 0; scenario < 3; ++scenario)
                    {
                        int successes = 0;
                        for (int i = 0; i < 30; ++i)
                        {
                            List<TrialEngine> pair = new List<TrialEngine>();
                            foreach (ControllerKind kind in new[] { ControllerKind.Proportional, ControllerKind.Pfl3 })
                            {
                                TrialEngine e = new TrialEngine(ScenarioFactory.Create((ScenarioKind)scenario, i), kind, false); e.Start();
                                while (!e.Finished) e.Step();
                                w.WriteLine(TrialExport.Summary(e)); if (e.State == TrialState.Success) successes++;
                                pair.Add(e);
                            }
                            if (i == 0) TrialExport.Write(Path.Combine(folder, "example_" + scenario), pair, "Development core harness; timing disabled; not Unity Player.");
                        }
                        Console.WriteLine(((ScenarioKind)scenario).ToString() + " successful: " + successes + "/60 (development pilot)");
                    }
                }
                return 0;
            }
            Console.Error.WriteLine("Commands: check | configurations | pilot [--root PROJECT_FOLDER]"); return 2;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); return 1; }
    }
}
