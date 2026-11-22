using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NpcLab.Core;
using UnityEngine;

namespace NpcLab.Runtime
{
    public enum LabScreen { Title, Setup, Running, Paused, Results, Instructions, Credits, GuideSetup, Workshop }
    public enum GuideVariant { Pilot, Challenge, Workshop }

    public sealed class LabApp : MonoBehaviour
    {
        public const string AppVersion = "0.3.0-alpha.4";
        public static readonly Rect GuideArena = new Rect(340, 112, 712, 612);
        public static readonly Rect WorkshopArena = new Rect(344, 139, 728, 652);
        public static readonly Rect MenuArena = new Rect(790, 163, 578, 348);
        public static readonly string[] ScenarioNames = { "A · Espacio abierto", "B · Obstáculos fijos", "C · Cambio de meta" };
        public static readonly string[] ControllerNames = { "Proporcional", "PFL3", "Comparación" };

        public LabScreen ScreenState { get; private set; }
        public readonly List<TrialEngine> Engines = new List<TrialEngine>();
        public LabWorld World { get; private set; }
        public TrialDefinition Selected => IsGuided ? guideSelection : freeSelection;
        public bool IsGuided { get; private set; }
        public GuideVariant Variant { get; private set; }
        public ArenaWorkshop Workshop { get; private set; }
        ArenaWorkshop obstacleWorkshop, relayWorkshop;
        public double PeakSeparation { get; private set; }
        public int? Prediction { get; set; }
        public double GuideHeading { get; private set; }
        public TrialEngine[] PreviousGuideTrial { get; private set; }
        public int PlaybackSpeed => IsGuided ? guideSpeed : VisualSpeed;
        public bool ResultsSaved { get; private set; }

        public string Notice = "", ExportFolder = "";
        public int ScenarioIndex, ConfigurationIndex, ControllerSelection = 2, VisualSpeed = 1;
        public bool TopView, Trajectories = true, ReferenceArrows = true;
        public string HeadingInput = "";

        TrialDefinition freeSelection, guideSelection;
        int guideSpeed = 1;
        LabUI ui;
        readonly List<TrialEngine> demo = new List<TrialEngine>();
        double demoTime;

        void Awake()
        {
            Application.runInBackground = true;
            Time.fixedDeltaTime = .02f;
            Time.timeScale = 1;
            World = new LabWorld();
            ui = new LabUI(this);
            obstacleWorkshop = new ArenaWorkshop(Application.persistentDataPath, ScenarioKind.Obstacles);
            relayWorkshop = new ArenaWorkshop(Application.persistentDataPath, ScenarioKind.MovingGoal);
            Workshop = obstacleWorkshop;
            ScreenState = LabScreen.Title;
            LoadSelection();
            StartDemo();
        }

        static TrialDefinition ReadConfiguration(string id)
        {
            TextAsset asset = Resources.Load<TextAsset>("Configurations/" + id);
            if (asset == null) throw new InvalidOperationException("Missing saved configuration " + id);
            TrialDefinition definition = JsonUtility.FromJson<TrialDefinition>(asset.text);
            definition.Validate();
            return definition;
        }

        public void LoadSelection()
        {
            ConfigurationIndex = Mathf.Clamp(ConfigurationIndex, 0, 29);
            ScenarioIndex = Mathf.Clamp(ScenarioIndex, 0, 2);
            string id = (char)('A' + ScenarioIndex) + "-" + ConfigurationIndex.ToString("D2", CultureInfo.InvariantCulture);
            freeSelection = ReadConfiguration(id);
            HeadingInput = freeSelection.initialHeading.ToString("0.###", CultureInfo.InvariantCulture);
            Notice = "";
            Preview();
        }

        public void EnterGuide(ScenarioKind scenario = ScenarioKind.Open, GuideVariant variant = GuideVariant.Challenge)
        {
            Variant = scenario == ScenarioKind.Open ? GuideVariant.Pilot : variant;
            if (Variant == GuideVariant.Workshop) guideSelection = Workshop.Snapshot();
            else if (Variant == GuideVariant.Challenge) guideSelection = ArenaCourses.Challenge(scenario);
            else guideSelection = ReadConfiguration(((char)('A' + (int)scenario)).ToString() + "-00");
            IsGuided = true;
            GuideHeading = guideSelection.initialHeading;
            guideSpeed = 1;
            Engines.Clear();
            PreviousGuideTrial = null;
            ExportFolder = Notice = "";
            ResultsSaved = false;
            PrepareGuide();
        }

