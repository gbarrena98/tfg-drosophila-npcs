using System;
using NpcLab.Core;
using UnityEngine;

namespace NpcLab.Runtime
{
    public sealed partial class LabUI
    {
        ScenarioKind Scenario => (ScenarioKind)app.Selected.scenario;
        double Bearing => Angles.Heading(app.Selected.goal - app.Selected.start);
        double InitialError => Math.Abs(Angles.Wrap(Bearing - app.GuideHeading));
        bool Opposite => Math.Abs(InitialError - 180) < .000001;
        static string Degrees(double angle) => F(Math.Abs(angle) < .05 ? 0 : angle, "0.#").Replace("-", "−") + "°";

        static string Arrival(TrialEngine engine) =>
            engine.ArrivalSeconds.HasValue ? F(engine.ArrivalSeconds.Value) + " s" : State(engine.State);

        static bool WaitingAtGoal(TrialEngine engine) =>
            !engine.GoalSwitched && Vec2.Distance(engine.Position, engine.Definition.goal) <= engine.Definition.settings.arrivalRadius;

        static string GoalProgress(TrialEngine engine)
        {
            if (engine.GoalSwitched) return "Meta 2 activa desde " + F(engine.Definition.goalChangeSeconds, "0.#") + " s";
            if (WaitingAtGoal(engine)) return "En la meta 1 · esperando el cambio";
            return "Meta 1 activa · cambio a los " + F(engine.Definition.goalChangeSeconds, "0.#") + " s";
        }

        void DrawGuide()
        {
            Header((Scenario == ScenarioKind.Open ? "Primer ensayo" : "Ensayo guiado") + "   /   "
                + app.Selected.configurationId + " · " + LabApp.ScenarioNames[app.Selected.scenario].Substring(4));
            LabScreen screen = app.ScreenState;
            bool preparing = screen == LabScreen.GuideSetup;
            Surface(new Rect(24, 102, 300, 776), panel, edge);
            Surface(new Rect(1068, 102, 348, 622), panel, edge);
            if (preparing) DrawGuideSetup();
            else DrawGuideControls();
            if (!app.IsGuided || app.ScreenState != screen) return;

            Rect arena = LabApp.GuideArena;
            Box(new Rect(arena.x, arena.y - 5, arena.width, 1), edge);
            Box(new Rect(arena.x, arena.yMax + 3, arena.width, 1), edge);
            DrawGuideMap(preparing);
            DrawObservations(preparing);

            Surface(new Rect(340, 743, 1076, 135), panel, edge);
            if (preparing)
            {
                Text("Antes de salir", 362, 761, 335, 41, heading);
                string help = "La flecha blanca indica el rumbo de salida. Los dos NPC reciben la dirección de la meta desde el primer instante.";
                if (Scenario == ScenarioKind.Obstacles)
                    help = "Los discos numerados son obstáculos. Fíjate en el punto en que cada trayectoria se desvía de la línea hacia la meta.";
                else if (Scenario == ScenarioKind.MovingGoal)
                    help = "El círculo verde identifica la meta activa. Si llegan pronto a la primera, esperan allí hasta que se active la segunda.";
                Text(help, 363, 809, 1008, 56);
            }
            else DrawGuideTimes();
        }

        void DrawGuideSetup()
        {
            Text("Rumbo de salida", 45, 127, 268, 43, heading);
            Text(app.Engines.Count > 0 ? "Cambia el rumbo y compara con el intento anterior." :
                "Elige hacia dónde miran los dos NPC al empezar.", 46, 184, 252, 98);
            HeadingChoice("De lado", Angles.Wrap(Bearing - 90), 334);
            HeadingChoice("Hacia la meta", Bearing, 403);
            HeadingChoice("De espaldas", Angles.Wrap(Bearing - 180), 472);
            if (Scenario == ScenarioKind.Open) HeadingChoice("Casi de espaldas", Angles.Wrap(Bearing - 179), 541);

            if (Button(app.Engines.Count > 0 ? "Lanzar de nuevo" : "Iniciar comparación", 45, 648, 257, 54, true))
                app.StartTrial();
            if (Button("Volver al menú", 45, 717, 257, 46)) app.ChangeScreen(LabScreen.Title);
            if (!string.IsNullOrEmpty(app.Notice) && !app.ResultsSaved) Text(app.Notice, 46, 780, 252, 81, small);
        }

