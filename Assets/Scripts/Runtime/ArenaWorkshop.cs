using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NpcLab.Core;
using UnityEngine;

namespace NpcLab.Runtime
{
    public sealed class ArenaWorkshop
    {
        public TrialDefinition Draft { get; private set; }
        public int SelectedIndex { get; private set; } = -1;
        public CircleObstacle Selected => SelectedIndex < 0 ? null : Draft.obstacles[SelectedIndex];
        public string Message { get; set; }
        public bool HasSave => File.Exists(path);
        public ScenarioKind Kind { get; }
        readonly string path;

        public ArenaWorkshop(string folder, ScenarioKind kind)
        {
            Kind = kind;
            bool relay = kind == ScenarioKind.MovingGoal;
            path = Path.Combine(folder, relay ? "arena-c.json" : "arena-b.json");
            Draft = relay ? ArenaCourses.RelayWorkshop() : ArenaCourses.EmptyWorkshop();
            if (HasSave) Load();
        }

        public bool MoveGoal(int stage, Vec2 point)
        {
            var d = Draft.Copy();
            double limit = d.settings.arenaSize / 2 - d.settings.agentRadius;
            if (Math.Abs(point.x) > limit || Math.Abs(point.z) > limit)
            {
                Message = "Coloca el centro de la baliza dentro del borde de la pista.";
                return false;
            }
            if (stage == 0) d.goal = point;
            else if (stage == 1) d.secondGoal = point;
            else d.thirdGoal = point;
            d.Validate();
            Draft = d;
            Message = "Baliza " + (stage+1) + " colocada.";
            return true;
        }

        public void ChangeTime(int stage, int delta)
        {
            var d = Draft.Copy();
            double first = d.goalChangeSeconds, last = d.secondChangeSeconds;
            if (stage == 1) first += delta;
            else last += delta;
            if (first < 1 || last <= first || last >= d.settings.maxSeconds)
            {
                Message = "Cada relevo debe activarse después del anterior y antes de los 60 s.";
                return;
            }
            d.goalChangeSeconds = first;
            d.secondChangeSeconds = last;
            d.Validate();
            Draft = d;
            Message = "Horario de las balizas actualizado.";
        }

        public void Select(int index) { SelectedIndex = index; Message = ""; }

        public int Hit(Vec2 point)
        {
            for (int i=Draft.obstacles.Length-1; i>=0; --i)
                if (ObstacleGeometry.Distance(Draft.obstacles[i],point) < .15) return i;
            return -1;
        }

        public bool Place(CircleObstacle piece, bool adding)
        {
            var candidate = Draft.Copy();
            var list = new List<CircleObstacle>(candidate.obstacles);
            if (adding)
            {
                if (list.Count>=32) { Message="Has colocado las 32 piezas disponibles."; return false; }
                int id=0;
                foreach (var other in list) id=Math.Max(id,other.id+1);
                piece.id=id;
                list.Add(piece);
            }
            else if (Selected == null) return false;
            else list[SelectedIndex]=piece;
            candidate.obstacles=list.ToArray();
            try { candidate.Validate(); }
            catch (ArgumentException ex) { Message=ex.Message; return false; }
            Draft=candidate;
            if (adding) SelectedIndex=list.Count-1;
            Message="";
            return true;
        }

        public void Remove()
        {
            if (Selected == null) return;
            var list=new List<CircleObstacle>(Draft.obstacles);
            list.RemoveAt(SelectedIndex);
            Draft.obstacles=list.ToArray();
            Select(-1);
        }

        public void Save()
        {
            string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(temp,JsonText.Configuration(Draft),new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp,path,null);
                else File.Move(temp,path);
                Message="Diseño guardado en este equipo.";
            }
            catch(Exception ex) { Message="No se pudo guardar. "+ex.Message; }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public bool Load()
        {
            try
            {
                var d=JsonUtility.FromJson<TrialDefinition>(File.ReadAllText(path));
                if(d==null || d.datasetRole!="free-workshop" || d.scenario!=(int)Kind || d.obstacles==null || d.obstacles.Length>32 ||
                    (Kind==ScenarioKind.MovingGoal && (!d.HasThirdGoal || d.obstacles.Length!=0)))
                    throw new ArgumentException("Archivo de taller no válido.");
                d.Validate(); Draft=d; Select(-1); Message="Diseño recuperado."; return true;
            }
            catch(Exception ex) { Message="No se pudo abrir el diseño. "+ex.Message; return false; }
        }

        public TrialDefinition Snapshot()
        {
            var copy=Draft.Copy();
            using(var hash=SHA256.Create())
            {
                byte[] digest=hash.ComputeHash(Encoding.UTF8.GetBytes(JsonText.Configuration(copy)));
                copy.configurationId=(Kind==ScenarioKind.MovingGoal ? "C-TALLER-" : "B-TALLER-")+BitConverter.ToString(digest,0,5).Replace("-","");
            }
            return copy;
        }
    }
}
