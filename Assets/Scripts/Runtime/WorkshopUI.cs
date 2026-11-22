using System;
using System.Collections.Generic;
using NpcLab.Core;
using UnityEngine;

namespace NpcLab.Runtime
{
    public sealed partial class LabUI
    {
        enum WorkshopTool { Select, Disc, Rectangle, Polygon }
        WorkshopTool tool;
        readonly List<Vec2> outline = new List<Vec2>();
        bool dragging;
        Vec2 grabOffset, ghost;
        static readonly string[] ToolNames={"Mover pieza","Disco","Bloque rectangular","Polígono libre"};
        ArenaWorkshop Workshop => app.Workshop;

        public void ResetWorkshopTools()
        {
            tool=WorkshopTool.Select; outline.Clear(); dragging=false;
        }

        static Vector2 EditPoint(Vec2 p)
        {
            Rect rect=LabApp.WorkshopArena;
            float pixels=rect.height/21.3f;
            return rect.center+new Vector2((float)p.x*pixels,-(float)p.z*pixels);
        }

        static Vec2 WorldPoint(Vector2 p)
        {
            Rect rect=LabApp.WorkshopArena;
            Vector2 offset=(p-rect.center)*(21.3f/rect.height);
            return new Vec2(Math.Round(offset.x,1),Math.Round(-offset.y,1));
        }

        void Stroke(Vector2 a, Vector2 b, Color color, float width)
        {
            Vector2 delta=b-a;
            if(delta.sqrMagnitude<.01f) return;
            Matrix4x4 old=GUI.matrix;
            GUI.matrix=old*Matrix4x4.TRS(a,Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg),Vector3.one);
            Box(new Rect(0,-width/2,delta.magnitude,width),color);
            GUI.matrix=old;
        }

        void TracePiece(CircleObstacle o, Vec2 center)
        {
            int n=o.shape==ObstacleShape.Disc ? 48 : o.vertices.Length;
            Vector2 first=Vector2.zero,previous=Vector2.zero;
            for(int i=0;i<n;++i)
            {
                Vec2 v=o.shape==ObstacleShape.Disc ? Angles.Forward(i*360.0/n)*o.radius : ObstacleGeometry.Rotate(o.vertices[i],o.rotation);
                Vector2 pt=EditPoint(center+v);
                if(i==0) first=pt; else Stroke(previous,pt,LabWorld.Cyan,3);
                previous=pt;
            }
            Stroke(previous,first,LabWorld.Cyan,3);
        }

        void OpenTool(WorkshopTool next)
        {
            tool=next; dragging=false; outline.Clear(); Workshop.Message="";
            if (next!=WorkshopTool.Select) Workshop.Select(-1);
        }

        void FinishPolygon()
        {
            if(outline.Count<3) { Workshop.Message="Marca al menos tres vértices."; return; }
            Vec2 center=new Vec2();
            foreach(var v in outline) center+=v;
            center/=outline.Count;
            var piece=new CircleObstacle {shape=ObstacleShape.Polygon,center=center,vertices=new Vec2[outline.Count]};
            for(int i=0;i<outline.Count;++i) piece.vertices[i]=outline[i]-center;
            if(Workshop.Place(piece,true)) { OpenTool(WorkshopTool.Select); app.RefreshWorkshop(); }
        }