        void HeadingChoice(string label, double angle, float y)
        {
            bool selected = Math.Abs(Angles.Wrap(app.GuideHeading - angle)) < .001;
            if (Button(label + "    " + Degrees(angle), 45, y, 257, 50, selected)) app.SetGuideHeading(angle);
            if (selected) Rounded(new Rect(33, y + 10, 4, 30), LabWorld.Cyan, 2);
        }

        void DrawGuideControls()
        {
            bool results = app.ScreenState == LabScreen.Results;
            bool paused = app.ScreenState == LabScreen.Paused;
            Text(results ? "Resultados" : paused ? "En pausa" : "En marcha", 45, 127, 263, 45, heading);
            Text("Rumbo de salida", 46, 212, 246, 30, small);
            Text(Degrees(app.GuideHeading), 43, 242, 253, 68, title);
            Text("Tiempo simulado", 46, 331, 248, 28, small);
            double time = Math.Max(app.Engines[0].Elapsed, app.Engines[1].Elapsed);
            Text(F(time, "0.0") + " / " + F(app.Selected.settings.maxSeconds, "0") + " s", 46, 363, 250, 43, heading);
            if (results)
            {
                string hint = Scenario == ScenarioKind.Open && Opposite && app.Engines[1].State == TrialState.Timeout
                    ? "Prueba «Casi de espaldas» para ver qué cambia con un solo grado."
                    : "Puedes comparar con otro rumbo o repetir las mismas condiciones.";
                Text(hint, 46, 432, 252, 88);
                if (Button("Cambiar rumbo y repetir", 45, 531, 257, 52, true)) app.PrepareGuide();
                if (Button("Repetir el mismo rumbo", 45, 595, 257, 44)) app.StartTrial();
                if (Button("Configuración libre", 45, 652, 257, 44)) app.ChangeScreen(LabScreen.Setup);
                if (Button(app.ResultsSaved ? "Abrir carpeta CSV" : "Reintentar guardado", 45, 709, 257, 44))
                {
                    if (app.ResultsSaved) app.OpenResults();
                    else app.SaveResults();
                }
                if (Button("Menú principal", 45, 766, 257, 42)) app.ChangeScreen(LabScreen.Title);
                Text(app.Notice, 46, 824, 251, 48, small);
            }
            else
            {
                Text("Puedes pausar y alternar entre los dos recorridos para compararlos.", 46, 432, 249, 92);
                if (Button(paused ? "Continuar" : "Pausar", 45, 551, 257, 52, true))
                {
                    if (paused) app.Resume();
                    else app.Pause();
                }
                if (Button(app.PlaybackSpeed == 1 ? "Acelerar a 4×" : "Volver a 1×", 45, 616, 257, 46)) app.ToggleSpeed();
                if (Button("Detener y ver resultados", 45, 675, 257, 46)) app.AbortToResults();
            }
        }

