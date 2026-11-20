using System;
using System.Globalization;
using NpcLab.Core;
using UnityEngine;

namespace NpcLab.Runtime
{
    public sealed partial class LabUI
    {
        static string Degrees(double angle)
        {
            return angle.ToString("0.#", CultureInfo.InvariantCulture).Replace("-", "−") + "°";
        }

        static string Arrival(TrialEngine engine)
        {
            if (engine.ArrivalSeconds.HasValue) return F(engine.ArrivalSeconds.Value) + " s";
            switch (engine.State)
            {
                case TrialState.Timeout: return "Tiempo agotado";
                case TrialState.Aborted: return "Interrumpido";
                default: return State(engine.State);
            }
        }

        void DrawGuide()
        {
            Header("Primer ensayo   /   A-00 · Espacio abierto");
            bool preparing = app.ScreenState == LabScreen.GuideSetup;
            Box(new Rect(24, 102, 322, 765), panel);
            Box(new Rect(370, 103, 1040, 2), panel);
            Text("Vista superior", 380, 116, 270, 32, heading);
            Marker(new Vector2(950, 132), ControllerKind.Proportional);
            Text("Proporcional", 968, 118, 140, 34);
            Marker(new Vector2(1128, 132), ControllerKind.Pfl3);
            Text("PFL3", 1146, 118, 110, 34);

            if (preparing) DrawGuideSetup();
            else DrawGuideControls();
            DrawGuideMap(preparing);

            Box(new Rect(370, 681, 1040, 186), panel);
            if (preparing)
            {
                Text("Antes de salir", 398, 699, 960, 42, heading);
                Text("La flecha blanca marca el rumbo que has elegido. Los dos NPC salen del mismo punto y se orientan hacia la meta por su cuenta.",
                    399, 753, 945, 79);
            }
            else DrawGuideTimes();
        }

        void DrawGuideSetup()
        {
            Text("Rumbo de salida", 47, 126, 282, 42, heading);
            bool returning = app.Engines.Count > 0;
            string help = returning
                ? "Elige otro rumbo para ver qué cambia. Se mantienen la salida, la meta y los dos controladores."
                : "Elige hacia dónde miran los dos NPC al empezar. La meta es el círculo verde.";
            Text(help, 48, 184, 268, 112);
            TrialDefinition definition = app.Selected;
            double towards = Angles.Heading(definition.goal - definition.start);
            HeadingChoice("De lado", definition.initialHeading, 334);
            HeadingChoice("Hacia la meta", towards, 403);
            HeadingChoice("De espaldas", Angles.Wrap(towards - 180), 472);
            bool opposite = Math.Abs(Math.Abs(Angles.Wrap(app.GuideHeading - towards)) - 180) < .001;
            if (opposite)
                Text("La meta queda justo detrás. Puedes acelerar el ensayo hasta 4×.", 48, 549, 267, 78, small);
            if (Button(returning ? "Lanzar de nuevo" : "Iniciar comparación", 47, 648, 276, 54, true))
                app.StartTrial();
            if (Button("Volver al menú", 47, 717, 276, 46))
                app.ChangeScreen(LabScreen.Title);
            if (!string.IsNullOrEmpty(app.Notice) && !app.ResultsSaved)
                Text(app.Notice, 48, 776, 270, 76, small);
        }

        void HeadingChoice(string label, double angle, float y)
        {
            bool selected = Math.Abs(Angles.Wrap(app.GuideHeading - angle)) < .001;
            if (Button(label + "    " + Degrees(angle), 47, y, 276, 52, selected))
                app.SetGuideHeading(angle);
            if (selected) Box(new Rect(36, y + 4, 4, 44), LabWorld.Cyan);
        }

