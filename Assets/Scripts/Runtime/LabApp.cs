using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NpcLab.Core;
using UnityEngine;

namespace NpcLab.Runtime
{
    public enum LabScreen { Title, Setup, Running, Paused, Results, Instructions, Credits }
    public sealed class LabApp : MonoBehaviour
    {
        public LabScreen ScreenState {get;private set;}
        public readonly List<TrialEngine> Engines=new List<TrialEngine>();
        public TrialDefinition Selected {get;private set;}
        public LabWorld World {get;private set;}
        public string Notice="", ExportFolder="";
        public int ScenarioIndex, ConfigurationIndex, ControllerSelection=2, VisualSpeed=1;
        public bool TopView, Trajectories=true, ReferenceArrows=true;
        public string HeadingInput="";
        bool exported;
        LabUI ui;
        public static readonly string[] ScenarioNames={"A · Espacio abierto","B · Obstáculos fijos","C · Cambio de meta"};
        public static readonly string[] ControllerNames={"Proporcional","PFL3","Comparación"};
        void Awake()
        {
            Application.runInBackground=true;Time.fixedDeltaTime=.02f;Time.timeScale=1;
            World=new LabWorld();ui=new LabUI(this);ScreenState=LabScreen.Title;LoadSelection();
        }
        public void LoadSelection()
        {
            ConfigurationIndex=Mathf.Clamp(ConfigurationIndex,0,29);ScenarioIndex=Mathf.Clamp(ScenarioIndex,0,2);
            string id=((char)('A'+ScenarioIndex)).ToString()+"-"+ConfigurationIndex.ToString("D2",CultureInfo.InvariantCulture);
            TextAsset asset=Resources.Load<TextAsset>("Configurations/"+id);
            if(asset==null)throw new InvalidOperationException("Missing saved configuration "+id);
            Selected=JsonUtility.FromJson<TrialDefinition>(asset.text);Selected.Validate();
            HeadingInput=Selected.initialHeading.ToString("0.###",CultureInfo.InvariantCulture);Notice="";
            Preview();
        }
        public void Preview()
        {
            List<ControllerKind> kinds=Kinds();World.Rebuild(Selected,kinds);World.SetTopView(TopView);
        }
        List<ControllerKind> Kinds()
        {
            if(ControllerSelection==0)return new List<ControllerKind>{ControllerKind.Proportional};
            if(ControllerSelection==1)return new List<ControllerKind>{ControllerKind.Pfl3};
            return new List<ControllerKind>{ControllerKind.Proportional,ControllerKind.Pfl3};
        }
        public void StartTrial()
        {
            try {
                TrialDefinition d=Selected.Copy();double h;
                if(!double.TryParse(HeadingInput.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out h)||!Angles.Finite(h)||h< -180||h>180)
                {Notice="El rumbo debe estar entre −180 y 180 grados.";return;}
                if(Math.Abs(Angles.Wrap(h-d.initialHeading))>.001){d.initialHeading=h;d.configurationId+="-custom";d.datasetRole="development-custom";}
                d.Validate();Engines.Clear();foreach(ControllerKind k in Kinds()){TrialEngine e=new TrialEngine(d,k);e.Start();Engines.Add(e);}
                World.Rebuild(d,Kinds());exported=false;ExportFolder="";Notice="";Time.timeScale=VisualSpeed;ScreenState=LabScreen.Running;
            }catch(Exception ex){Notice="No se pudo iniciar: "+ex.Message;Debug.LogException(ex);}
        }
        void FixedUpdate()
        {
            if(ScreenState!=LabScreen.Running)return;
            bool finished=true;foreach(TrialEngine e in Engines){e.Step();if(!e.Finished)finished=false;}
            if(finished){ScreenState=LabScreen.Results;Time.timeScale=1;SaveResults();}
        }
        void Update()
        {
            if(World==null)return;
            float scale=Mathf.Min(Screen.width/1440f,Screen.height/900f);
            float offsetX=(Screen.width-1440*scale)/2,offsetY=(Screen.height-900*scale)/2;
            World.Camera.rect=new Rect((offsetX+320*scale)/Screen.width,(offsetY+190*scale)/Screen.height,1100*scale/Screen.width,610*scale/Screen.height);
            World.ShowTrails=Trajectories;World.ShowReference=ReferenceArrows;
            if(Engines.Count>0&&(ScreenState==LabScreen.Running||ScreenState==LabScreen.Paused||ScreenState==LabScreen.Results))World.Update(Engines);
        }
        public void Pause()
        {foreach(TrialEngine e in Engines)e.Pause();ScreenState=LabScreen.Paused;Time.timeScale=1;}
        public void Resume()
        {foreach(TrialEngine e in Engines)e.Resume();ScreenState=LabScreen.Running;Time.timeScale=VisualSpeed;}
        public void ChangeScreen(LabScreen screen)
        {ScreenState=screen;Time.timeScale=1;if(screen==LabScreen.Setup)Preview();}
        public void AbortToResults()
        {foreach(TrialEngine e in Engines)e.Abort();ScreenState=LabScreen.Results;Time.timeScale=1;SaveResults();}
        public void SaveResults()
        {
            if(exported||Engines.Count==0)return;
            try {
                if(string.IsNullOrEmpty(ExportFolder))ExportFolder=Path.Combine(Application.persistentDataPath,"Trials",DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff",CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString("N").Substring(0,6));
                TrialExport.Write(ExportFolder,Engines,"Unity "+Application.unityVersion+"; "+(Application.isEditor?"Editor":"Player")+"; "+SystemInfo.operatingSystem+"; "+SystemInfo.processorType+"; visual speed "+VisualSpeed);
                exported=true;Notice="Resultados guardados. Usa «Abrir carpeta» para ver los CSV.";
            }catch(Exception ex){Notice="No se pudo guardar. Puedes reintentar: "+ex.Message;Debug.LogException(ex);}
        }
        public void OpenResults()
        {if(!string.IsNullOrEmpty(ExportFolder))Application.OpenURL(new Uri(ExportFolder).AbsoluteUri);}
        void OnGUI(){if(ui!=null)ui.Draw();}
        void OnApplicationQuit(){foreach(TrialEngine e in Engines)if(!e.Finished)e.Abort();SaveResults();Time.timeScale=1;}
        void OnDisable(){Time.timeScale=1;}
    }
}