        void DrawObservations(bool preparing)
        {
            Text("Qué observar", 1090, 124, 302, 45, heading);
            string help;
            switch (Scenario)
            {
                case ScenarioKind.Obstacles:
                    help = "Al detectar un obstáculo en su camino, cada NPC calcula un desvío local. Ambos usan la misma regla para rodearlo.";
                    break;
                case ScenarioKind.MovingGoal:
                    help = "Cuando la meta pasa del punto 1 al 2, observa cuándo vuelven a orientar el giro hacia el nuevo objetivo.";
                    break;
                default:
                    help = Opposite
                        ? "La meta queda exactamente a 180°. El PFL3 recibe su dirección, pero sus señales izquierda y derecha se compensan y apenas gira."
                        : InitialError > 178.9 && InitialError < 179.1
                            ? "Ahora la meta queda a 179°. Ese grado de diferencia rompe el equilibrio entre las señales izquierda y derecha del PFL3."
                            : "El proporcional gira según el ángulo que falta. El PFL3 convierte la diferencia entre sus señales derecha e izquierda en una orden de giro.";
                    break;
            }
            Text(help, 1090, 190, 296, 150);
            Box(new Rect(1090, 347, 300, 1), edge);
            if (preparing)
            {
                Text("Dirección inicial a la meta", 1090, 378, 299, 31, small);
                Text(Degrees(Bearing), 1090, 411, 299, 41, heading);
                Text("Ángulo que deben corregir", 1090, 469, 299, 30, small);
                Text(Degrees(InitialError), 1090, 502, 299, 41, heading);
            }
            else if (Scenario == ScenarioKind.Open)
            {
                TrialEngine pfl3 = app.Engines[1];
                Text("Actividad PFL3", 1090, 370, 300, 39, heading);
                double scale = Math.Max(1, Math.Max(pfl3.LeftActivity, pfl3.RightActivity));
                ActivityBar("Izquierda", pfl3.LeftActivity, scale, 421);
                ActivityBar("Derecha", pfl3.RightActivity, scale, 473);
                double turn = pfl3.Samples.Count == 0 ? 0 : pfl3.Samples[pfl3.Samples.Count - 1].omega;
                Text("Orden de giro   " + Degrees(turn) + "/s", 1090, 536, 304, 34, readout);
            }
            else if (Scenario == ScenarioKind.Obstacles)
            {
                Text(app.ScreenState == LabScreen.Results ? "Contactos" : "Orientación", 1090, 370, 300, 42, heading);
                for (int i = 0; i < app.Engines.Count; i++)
                {
                    TrialEngine e = app.Engines[i];
                    string value = e.Collisions.ToString();
                    if (app.ScreenState != LabScreen.Results)
                    {
                        if (e.Finished) value = State(e.State);
                        else
                        {
                            TrialSample last = e.Samples.Count == 0 ? null : e.Samples[e.Samples.Count - 1];
                            bool detour = last != null && Math.Abs(Angles.Wrap(last.error - last.goalError)) > .0001;
                            value = detour ? "Rodeando" : "Hacia la meta";
                        }
                    }
                    Marker(new Vector2(1099, 438 + 60*i), e.Kind);
                    Text(e.Kind == ControllerKind.Pfl3 ? "PFL3" : "Proporcional", 1117, 424+60*i, 132, 35, small);
                    Text(value, 1249, 424+60*i, 142, 41, body);
                }
            }
            else
            {
                TrialEngine e = app.Engines[0];
                Text("Meta activa", 1090, 370, 300, 42, heading);
                Text(e.GoalSwitched ? "02" : "01", 1089, 414, 128, 68, title);
                Text(e.GoalSwitched ? "Segunda meta" : "Primera meta", 1192, 434, 197, 40);
                string phase = e.GoalSwitched ? "Cambio a los " + F(e.Definition.goalChangeSeconds, "0.#") + " s"
                    : "Cambio en " + F(Math.Max(0, e.Definition.goalChangeSeconds - e.Elapsed), "0.0") + " s";
                Text(phase, 1090, 504, 302, 39, readout);
            }
            Box(new Rect(1090, 580, 300, 1), edge);
            Text(preparing ? "Controladores" : "Mostrar en la arena", 1090, 597, 300, 32, small);
            if (!preparing) DrawFilter(1090, 631, 303);
            Marker(new Vector2(1100, 691), ControllerKind.Proportional);
            Text("Proporcional", 1117, 677, 123, 31, small);
            Marker(new Vector2(1261, 691), ControllerKind.Pfl3);
            Text("PFL3", 1277, 677, 101, 31, small);
        }

        void ActivityBar(string label, double value, double scale, float y)
        {
            Text(label, 1090, y, 170, 27, small);
            Text(F(value, "0.0"), 1283, y - 1, 110, 29, readout);
            Rounded(new Rect(1090,y+28,300,5),edge,2);
            float w = 300 * Mathf.Clamp01((float)(value / scale));
            if (w > 0) Rounded(new Rect(1090,y+28,w,5),LabWorld.Cyan,2);
        }

        void DrawFilter(float x, float y, float width)
        {
            float a = width*.235f, b = width*.45f;
            if (Button("Ambos", x, y, a, 36, !app.World.OnlyController.HasValue)) app.World.OnlyController = null;
            if (Button("Proporcional", x+a+4, y, b, 36, app.World.OnlyController == ControllerKind.Proportional))
                app.World.OnlyController = ControllerKind.Proportional;
            if (Button("PFL3", x+a+b+8, y, width-a-b-8, 36, app.World.OnlyController == ControllerKind.Pfl3))
                app.World.OnlyController = ControllerKind.Pfl3;
        }

