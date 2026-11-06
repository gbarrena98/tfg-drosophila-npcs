using System;
using System.Globalization;
using NpcLab.Core;
using UnityEngine;

namespace NpcLab.Runtime
{
    // Immediate-mode UI keeps the prototype independent of UGUI/TMP packages and fonts.
    public sealed class LabUI
    {
        readonly LabApp app;
        GUIStyle body, small, heading, title, button, field, toggle;
        readonly Color background=new Color(.035f,.055f,.08f), panel=new Color(.065f,.10f,.145f);
        readonly Color muted=new Color(.62f,.7f,.76f), white=new Color(.91f,.95f,.96f);
        public LabUI(LabApp app){this.app=app;}
        void Styles()
        {
            if(body!=null)return;
            body=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true};body.normal.textColor=white;
            small=new GUIStyle(body){fontSize=14};small.normal.textColor=muted;
            heading=new GUIStyle(body){fontSize=24,fontStyle=FontStyle.Bold};
            title=new GUIStyle(body){fontSize=52,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button){fontSize=17,fontStyle=FontStyle.Bold};
            button.normal.textColor=background;button.hover.textColor=background;button.active.textColor=background;
            button.normal.background=Texture2D.whiteTexture;button.hover.background=Texture2D.whiteTexture;button.active.background=Texture2D.whiteTexture;
            field=new GUIStyle(GUI.skin.textField){fontSize=20,padding=new RectOffset(12,12,8,8)};
            toggle=new GUIStyle(GUI.skin.toggle){fontSize=15};toggle.normal.textColor=white;toggle.onNormal.textColor=white;
        }
        static string F(double x,string format="0.00"){return x.ToString(format,CultureInfo.InvariantCulture);}
        void Box(Rect r,Color color){Color old=GUI.color;GUI.color=color;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
        void Text(string t,float x,float y,float w,float h,GUIStyle style=null){GUI.Label(new Rect(x,y,w,h),t,style??body);}
        bool Button(string t,float x,float y,float w,float h,bool primary=false)
        {Color old=GUI.backgroundColor;GUI.backgroundColor=primary?LabWorld.Cyan:new Color(.55f,.65f,.72f);bool hit=GUI.Button(new Rect(x,y,w,h),t,button);GUI.backgroundColor=old;return hit;}
        bool Card(string tag,string description,float x,float y,float w,float h,bool selected)
        {
            Box(new Rect(x,y,w,h),selected?new Color(.1f,.25f,.29f):panel);
            if(selected)Box(new Rect(x,y,4,h),LabWorld.Cyan);
            Text(tag,x+20,y+18,w-40,38,heading);Text(description,x+20,y+63,w-40,h-70,small);
            return GUI.Button(new Rect(x,y,w,h),GUIContent.none,GUIStyle.none);
        }
        public void Draw()
        {
            Styles();Matrix4x4 old=GUI.matrix;float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1440*scale)/2,(Screen.height-900*scale)/2,0),Quaternion.identity,new Vector3(scale,scale,1));
            HandleKeys();
            if(app.ScreenState==LabScreen.Title)DrawTitle();
            else if(app.ScreenState==LabScreen.Setup)DrawSetup();
            else if(app.ScreenState==LabScreen.Instructions)DrawInstructions();
            else if(app.ScreenState==LabScreen.Credits)DrawCredits();
            else DrawTrial();
            GUI.matrix=old;
        }
        void Header(string subtitle)
        {
            Box(new Rect(0,0,1440,82),background);
            Text("NPC / NAVIGATION LAB",24,16,650,34,heading);Text(subtitle,26,50,1000,26,small);
            Text("PEC2 · v0.2.0",1215,25,210,30,small);
        }
        void DrawTitle()
        {
            Box(new Rect(0,0,1440,900),background);Header("Control de orientación · TFG de Ingeniería Informática");
            Text("Navegación bioinspirada",110,155,1200,85,title);
            Text("Observa y compara dos formas de orientar un NPC hacia una meta.",114,255,1120,50,heading);
            Text("Modelo reducido PFL3 y control proporcional bajo las mismas reglas de movimiento.",114,315,1120,55,body);
            Card("A  Espacio abierto","Corrección del rumbo y llegada a una meta fija.",110,420,380,145,false);
            Card("B  Obstáculos","Orientación con una capa común de evitación local.",530,420,380,145,false);
            Card("C  Cambio de meta","Nueva referencia a los 10 segundos de simulación.",950,420,380,145,false);
            if(Button("CONFIGURAR ENSAYO",110,625,400,62,true))app.ChangeScreen(LabScreen.Setup);
            if(Button("Instrucciones",550,625,300,62))app.ChangeScreen(LabScreen.Instructions);
            if(Button("Créditos y fuentes",890,625,330,62))app.ChangeScreen(LabScreen.Credits);
            Text("Prototipo experimental · Resultados exportables · Configuraciones guardadas",114,790,1200,35,small);
        }
        void DrawSetup()
        {
            Box(new Rect(0,0,1440,900),background);Header("Configuración del ensayo");
            Text("1  Elige un escenario",80,115,1200,40,heading);
            string[] descriptions={"Arena sin obstáculos. La meta permanece fija.","Tres obstáculos circulares y evitación geométrica común.","La meta cambia a los 10 s. Llegar a la primera no termina la prueba."};
            for(int i=0;i<3;++i)
                if(Card(LabApp.ScenarioNames[i],descriptions[i],80+i*435,175,410,140,app.ScenarioIndex==i))
                {app.ScenarioIndex=i;app.LoadSelection();}
            Text("2  Elige qué observar",80,350,1200,40,heading);
            string[] models={"Referencia de giro proporcional al error angular.","Modelo funcional publicado con 24 unidades PFL3.","Dos agentes parten de la misma configuración."};
            for(int i=0;i<3;++i)
                if(Card(LabApp.ControllerNames[i],models[i],80+i*435,410,410,130,app.ControllerSelection==i))
                {app.ControllerSelection=i;app.Preview();}
            Box(new Rect(80,580,1280,130),panel);
            Text("Configuración guardada",102,594,420,30,small);
            if(Button("−",102,632,50,48)){app.ConfigurationIndex--;app.LoadSelection();}
            Text(app.Selected.configurationId,175,637,200,42,heading);
            if(Button("+",350,632,50,48)){app.ConfigurationIndex++;app.LoadSelection();}
            Text("Semilla "+app.Selected.seed,440,600,300,32,small);
            Text("1,5 m/s · 120°/s · paso 0,02 s",440,640,440,40,body);
            Text("Rumbo inicial (grados)",930,594,350,30,small);
            GUI.SetNextControlName("initial-heading");app.HeadingInput=GUI.TextField(new Rect(930,632,260,48),app.HeadingInput,field);
            if(Button("Caso 180°",1200,632,140,48))app.HeadingInput=Angles.Wrap(Angles.Heading(app.Selected.goal-app.Selected.start)-180).ToString("R",CultureInfo.InvariantCulture);
            if(Button("INICIAR ENSAYO  [Enter]",80,760,470,60,true))app.StartTrial();
            if(Button("Volver al inicio",580,760,310,60))app.ChangeScreen(LabScreen.Title);
            Text(app.Notice.Length>0?app.Notice:"Las configuraciones son de desarrollo. Los parámetros definitivos se fijarán tras el piloto.",80,843,1280,45,small);
        }
        void DrawTrial()
        {
            Header(LabApp.ScenarioNames[app.ScenarioIndex]+"  /  "+app.Selected.configurationId);
            Box(new Rect(20,100,280,780),panel);
            Text("SESIÓN",40,120,240,28,small);
            Text(LabApp.ControllerNames[app.ControllerSelection],40,156,240,65,heading);
            double elapsed=0;foreach(TrialEngine e in app.Engines)elapsed=Math.Max(elapsed,e.Elapsed);
            Text("Tiempo simulado",40,235,240,28,small);Text(F(elapsed)+" s",40,268,240,52,title);
            Text("Límite 60 s · llegada ≤ 0,5 m",40,333,240,45,small);
            app.Trajectories=GUI.Toggle(new Rect(40,395,240,35),app.Trajectories," Mostrar recorridos",toggle);
            app.ReferenceArrows=GUI.Toggle(new Rect(40,438,240,35),app.ReferenceArrows," Mostrar referencia",toggle);
            bool top=GUI.Toggle(new Rect(40,481,240,35),app.TopView," Vista superior",toggle);
            if(top!=app.TopView){app.TopView=top;app.World.SetTopView(top);}
            if(app.ScreenState==LabScreen.Running)
            {
                if(Button("Pausar  [Espacio]",40,552,240,45,true))app.Pause();
                if(Button("Detener y guardar",40,611,240,45))app.AbortToResults();
                if(Button(app.VisualSpeed==1?"Velocidad 1× → 4×":"Velocidad 4× → 1×",40,670,240,42))
                {app.VisualSpeed=app.VisualSpeed==1?4:1;Time.timeScale=app.VisualSpeed;}
            }
            else if(app.ScreenState==LabScreen.Paused)
            {
                if(Button("Continuar [Espacio]",40,552,240,45,true))app.Resume();
                if(Button("Detener y guardar",40,611,240,45))app.AbortToResults();
                Text("EN PAUSA",40,681,240,36,heading);
            }
            else
            {
                if(Button("Repetir ensayo [R]",40,552,240,45,true))app.StartTrial();
                if(Button("Cambiar configuración",40,611,240,45))app.ChangeScreen(LabScreen.Setup);
                if(Button("Abrir carpeta CSV",40,670,240,42))app.OpenResults();
                if(Button("Reintentar guardado",40,724,240,40))app.SaveResults();
            }
            Text("Cian: PFL3\nÁmbar: proporcional",40,795,240,65,small);
            Box(new Rect(320,100,1100,3),new Color(.19f,.29f,.34f));
            Text(app.ScreenState==LabScreen.Results?"ENSAYOS FINALIZADOS":app.ScreenState==LabScreen.Paused?"PAUSA":"OBSERVACIÓN EN CURSO",340,113,620,32,small);
            Text("Paso fijo · referencias y condiciones equivalentes",340,681,1050,27,small);
            DrawMetrics();
            if(app.ScreenState==LabScreen.Results)
                Text(app.Notice,340,164,1000,58,body);
            foreach(TrialEngine e in app.Engines)
                if(e.Kind==ControllerKind.Pfl3)
                {
                    float w=180;float y=app.ScreenState==LabScreen.Results?246:170;
                    Text("Actividad PFL3",1175,y,220,27,small);
                    Box(new Rect(1175,y+35,w,9),panel);Box(new Rect(1175,y+35,w*Mathf.Clamp01((float)e.LeftActivity/752),9),LabWorld.Cyan);
                    Text("L "+F(e.LeftActivity,"0.0"),1175,y+48,220,24,small);
                    Box(new Rect(1175,y+78,w,9),panel);Box(new Rect(1175,y+78,w*Mathf.Clamp01((float)e.RightActivity/752),9),LabWorld.Cyan);
                    Text("R "+F(e.RightActivity,"0.0"),1175,y+91,220,24,small);
                }
        }
        void DrawMetrics()
        {
            Box(new Rect(320,725,1100,155),panel);
            string[] headings={"CONTROLADOR / ESTADO","LLEGADA","ERROR MEDIO","RECORRIDO","CONTACTOS","COSTE P95"};
            float[] xs={340,645,780,965,1120,1260};float[] widths={295,125,175,145,130,145};
            for(int j=0;j<headings.Length;++j)Text(headings[j],xs[j],738,widths[j],25,small);
            for(int i=0;i<app.Engines.Count;++i)
            {
                TrialEngine e=app.Engines[i];float y=778+i*46;
                Color old=GUI.color;GUI.color=e.Kind==ControllerKind.Pfl3?LabWorld.Cyan:LabWorld.Amber;
                Text((e.Kind==ControllerKind.Pfl3?"PFL3":"Proporcional")+" · "+State(e.State),xs[0],y,widths[0],42,body);GUI.color=old;
                Text(e.ArrivalSeconds.HasValue?F(e.ArrivalSeconds.Value)+" s":"—",xs[1],y,widths[1],32);
                Text(e.Samples.Count>0?F(e.MeanError)+"°":"—",xs[2],y,widths[2],32);
                Text(F(e.RouteLength)+" m",xs[3],y,widths[3],32);Text(e.Collisions.ToString(),xs[4],y,widths[4],32);
                Text(e.Samples.Count>0?F(e.P95ControllerMs,"0.000")+" ms":"—",xs[5],y,widths[5],32);
            }
        }
        public static string State(TrialState state)
        {
            switch(state){case TrialState.Success:return "Llegada";case TrialState.Timeout:return "Tiempo agotado";
                case TrialState.Aborted:return "Interrumpido";case TrialState.Paused:return "Pausa";case TrialState.Running:return "En curso";default:return "Preparado";}
        }
        void DrawInstructions()
        {
            Box(new Rect(0,0,1440,900),background);Header("Instrucciones");Text("Cómo usar el laboratorio",80,130,1200,60,title);
            Card("1  Configura","Elige escenario, controlador y una de las 30 configuraciones guardadas de cada escenario. Comparación ejecuta la pareja.",80,250,410,205,false);
            Card("2  Observa","El NPC se orienta y avanza. Puedes pausar, alternar la vista y ver los recorridos. La meta de C cambia a los 10 s.",515,250,410,205,false);
            Card("3  Revisa","Al llegar, agotar el tiempo o detener la prueba, se guardan configuración, métricas, eventos y series temporales CSV.",950,250,410,205,false);
            Text("Enter: iniciar · Espacio / Esc: pausa y continuación · R: repetir desde resultados",80,510,1270,50,heading);
            Text("La flecha del agente muestra su rumbo. La línea que sale del cuerpo indica la referencia de giro. Cada controlador conserva su color y etiqueta.",80,590,1270,70,body);
            Text("El error medio es la media temporal del valor absoluto del error circular respecto a la referencia. En C, llegar a la primera meta no termina el ensayo. El caso exacto de 180° conserva una limitación del modelo PFL3.",80,680,1270,95,small);
            if(Button("Volver al inicio",80,805,330,55,true))app.ChangeScreen(LabScreen.Title);
        }
        void DrawCredits()
        {
            Box(new Rect(0,0,1440,900),background);Header("Créditos y fuentes");Text("NPCs bioinspirados",80,130,1270,65,title);
            Text("Germán Barrena Alonso · Grado de Ingeniería Informática · UOC\nTutor: Daniel Hernández Palomeque",80,235,1270,85,heading);
            Text("Modelo funcional: Mussells Pires et al., Nature 626, 808–818 (2024).\nEcuación, parámetros y organización PFL3: código MaimonLab con licencia MIT.\nConectoma y anotaciones: FlyWire, Dorkenwald et al. y Schlegel et al. (2024).",80,360,1270,135,body);
            Text("El controlador utiliza un modelo publicado de 24 unidades PFL3, adaptado a la navegación del NPC. La extracción de FlyWire se conserva como material de investigación; sus recuentos de sinapsis no se utilizan como pesos del controlador.",80,535,1270,160,small);
            if(Button("Artículo del modelo",80,740,330,55))Application.OpenURL("https://doi.org/10.1038/s41586-023-07006-3");
            if(Button("Volver al inicio",450,740,330,55,true))app.ChangeScreen(LabScreen.Title);
        }
        void HandleKeys()
        {
            Event e=Event.current;if(e.type!=EventType.KeyDown)return;
            if(e.keyCode==KeyCode.Return&&app.ScreenState==LabScreen.Setup){app.StartTrial();e.Use();}
            else if(e.keyCode==KeyCode.Space||e.keyCode==KeyCode.Escape)
            {
                if(app.ScreenState==LabScreen.Running){app.Pause();e.Use();}
                else if(app.ScreenState==LabScreen.Paused){app.Resume();e.Use();}
                else if(e.keyCode==KeyCode.Escape&&(app.ScreenState==LabScreen.Instructions||app.ScreenState==LabScreen.Credits||app.ScreenState==LabScreen.Setup)){app.ChangeScreen(LabScreen.Title);e.Use();}
            }
            else if(e.keyCode==KeyCode.R&&app.ScreenState==LabScreen.Results){app.StartTrial();e.Use();}
        }
    }
}