        public void PrepareGuide()
        {
            Prediction = null;
            World.OnlyController = null;
            ScreenState = LabScreen.GuideSetup;
            Time.timeScale = 1;
            Preview();
        }

        public void SetGuideHeading(double heading)
        {
            GuideHeading = heading;
            World.SetStartingHeading(Selected.start, heading);
        }

        public void Preview()
        {
            World.Rebuild(Selected, Kinds());
            World.SetTopView(IsGuided || TopView);
            if (IsGuided) World.SetStartingHeading(Selected.start, GuideHeading);
        }

        public void EnterWorkshop(ScenarioKind kind = ScenarioKind.Obstacles)
        {
            Workshop = kind == ScenarioKind.MovingGoal ? relayWorkshop : obstacleWorkshop;
            ChangeScreen(LabScreen.Workshop);
            ui.ResetWorkshopTools();
            RefreshWorkshop();
        }

        public void RefreshWorkshop()
        {
            World.Rebuild(Workshop.Draft, new[] {ControllerKind.Proportional, ControllerKind.Pfl3});
            World.SetTopView(true);
        }

        public void PlayWorkshop()
        {
            EnterGuide(Workshop.Kind, GuideVariant.Workshop);
            StartTrial();
        }

        void StartDemo()
        {
            var d=ArenaCourses.Challenge(ScenarioKind.Obstacles);
            var kinds=new[] { ControllerKind.Proportional, ControllerKind.Pfl3 };
            demo.Clear();
            World.Rebuild(d,kinds);
            World.SetTopView(false);
            foreach(var kind in kinds) { var e=new TrialEngine(d,kind,false); e.Start(); demo.Add(e); }
            demoTime=0;
        }

        void AnimateMenu()
        {
            demoTime += Math.Min(Time.unscaledDeltaTime,.12f)*1.3;
            if (demo[0].Finished && demo[1].Finished)
            {
                if (demoTime > Math.Max(demo[0].Elapsed,demo[1].Elapsed)+2.2) StartDemo();
                return;
            }
            while (Math.Max(demo[0].Elapsed,demo[1].Elapsed)<demoTime && (!demo[0].Finished || !demo[1].Finished))
                foreach(var e in demo) e.Step();
            World.ShowTrails=true; World.ShowReference=false;
            World.Update(demo);
        }

        List<ControllerKind> Kinds()
        {
            if (!IsGuided && ControllerSelection == 0) return new List<ControllerKind> { ControllerKind.Proportional };
            if (!IsGuided && ControllerSelection == 1) return new List<ControllerKind> { ControllerKind.Pfl3 };
            return new List<ControllerKind> { ControllerKind.Proportional, ControllerKind.Pfl3 };
        }

        public void StartTrial()
        {
            try
            {
                TrialDefinition definition = Selected.Copy();
                double heading = GuideHeading;
                if (!IsGuided && (!double.TryParse(HeadingInput.Replace(',', '.'), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out heading) || !Angles.Finite(heading) || heading < -180 || heading > 180))
                {
                    Notice = "El rumbo debe estar entre −180 y 180 grados.";
                    return;
                }
                if (Math.Abs(Angles.Wrap(heading - definition.initialHeading)) > .001)
                {
                    definition.initialHeading = heading;
                    definition.configurationId += "-custom";
                    if (!IsGuided || Variant==GuideVariant.Pilot) definition.datasetRole = "development-custom";
                }
                definition.Validate();
                List<ControllerKind> kinds = Kinds();
                var next = new List<TrialEngine>(kinds.Count);
                foreach (ControllerKind kind in kinds)
                {
                    var engine = new TrialEngine(definition, kind);
                    engine.Start();
                    next.Add(engine);
                }
                World.Rebuild(definition, kinds);
                if (IsGuided && Engines.Count > 0)
                    PreviousGuideTrial = Engines.ToArray();
                Engines.Clear();
                Engines.AddRange(next);
                PeakSeparation=0;
                ResultsSaved = false;
                ExportFolder = Notice = "";
                Time.timeScale = PlaybackSpeed;
                ScreenState = LabScreen.Running;
            }
            catch (Exception ex)
            {
                Notice = "No se pudo iniciar el ensayo. " + ex.Message;
                Debug.LogException(ex);
            }
        }