        void WorkshopMouse()
        {
            Event e=Event.current;
            int id=GUIUtility.GetControlID(73051,FocusType.Passive);
            Vec2 point=WorldPoint(e.mousePosition);
            bool inside=Math.Abs(point.x)<=10 && Math.Abs(point.z)<=10 && LabApp.WorkshopArena.Contains(e.mousePosition);
            if(e.type==EventType.MouseDown && e.button==0 && inside)
            {
                GUI.FocusControl(null);
                if(tool==WorkshopTool.Select)
                {
                    Workshop.Select(Workshop.Hit(point));
                    if(Workshop.Selected!=null)
                    {
                        dragging=true; grabOffset=Workshop.Selected.center-point; ghost=Workshop.Selected.center;
                        GUIUtility.hotControl=id;
                    }
                }
                else if(tool==WorkshopTool.Polygon)
                {
                    if(outline.Count>=3 && Vec2.Distance(point,outline[0])<.35) FinishPolygon();
                    else if(outline.Count>=16) Workshop.Message="Cierra la forma para colocar los 16 vértices.";
                    else { outline.Add(point); Workshop.Message=""; }
                }
                else
                {
                    CircleObstacle piece = tool==WorkshopTool.Disc ? new CircleObstacle(0,point,1) :
                        CircleObstacle.Rectangle(0,point,2.5,1,0);
                    if(Workshop.Place(piece,true)) { OpenTool(WorkshopTool.Select); app.RefreshWorkshop(); }
                }
                e.Use();
            }
            else if (dragging && GUIUtility.hotControl==id && e.type==EventType.MouseDrag)
            { ghost=point+grabOffset; e.Use(); }
            else if (dragging && GUIUtility.hotControl==id && e.type==EventType.MouseUp)
            {
                dragging=false; GUIUtility.hotControl=0;
                var piece=Workshop.Selected.Copy(); piece.center=point+grabOffset;
                if(Workshop.Place(piece,false)) app.RefreshWorkshop();
                e.Use();
            }
        }

        void DrawWorkshop()
        {
            Header("Modo libre   /   Taller de obstáculos");
            Surface(new Rect(24,106,302,770),panel,edge);
            Text("Taller de arena",44,128,266,45,heading);
            Text("Elige una herramienta y pulsa sobre la pista.",45,179,251,70);
            for(int i=0;i<ToolNames.Length;++i)
                if(Button(ToolNames[i],45,270+53*i,258,44,i==(int)tool)) OpenTool((WorkshopTool)i);
            Box(new Rect(44,500,258,2),edge);
            Text("Las piezas tienen el mismo contorno para ambos NPC.",45,519,246,80,small);
            if(Button("Probar con ambos",45,632,258,56,true))
            {
                if(outline.Count>0) Workshop.Message="Cierra el polígono o cancela el dibujo para probar.";
                else { app.PlayWorkshop(); return; }
            }
            if(Button("Guardar diseño",45,705,258,45))
            {
                if(outline.Count>0) Workshop.Message="Cierra el polígono antes de guardar.";
                else Workshop.Save();
            }
            if(Button("Menú principal",45,794,258,44)) { app.ChangeScreen(LabScreen.Title); return; }
            Text("Vista superior",359,101,390,38,heading);
            Text(Workshop.Draft.obstacles.Length+" / 32 piezas",831,109,239,32,caption);
            FrameEdges(LabApp.WorkshopArena,edge,0,2);
            WorkshopMouse();
            MapLabel("Salida",EditPoint(Workshop.Draft.start)+new Vector2(12,5),68);
            MapLabel("Meta",EditPoint(Workshop.Draft.goal)+new Vector2(12,-21),60);
            if (Event.current.type==EventType.Repaint)
            {
                if(Workshop.Selected!=null) TracePiece(Workshop.Selected,dragging ? ghost : Workshop.Selected.center);
                for(int i=0;i<outline.Count;++i)
                {
                    Vector2 v=EditPoint(outline[i]);
                    if(i>0) Stroke(EditPoint(outline[i-1]),v,LabWorld.Amber,3);
                    Rounded(new Rect(v.x-4,v.y-4,8,8),i==0?LabWorld.Cyan:LabWorld.Amber,4);
                }
                if(outline.Count>0 && LabApp.WorkshopArena.Contains(Event.current.mousePosition))
                    Stroke(EditPoint(outline[outline.Count-1]),Event.current.mousePosition,LabWorld.Amber,2);
            }
            Surface(new Rect(1085,106,331,681),panel,edge);
            DrawPieceSettings();
            Surface(new Rect(344,806,1072,70),panel,edge);
            Text(string.IsNullOrEmpty(Workshop.Message) ? WorkshopHint() : Workshop.Message,364,826,1027,42,body);
        }

