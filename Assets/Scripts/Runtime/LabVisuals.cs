using System;
using System.Globalization;
using System.IO;
using NpcLab.Core;
using UnityEngine;

namespace NpcLab.Runtime
{
    public sealed partial class LabUI
    {
        readonly LabApp app;
        readonly Color background = new Color32(13, 22, 27, 255);
        readonly Color panel = new Color32(23, 38, 44, 255);
        readonly Color edge = new Color32(52, 75, 80, 255);
        readonly Color ink = new Color32(10, 31, 32, 255);
        readonly Color muted = new Color32(163, 186, 186, 255);
        readonly Color white = new Color32(235, 242, 235, 255);
        static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");
        GUIStyle body, small, heading, title, headline, button, field, toggle, readout, caption, circuitTitle;
        Texture2D fly;
        Font textFont, displayFont, numberFont;

        public LabUI(LabApp app) { this.app = app; }

        static Font InstalledFont(string[] installed, params string[] choices)
        {
            foreach (string name in choices)
                if (Array.Exists(installed, candidate => string.Equals(name, candidate, StringComparison.OrdinalIgnoreCase)))
                    return Font.CreateDynamicFontFromOSFont(name, 20);
            return null;
        }

        void Styles()
        {
            if (body != null) return;
            string[] installed = Font.GetOSInstalledFontNames();
            textFont = InstalledFont(installed, "Inter", "Segoe UI", "Arial", "Liberation Sans");
            displayFont = InstalledFont(installed, "Barlow", "Bahnschrift", "Segoe UI", "Arial", "Liberation Sans");
            numberFont = InstalledFont(installed, "IBM Plex Mono", "Consolas", "DejaVu Sans Mono");
            body = new GUIStyle(GUI.skin.label) { font = textFont, fontSize = 18, wordWrap = true,
                padding = new RectOffset(), margin = new RectOffset() };
            body.normal.textColor = white;
            small = new GUIStyle(body) { fontSize = 15 };
            small.normal.textColor = muted;
            caption = new GUIStyle(small) { font = numberFont, fontSize = 14 };
            heading = new GUIStyle(body) { font = displayFont, fontSize = 27, fontStyle = FontStyle.Bold };
            circuitTitle = new GUIStyle(heading) { fontSize = 24, alignment = TextAnchor.MiddleLeft };
            circuitTitle.normal.textColor = ink;
            title = new GUIStyle(heading) { fontSize = 49 };
            headline = new GUIStyle(heading) { fontSize = 74 };
            readout = new GUIStyle(body) { font = numberFont, fontSize = 18 };
            button = new GUIStyle(body) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            field = new GUIStyle(GUI.skin.textField) { font = numberFont, fontSize = 20, padding = new RectOffset(12,12,8,8) };
            toggle = new GUIStyle(GUI.skin.toggle) { font = textFont, fontSize = 16 };
            toggle.normal.textColor = white;
            toggle.onNormal.textColor = white;

            string path = Path.Combine(Application.streamingAssetsPath, "Drosophila.png");
            if (File.Exists(path))
            {
                fly = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                if (!ImageConversion.LoadImage(fly, File.ReadAllBytes(path), true))
                    Debug.LogWarning("No se pudo cargar la ilustración de Drosophila.");
            }
        }

        public void Dispose()
        {
            if (fly != null) UnityEngine.Object.Destroy(fly);
            if (textFont != null) UnityEngine.Object.Destroy(textFont);
            if (displayFont != null) UnityEngine.Object.Destroy(displayFont);
            if (numberFont != null) UnityEngine.Object.Destroy(numberFont);
        }

        static string F(double value, string format = "0.00") => value.ToString(format, Spanish);

        void Box(Rect rect, Color color) => GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, 0);