        void FixedUpdate()
        {
            if (ScreenState != LabScreen.Running) return;
            bool finished = true;
            foreach (TrialEngine engine in Engines)
            {
                engine.Step();
                if (!engine.Finished) finished = false;
            }
            if (Engines.Count==2) PeakSeparation=Math.Max(PeakSeparation,Vec2.Distance(Engines[0].Position,Engines[1].Position));
            if (finished) FinishTrial();
        }

        void Update()
        {
            if (World == null) return;
            float scale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            float offsetX = (Screen.width - 1440 * scale) / 2;
            float offsetY = (Screen.height - 900 * scale) / 2;
            bool menu = ScreenState == LabScreen.Title, editing = ScreenState == LabScreen.Workshop;
            Rect arena = menu ? MenuArena : editing ? WorkshopArena : IsGuided ? GuideArena : new Rect(320, 100, 1100, 610);
            World.Camera.rect = new Rect((offsetX + arena.x * scale) / Screen.width,
                (offsetY + (900 - arena.yMax) * scale) / Screen.height,
                arena.width * scale / Screen.width, arena.height * scale / Screen.height);
            World.Camera.orthographicSize = menu ? 10.6f : editing || IsGuided ? 10.65f : 13.4f;
            if (menu) { AnimateMenu(); return; }
            World.ShowTrails = IsGuided || Trajectories;
            World.ShowReference = !IsGuided && ReferenceArrows;
            if (Engines.Count > 0 && (ScreenState == LabScreen.Running || ScreenState == LabScreen.Paused || ScreenState == LabScreen.Results))
                World.Update(Engines);
        }

        public void ToggleSpeed()
        {
            if (IsGuided) guideSpeed = guideSpeed == 1 ? 4 : 1;
            else VisualSpeed = VisualSpeed == 1 ? 4 : 1;
            if (ScreenState == LabScreen.Running) Time.timeScale = PlaybackSpeed;
        }

        public void Pause()
        {
            foreach (TrialEngine engine in Engines) engine.Pause();
            ScreenState = LabScreen.Paused;
            Time.timeScale = 1;
        }

        public void Resume()
        {
            foreach (TrialEngine engine in Engines) engine.Resume();
            ScreenState = LabScreen.Running;
            Time.timeScale = PlaybackSpeed;
        }

        public void ChangeScreen(LabScreen screen)
        {
            IsGuided = false;
            World.OnlyController = null;
            PreviousGuideTrial = null;
            ScreenState = screen;
            Time.timeScale = 1;
            Notice = "";
            if (screen == LabScreen.Setup) Preview();
            if (screen == LabScreen.Title) StartDemo();
        }

        public void AbortToResults()
        {
            foreach (TrialEngine engine in Engines) engine.Abort();
            FinishTrial();
        }

        void FinishTrial()
        {
            ScreenState = LabScreen.Results;
            Time.timeScale = 1;
            SaveResults();
        }

        public void SaveResults()
        {
            if (ResultsSaved || Engines.Count == 0) return;
            try
            {
                if (string.IsNullOrEmpty(ExportFolder))
                    ExportFolder = Path.Combine(Application.persistentDataPath, "Trials",
                        DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                string environment = "App " + AppVersion + "; Unity " + Application.unityVersion + "; "
                    + (Application.isEditor ? "Editor" : "Player") + "; " + SystemInfo.operatingSystem + "; "
                    + SystemInfo.processorType + "; visual speed " + PlaybackSpeed + "; mode "
                    + (IsGuided && Variant==GuideVariant.Workshop ? "workshop" : IsGuided ? "guided" : "free");
                TrialExport.Write(ExportFolder, Engines, environment);
                ResultsSaved = true;
                Notice = "Resultados guardados.";
            }
            catch (Exception ex)
            {
                Notice = "No se pudo guardar. " + ex.Message;
                Debug.LogException(ex);
            }
        }

        public void OpenResults()
        {
            if (!string.IsNullOrEmpty(ExportFolder)) Application.OpenURL(new Uri(ExportFolder).AbsoluteUri);
        }

        void OnGUI() { if (ui != null) ui.Draw(); }
        void OnApplicationQuit()
        {
            foreach (TrialEngine engine in Engines) if (!engine.Finished) engine.Abort();
            SaveResults();
            Time.timeScale = 1;
        }
        void OnDestroy()
        {
            if (ui != null) ui.Dispose();
            if (World != null) World.Dispose();
        }
        void OnDisable() { Time.timeScale = 1; }
    }
}
