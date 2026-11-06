using System.Collections.Generic;
using NpcLab.Core;
using UnityEngine;

namespace NpcLab.Runtime
{
    public sealed class LabWorld
    {
        public static readonly Color Cyan = new Color(0.24f,0.82f,0.88f);
        public static readonly Color Amber = new Color(1f,0.68f,0.27f);
        readonly GameObject root;
        readonly Dictionary<ControllerKind, Transform> bodies = new Dictionary<ControllerKind, Transform>();
        readonly Dictionary<ControllerKind, LineRenderer> trails = new Dictionary<ControllerKind, LineRenderer>();
        readonly Dictionary<ControllerKind, LineRenderer> arrows = new Dictionary<ControllerKind, LineRenderer>();
        readonly Dictionary<ControllerKind, List<Vector3>> points = new Dictionary<ControllerKind, List<Vector3>>();
        readonly List<Material> materials = new List<Material>();
        Transform target;
        Transform arrivalRing;
        readonly Shader shader;
        public readonly Camera Camera;
        public bool ShowTrails = true, ShowReference = true;
        public LabWorld()
        {
            root = new GameObject("Navigation lab world");
            shader = Resources.Load<Shader>("LabSurface");
            if (shader == null) throw new System.InvalidOperationException("Missing Resources/LabSurface.shader.");
            GameObject cameraObject = new GameObject("Lab camera"); cameraObject.transform.SetParent(root.transform);
            Camera = cameraObject.AddComponent<Camera>(); Camera.orthographic = true; Camera.orthographicSize = 13.4f;
            Camera.backgroundColor = new Color(0.035f,0.055f,0.08f); Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.nearClipPlane = 0.1f; Camera.farClipPlane = 100;
            Camera.transform.position = new Vector3(17,25,-21); Camera.transform.LookAt(Vector3.zero);
        }
        Material Material(Color color)
        {
            Material m = new Material(shader); m.color = color; materials.Add(m); return m;
        }
        GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type); go.name = name; go.transform.SetParent(parent);
            go.transform.position = position; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = Material(color);
            Collider collider = go.GetComponent<Collider>(); if (collider != null) Object.Destroy(collider);
            return go;
        }
        LineRenderer Line(string name, Color color, float width, Transform parent)
        {
            GameObject go = new GameObject(name); go.transform.SetParent(parent); LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true; line.sharedMaterial = Material(color); line.startWidth = width; line.endWidth = width;
            line.numCapVertices = 4; line.positionCount = 0; return line;
        }
        public void Rebuild(TrialDefinition definition, IList<ControllerKind> kinds)
        {
            for (int i = root.transform.childCount - 1; i >= 0; --i)
            { Transform t = root.transform.GetChild(i); if (t.gameObject != Camera.gameObject) { t.gameObject.SetActive(false); Object.Destroy(t.gameObject); } }
            foreach (Material m in materials) Object.Destroy(m); materials.Clear();
            bodies.Clear(); trails.Clear(); arrows.Clear(); points.Clear();
            float half = (float)definition.settings.arenaSize / 2;
            Primitive(PrimitiveType.Cube,"Arena",new Vector3(0,-.08f,0),new Vector3(half*2,.12f,half*2),new Color(.09f,.14f,.18f),root.transform);
            for (int i = -(int)half; i <= (int)half; ++i)
            {
                Color c = i == 0 ? new Color(.23f,.31f,.36f) : new Color(.13f,.2f,.24f);
                LineRenderer x = Line("Grid X",c,.018f,root.transform); x.positionCount=2; x.SetPosition(0,new Vector3(-half,0,i));x.SetPosition(1,new Vector3(half,0,i));
                LineRenderer z = Line("Grid Z",c,.018f,root.transform); z.positionCount=2; z.SetPosition(0,new Vector3(i,0,-half));z.SetPosition(1,new Vector3(i,0,half));
            }
            foreach (CircleObstacle o in definition.obstacles)
            {
                Primitive(PrimitiveType.Cylinder,"Obstacle "+o.id,At(o.center,.5f),new Vector3((float)o.radius*2, .5f, (float)o.radius*2),new Color(.30f,.38f,.44f),root.transform);
                Ring("Obstacle clearance",o.center,(float)(o.radius+definition.settings.agentRadius+definition.settings.clearance),new Color(.38f,.46f,.50f));
            }
            Ring("Start",definition.start,.55f,new Color(.35f,.48f,.51f));
            if (definition.scenario == 2) Ring("Future goal",definition.secondGoal,.6f,new Color(.37f,.39f,.23f));
            target = Primitive(PrimitiveType.Cylinder,"Target",At(definition.goal,.13f),new Vector3(.38f,.13f,.38f),new Color(.58f,.92f,.56f),root.transform).transform;
            arrivalRing=Ring("Arrival radius",definition.goal,(float)definition.settings.arrivalRadius,new Color(.58f,.92f,.56f));
            foreach (ControllerKind kind in kinds)
            {
                Color color = kind == ControllerKind.Pfl3 ? Cyan : Amber;
                Transform body = new GameObject(kind.ToString()).transform; body.SetParent(root.transform);
                Primitive(PrimitiveType.Capsule,"Body",Vector3.zero,new Vector3(.48f,.38f,.48f),color,body).transform.localPosition = new Vector3(0,.43f,0);
                Primitive(PrimitiveType.Cube,"Heading",Vector3.zero,new Vector3(.12f,.1f,.38f),Color.white,body).transform.localPosition = new Vector3(0,.84f,.23f);
                body.position=At(definition.start,kind==ControllerKind.Pfl3?.035f:0);
                body.rotation=Quaternion.Euler(0,(float)definition.initialHeading,0);bodies[kind]=body;
                trails[kind]=Line(kind+" trajectory",color,.055f,root.transform);
                arrows[kind]=Line(kind+" reference",new Color(color.r*.65f,color.g*.65f,color.b*.65f),.035f,root.transform);
                points[kind]=new List<Vector3>();
            }
        }
        Transform Ring(string name,Vec2 center,float radius,Color color)
        {
            LineRenderer l=Line(name,color,.035f,root.transform);l.positionCount=65;l.useWorldSpace=false;l.transform.position=At(center,.035f);
            for(int i=0;i<=64;++i){float a=i*Mathf.PI*2/64;l.SetPosition(i,new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*radius);}
            return l.transform;
        }
        public static Vector3 At(Vec2 p,float y=0){return new Vector3((float)p.x,y,(float)p.z);}
        public void Update(IList<TrialEngine> engines)
        {
            foreach(TrialEngine e in engines)
            {
                ControllerKind k=e.Kind;if(!bodies.ContainsKey(k))continue;
                bodies[k].position=At(e.Position,k==ControllerKind.Pfl3?.035f:0);bodies[k].rotation=Quaternion.Euler(0,(float)e.Heading,0);
                List<Vector3> p=points[k];Vector3 now=At(e.Position,k==ControllerKind.Pfl3?.10f:.065f);
                if(p.Count==0||Vector3.Distance(p[p.Count-1],now)>.045f){p.Add(now);trails[k].positionCount=p.Count;trails[k].SetPosition(p.Count-1,now);}
                trails[k].enabled=ShowTrails;
                arrows[k].enabled=ShowReference;arrows[k].positionCount=2;
                arrows[k].SetPosition(0,At(e.Position,.9f));arrows[k].SetPosition(1,At(e.Position,.9f)+At(Angles.Forward(e.ReferenceHeading),0)*1.4f);
                target.position=At(e.Goal,.13f);
                arrivalRing.position=At(e.Goal,.035f);
            }
        }
        public void SetTopView(bool top)
        {Camera.transform.position=top?new Vector3(0,30,0):new Vector3(17,25,-21);Camera.transform.rotation=top?Quaternion.Euler(90,0,0):Quaternion.LookRotation(-Camera.transform.position);}
    }
}