        void Rounded(Rect rect, Color color, float radius) =>
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0, color, 0, radius);

        void Surface(Rect rect, Color fill, Color border, float radius = 8)
        {
            float pixelScale = Mathf.Min(Screen.width / 1440f, Screen.height / 900f);
            float stroke = Mathf.Max(2f, 1.8f / pixelScale);
            Rounded(rect, border, radius);
            Rounded(new Rect(rect.x+stroke, rect.y+stroke, rect.width-2*stroke, rect.height-2*stroke),
                fill, Mathf.Max(0, radius-stroke));
            // Draw the straight runs last so fractional UI scaling cannot erase the lower rim.
            FrameEdges(rect, border, radius, stroke);
        }

        void FrameEdges(Rect r, Color color, float inset, float stroke)
        {
            Box(new Rect(r.x+inset,r.y,r.width-2*inset,stroke),color);
            Box(new Rect(r.x+inset,r.yMax-stroke,r.width-2*inset,stroke),color);
            Box(new Rect(r.x,r.y+inset,stroke,r.height-2*inset),color);
            Box(new Rect(r.xMax-stroke,r.y+inset,stroke,r.height-2*inset),color);
        }

        void Text(string value, float x, float y, float width, float height, GUIStyle style = null) =>
            GUI.Label(new Rect(x, y, width, height), value, style ?? body);

        bool Button(string label, float x, float y, float width, float height, bool primary = false)
        {
            Rect rect = new Rect(x, y, width, height);
            bool hovered = rect.Contains(Event.current.mousePosition);
            Color fill = primary ? LabWorld.Cyan : new Color32(37, 57, 63, 255);
            if (hovered) fill = Color.Lerp(fill, white, .12f);
            Surface(rect, fill, primary ? fill : hovered ? muted : edge, 6);
            button.normal.textColor = primary ? ink : white;
            GUI.Label(rect, label, button);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        bool Card(string name, string description, float x, float y, float width, float height, bool selected)
        {
            Rect rect = new Rect(x, y, width, height);
            bool hover = rect.Contains(Event.current.mousePosition);
            Surface(rect, selected ? new Color32(28, 65, 66, 255) : panel, selected ? LabWorld.Cyan : hover ? muted : edge);
            if (selected) Rounded(new Rect(x + 15, y + 21, 4, 26), LabWorld.Cyan, 2);
            Text(name, x + 28, y + 16, width - 46, 42, heading);
            Text(description, x + 28, y + 61, width - 48, height - 62, small);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        bool CircuitCard(float x, string name, string detail)
        {
            Rect rect = new Rect(x,710,420,137);
            bool hover = rect.Contains(Event.current.mousePosition);
            Color accent = hover ? Color.Lerp(LabWorld.Cyan, white, .19f) : LabWorld.Cyan;
            Surface(rect, panel, hover ? accent : edge);
            Rounded(new Rect(x+2,712,416,49), accent, 6);
            Box(new Rect(x+2,754,416,7), accent);
            Text(name,x+20,713,380,46,circuitTitle);
            Text(detail,x+20,776,380,59,body);
            return GUI.Button(rect,GUIContent.none,GUIStyle.none);
        }

        void Header(string subtitle)
        {
            Box(new Rect(0, 0, 1440, 83), background);
            Box(new Rect(24, 82, 1392, 1), edge);
            Surface(new Rect(27, 20, 40, 40), ink, edge);
            Marker(new Vector2(47,40),ControllerKind.Proportional);
            Marker(new Vector2(47,40),ControllerKind.Pfl3);
            Text("Drosophila Navigation Lab", 81, 11, 775, 41, heading);
            Text(subtitle, 82, 48, 960, 26, small);
            Text("PEC3   /   " + LabApp.AppVersion, 1194, 32, 222, 29, caption);
        }

        void ArenaPreview()
        {
            var frame=new Rect(783,112,592,493);
            var border=new Color32(83,117,119,255);
            // The centre is the live Unity camera. Only its title bar, caption and frame cover it.
            Surface(new Rect(frame.x,frame.y,frame.width,53),panel,border,8);
            Box(new Rect(785,151,588,12),panel);
            FrameEdges(frame,border,2,2);
            Text("Circuito B  /  Eslalon",804,124,405,38,heading);
            Rounded(new Rect(1256,134,8,8),LabWorld.Cyan,4);
            Text("En pista",1273,123,98,34,small);
            Surface(new Rect(783,511,592,94),panel,border,8);
            if (fly!=null) GUI.DrawTexture(new Rect(798,523,67,69),fly,ScaleMode.ScaleToFit,true);
            Text("Agentes autónomos",876,521,437,37,heading);
            Marker(new Vector2(887,574),ControllerKind.Proportional);
            Text("Proporcional",903,561,155,28,small);
            Marker(new Vector2(1074,574),ControllerKind.Pfl3);
            Text("PFL3",1092,561,105,28,small);
        }

        void DrawTitle()
        {
            Header("Arena de navegación de NPC");
            Text("Arena de",63,142,694,97,headline);
            Text("navegación",63,220,694,100,headline);
            Text("Elige un circuito para comparar a los dos NPC. También puedes diseñar los obstáculos o decidir dónde y cuándo aparece cada meta.",
                68,337,641,90);
            if (Button("Taller de obstáculos",67,438,290,57)) app.EnterWorkshop();
            if (Button("Editor de metas",374,438,297,57)) app.EnterWorkshop(ScenarioKind.MovingGoal);
            if (Button("Ensayos del piloto",67,515,290,46)) app.ChangeScreen(LabScreen.Setup);
            ArenaPreview();
            Box(new Rect(65,637,1310,2),edge);
            Text("Selección de circuito",66,649,640,49,heading);
            if (Button("Instrucciones",986,649,163,38)) app.ChangeScreen(LabScreen.Instructions);
            if (Button("Créditos y fuentes",1164,649,211,38)) app.ChangeScreen(LabScreen.Credits);
            if (CircuitCard(66,"A  ·  Espacio abierto","Gira la salida y observa quién alcanza la meta de frente, de lado o de espaldas."))
                app.EnterGuide();
            if (CircuitCard(510,"B  ·  Eslalon","Seis obstáculos, una bandera. Compara por qué lado pasa cada NPC."))
                app.EnterGuide(ScenarioKind.Obstacles);
            if (CircuitCard(954,"C  ·  Relevo de balizas","La meta cambia a los 10 y 16 s. Sigue a los NPC cuando tengan que volver a girar."))
                app.EnterGuide(ScenarioKind.MovingGoal);
        }
    }
}
