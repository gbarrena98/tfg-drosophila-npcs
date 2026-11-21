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
        GUIStyle body, small, heading, title, headline, button, field, toggle, readout, caption;
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
            Rounded(rect, border, radius);
            Rounded(new Rect(rect.x + 1, rect.y + 1, rect.width - 2, rect.height - 2), fill, Mathf.Max(0, radius - 1));
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

        void Specimen()
        {
            Rect plate = new Rect(783, 112, 592, 493);
            Surface(plate, new Color32(19, 42, 45, 255), edge, 12);
            Color line = new Color32(38, 65, 66, 255);
            for (int col = 0; col < 11; col++)
                Box(new Rect(811 + 52 * col, 136, 1, 391), line);
            for (int row = 0; row < 8; row++)
                Box(new Rect(802, 147 + 52 * row, 553, 1), line);
            for (int i = 0; i < 2; i++)
            {
                float radius = 126 + i * 58;
                GUI.DrawTexture(new Rect(1081 - radius, 319 - radius, radius * 2, radius * 2), Texture2D.whiteTexture,
                    ScaleMode.StretchToFill, true, 0, new Color32(59, 89, 87, 255), 1, radius);
            }
            if (fly != null)
            {
                Matrix4x4 old = GUI.matrix;
                GUI.matrix = old * Matrix4x4.TRS(new Vector3(1080, 324, 0), Quaternion.Euler(0, 0, 22), Vector3.one);
                GUI.DrawTexture(new Rect(-196, -211, 392, 422), fly, ScaleMode.ScaleToFit, true);
                GUI.matrix = old;
            }
            Box(new Rect(808, 538, 540, 1), edge);
            Text("Drosophila melanogaster", 812, 551, 369, 44, heading);
            Text("Vista dorsal", 1217, 561, 138, 24, caption);
        }

        void DrawTitle()
        {
            Box(new Rect(0, 0, 1440, 900), background);
            Header("Control de orientación de NPC");
            Text("Navegación", 63, 142, 694, 97, headline);
            Text("bioinspirada", 63, 221, 694, 99, headline);
            Text("Elige hacia dónde miran los dos NPC al salir y observa cómo encuentra cada uno el camino hasta la meta.",
                68, 344, 625, 76, body);
            if (Button("Primer ensayo", 67, 438, 290, 65, true)) app.EnterGuide();
            if (Button("Configuración libre", 374, 438, 280, 65)) app.ChangeScreen(LabScreen.Setup);
            Specimen();
            Box(new Rect(65, 637, 1310, 1), edge);
            Text("Explorar la arena", 66, 650, 640, 49, heading);
            if (Button("Instrucciones", 986, 649, 163, 38)) app.ChangeScreen(LabScreen.Instructions);
            if (Button("Créditos y fuentes", 1164, 649, 211, 38)) app.ChangeScreen(LabScreen.Credits);
            if (Card("B · Obstáculos", "Compara cómo rodean tres obstáculos fijos para alcanzar una misma meta.",
                    66, 710, 639, 137, false)) app.EnterGuide(ScenarioKind.Obstacles);
            if (Card("C · Cambio de meta", "Observa cómo se reorientan cuando el objetivo cambia a los 10 s.",
                    726, 710, 648, 137, false)) app.EnterGuide(ScenarioKind.MovingGoal);
        }
    }
}