        void DrawGuideTimes()
        {
            TrialEngine[] previous = app.PreviousGuideTrial;
            Text("Controlador", 361, 753, 211, 26, small);
            Text("Llegada", 587, 753, 180, 26, small);
            Text("Recorrido", 779, 753, 152, 26, small);
            string measure = Scenario == ScenarioKind.MovingGoal ? "Reorientación" : Scenario == ScenarioKind.Obstacles ? "Contactos" : "Dist. a meta";
            Text(measure, 945, 753, 206, 26, small);
            if (previous != null) Text("Antes (" + Degrees(previous[0].Definition.initialHeading) + ")", 1182, 753, 210, 26, small);
            Box(new Rect(360, 783, 1030, 1), edge);
            for (int i = 0; i < app.Engines.Count; i++)
            {
                TrialEngine e = app.Engines[i];
                float y = 791 + i*40;
                Marker(new Vector2(370, y+13), e.Kind);
                Text(e.Kind == ControllerKind.Proportional ? "Proporcional" : "PFL3", 390, y, 185, 31);
                string arrival = Arrival(e);
                if (Scenario == ScenarioKind.MovingGoal && !e.Finished && WaitingAtGoal(e)) arrival = "Espera en meta 1";
                Text(arrival, 587, y, 191, 34);
                Text(F(e.RouteLength,"0.0")+" m",779,y,150,34,readout);
                string value = F(Vec2.Distance(e.Position,e.Goal),"0.0") + " m";
                if (Scenario == ScenarioKind.Obstacles) value = e.Collisions.ToString();
                else if (Scenario == ScenarioKind.MovingGoal)
                    value = e.ReorientationSeconds.HasValue ? F(e.ReorientationSeconds.Value) + " s" : "—";
                Text(value,945,y,203,34,readout);
                if (previous != null) Text(Arrival(previous[i]),1182,y,207,34);
            }
        }

        Vector2 MapPoint(Vec2 position)
        {
            Vector3 uv = app.World.Camera.WorldToViewportPoint(LabWorld.At(position));
            Rect arena = LabApp.GuideArena;
            return new Vector2(arena.x + uv.x*arena.width, arena.yMax - uv.y*arena.height);
        }

        void DrawGuideMap(bool preparing)
        {
            TrialDefinition d = app.Selected;
            Vector2 start = MapPoint(d.start);
            MapLabel("Salida", start + new Vector2(13, 9), 65);
            if (Scenario == ScenarioKind.MovingGoal)
            {
                bool switched = !preparing && app.Engines[0].GoalSwitched;
                MapLabel(switched ? "1 · Anterior" : "1 · Activa", MapPoint(d.goal)+new Vector2(14,-15), 107);
                MapLabel(switched ? "2 · Activa" : "2 · Próxima", MapPoint(d.secondGoal)+new Vector2(14,-15), 114);
            }
            else MapLabel("Meta", MapPoint(d.goal) + new Vector2(13,-15), 57);
            foreach (CircleObstacle obstacle in d.obstacles)
            {
                Vector2 pos = MapPoint(obstacle.center);
                Text((obstacle.id+1).ToString(),pos.x-5,pos.y-10,25,27,caption);
            }
            if (preparing)
            {
                Marker(start, ControllerKind.Proportional);
                Marker(start, ControllerKind.Pfl3);
            }
            else foreach (TrialEngine engine in app.Engines)
                if (app.World.Shows(engine.Kind)) Marker(MapPoint(engine.Position),engine.Kind);
        }

        void MapLabel(string label, Vector2 origin, float width)
        {
            Surface(new Rect(origin.x, origin.y, width, 27),background,edge,3);
            Text(label,origin.x+6,origin.y+2,width-9,25,small);
        }

        void Marker(Vector2 point, ControllerKind kind)
        {
            if (kind == ControllerKind.Pfl3)
            {
                Rounded(new Rect(point.x-3.5f,point.y-3.5f,7,7),LabWorld.Cyan,3.5f);
                return;
            }
            float x = point.x-7, y = point.y-7;
            Box(new Rect(x,y,14,2),LabWorld.Amber);
            Box(new Rect(x,y+12,14,2),LabWorld.Amber);
            Box(new Rect(x,y+2,2,10),LabWorld.Amber);
            Box(new Rect(x+12,y+2,2,10),LabWorld.Amber);
        }
    }
}