        void DrawGuideControls()
        {
            bool results = app.ScreenState == LabScreen.Results;
            bool paused = app.ScreenState == LabScreen.Paused;
            Text(results ? "Resultados" : paused ? "En pausa" : "En marcha", 47, 126, 282, 47, heading);
            Text("Rumbo de salida", 48, 203, 270, 34, small);
            Text(Degrees(app.GuideHeading), 47, 235, 272, 65, title);
            double time = Math.Max(app.Engines[0].Elapsed, app.Engines[1].Elapsed);
            Text("Tiempo simulado", 48, 334, 270, 30, small);
            Text(F(time, "0.0") + " / 60 s", 48, 367, 270, 42, heading);

            if (results)
            {
                Text("Prueba otro rumbo y compara los tiempos con los de esta salida.",
                    48, 428, 266, 84);
                if (Button("Cambiar rumbo y repetir", 47, 531, 276, 52, true)) app.PrepareGuide();
                if (Button("Repetir el mismo rumbo", 47, 595, 276, 45)) app.StartTrial();
                if (Button("Configuración libre", 47, 652, 276, 45)) app.ChangeScreen(LabScreen.Setup);
                if (Button(app.ResultsSaved ? "Abrir carpeta CSV" : "Reintentar guardado", 47, 709, 276, 45))
                {
                    if (app.ResultsSaved) app.OpenResults();
                    else app.SaveResults();
                }
                Text(app.Notice, 48, 782, 266, 70, small);
            }
            else
            {
                Text("Sigue el marco del proporcional y el punto del PFL3. Pueden recorrer la misma línea.",
                    48, 427, 265, 98);
                if (Button(paused ? "Continuar" : "Pausar", 47, 551, 276, 52, true))
                {
                    if (paused) app.Resume();
                    else app.Pause();
                }
                if (Button(app.PlaybackSpeed == 1 ? "Acelerar a 4×" : "Volver a 1×", 47, 616, 276, 46))
                    app.ToggleSpeed();
                if (Button("Detener y ver resultados", 47, 675, 276, 46)) app.AbortToResults();
            }
        }

        void DrawGuideTimes()
        {
            TrialEngine[] previous = app.PreviousGuideTrial;
            Text("Controlador", 399, 699, 225, 33, small);
            Text("Llegada con " + Degrees(app.GuideHeading), 665, 699, 235, 33, small);
            Text("Distancia", 921, 699, 195, 33, small);
            if (previous != null)
                Text("Anterior (" + Degrees(previous[0].Definition.initialHeading) + ")", 1119, 699, 264, 33, small);

            for (int i = 0; i < app.Engines.Count; i++)
            {
                TrialEngine engine = app.Engines[i];
                float y = 742 + i * 52;
                Marker(new Vector2(409, y + 14), engine.Kind);
                Text(engine.Kind == ControllerKind.Proportional ? "Proporcional" : "PFL3", 432, y, 215, 40);
                Text(Arrival(engine), 666, y, 243, 39);
                Text(F(engine.RouteLength, "0.0") + " m", 921, y, 190, 39);
                if (previous != null) Text(Arrival(previous[i]), 1119, y, 254, 39);
            }
        }

        Vector2 MapPoint(Vec2 point)
        {
            Vector3 uv = app.World.Camera.WorldToViewportPoint(LabWorld.At(point));
            Rect arena = LabApp.GuideArena;
            return new Vector2(arena.x + uv.x * arena.width, arena.yMax - uv.y * arena.height);
        }

        void DrawGuideMap(bool preparing)
        {
            TrialDefinition definition = app.Selected;
            Vector2 start = MapPoint(definition.start);
            MapLabel("Salida", start + new Vector2(13, 10), 62);
            MapLabel("Meta", MapPoint(definition.goal) + new Vector2(12, -15), 55);
            if (preparing)
            {
                Vector2 tip = MapPoint(definition.start + Angles.Forward(app.GuideHeading) * 2.3);
                Vector2 direction = (tip - start).normalized;
                Vector2 side = new Vector2(-direction.y, direction.x);
                GuideLine(start, tip);
                GuideLine(tip, tip - 8 * direction + 4 * side);
                GuideLine(tip, tip - 8 * direction - 4 * side);
                Marker(start, ControllerKind.Proportional);
                Marker(start, ControllerKind.Pfl3);
            }
            else
            {
                foreach (TrialEngine engine in app.Engines) Marker(MapPoint(engine.Position), engine.Kind);
            }
        }

        void MapLabel(string label, Vector2 origin, float width)
        {
            Box(new Rect(origin.x, origin.y, width, 27), background);
            Text(label, origin.x + 6, origin.y + 2, width - 8, 25);
        }

        void GuideLine(Vector2 from, Vector2 to)
        {
            Matrix4x4 old = GUI.matrix;
            Vector2 delta = to - from;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, from);
            Box(new Rect(from.x, from.y - 1, delta.magnitude, 2), white);
            GUI.matrix = old;
        }

        void Marker(Vector2 point, ControllerKind kind)
        {
            if (kind == ControllerKind.Pfl3)
            {
                Box(new Rect(point.x - 3, point.y - 3, 6, 6), LabWorld.Cyan);
                return;
            }
            float x = point.x - 7, y = point.y - 7;
            Box(new Rect(x, y, 14, 2), LabWorld.Amber);
            Box(new Rect(x, y + 12, 14, 2), LabWorld.Amber);
            Box(new Rect(x, y + 2, 2, 10), LabWorld.Amber);
            Box(new Rect(x + 12, y + 2, 2, 10), LabWorld.Amber);
        }
    }
}