        string WorkshopHint()
        {
            switch(tool)
            {
                case WorkshopTool.Disc: return "Pulsa en la pista para colocar el centro del disco.";
                case WorkshopTool.Rectangle: return "Pulsa para colocar un bloque. Ajusta sus dos lados y el giro en el inspector.";
                case WorkshopTool.Polygon: return "Marca los vértices en orden. Cierra la forma pulsando el primer punto o «Cerrar polígono».";
                default: return "Arrastra una pieza para moverla. Pulsa sobre ella para cambiar su tamaño o su giro.";
            }
        }

        void DrawPieceSettings()
        {
            Text("Inspector",1104,130,283,44,heading);
            if(tool==WorkshopTool.Polygon)
            {
                Text("Polígono libre",1104,205,289,44,heading);
                Text(outline.Count+" de 16 vértices",1105,264,280,38,readout);
                if(Button("Cerrar polígono",1105,343,287,48,true)) FinishPolygon();
                if(Button("Deshacer vértice",1105,408,287,44) && outline.Count>0)
                { outline.RemoveAt(outline.Count-1); Workshop.Message=""; }
                if(Button("Cancelar dibujo",1105,467,287,42)) OpenTool(WorkshopTool.Select);
                return;
            }
            CircleObstacle o=Workshop.Selected;
            if(o==null)
            {
                Text("Selecciona una pieza para editar sus dimensiones.",1105,205,275,110);
                Text("Escala",1105,367,260,30,small);
                Text("1 casilla = 1 m",1105,405,282,44,readout);
                return;
            }
            string name=o.shape==ObstacleShape.Disc ? "Disco" : o.shape==ObstacleShape.Rectangle ? "Bloque" : "Polígono";
            Text(name+" "+(o.id+1),1104,191,286,45,heading);
            Text("Centro  "+F(o.center.x,"0.0")+", "+F(o.center.z,"0.0")+" m",1105,246,287,35,small);
            if(o.shape==ObstacleShape.Disc)
            {
                int delta=Adjustment("Radio",F(o.radius,"0.00")+" m",309);
                if(delta!=0)
                {
                    var copy=o.Copy(); copy.radius=Angles.Clamp(o.radius+delta*.25,.25,9.5);
                    if(Workshop.Place(copy,false)) app.RefreshWorkshop();
                }
            }
            else
            {
                double minX=double.MaxValue,maxX=double.MinValue,minZ=double.MaxValue,maxZ=double.MinValue;
                foreach(Vec2 v in o.vertices)
                { minX=Math.Min(minX,v.x); maxX=Math.Max(maxX,v.x); minZ=Math.Min(minZ,v.z); maxZ=Math.Max(maxZ,v.z); }
                double width=maxX-minX, depth=maxZ-minZ;
                int dw=Adjustment("Ancho",F(width,"0.00")+" m",289);
                int dh=Adjustment("Fondo",F(depth,"0.00")+" m",389);
                int dr=Adjustment("Giro",Degrees(o.rotation),489);
                if(dw!=0||dh!=0||dr!=0)
                {
                    var copy=o.Copy(); copy.rotation=Angles.Wrap(copy.rotation+dr*15);
                    double sx=Angles.Clamp(width+dw*.25,.25,19.5)/width, sz=Angles.Clamp(depth+dh*.25,.25,19.5)/depth;
                    for(int i=0;i<copy.vertices.Length;++i) copy.vertices[i]=new Vec2(copy.vertices[i].x*sx,copy.vertices[i].z*sz);
                    if(Workshop.Place(copy,false)) app.RefreshWorkshop();
                }
            }
            if(Button("Eliminar pieza",1105,684,287,47))
            { Workshop.Remove(); app.RefreshWorkshop(); }
        }

        int Adjustment(string label,string value,float y)
        {
            Text(label,1105,y,270,28,small);
            bool less=Button("−",1105,y+31,53,44);
            Text(value,1182,y+39,136,35,readout);
            bool more=Button("+",1339,y+31,53,44);
            return less ? -1 : more ? 1 : 0;
        }
    }
}
