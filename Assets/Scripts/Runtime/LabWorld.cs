using System.Collections.Generic;
using NpcLab.Core;
using UnityEngine;

namespace NpcLab.Runtime
{
    public sealed class LabWorld
    {
        public static readonly Color Cyan = new Color32(77, 218, 215, 255);
        public static readonly Color Amber = new Color32(255, 178, 91, 255);
        readonly GameObject root;
        readonly Dictionary<ControllerKind, Transform> bodies = new Dictionary<ControllerKind, Transform>();
        readonly Dictionary<ControllerKind, LineRenderer> trails = new Dictionary<ControllerKind, LineRenderer>();
        readonly Dictionary<ControllerKind, LineRenderer> arrows = new Dictionary<ControllerKind, LineRenderer>();
        readonly Dictionary<ControllerKind, List<Vector3>> points = new Dictionary<ControllerKind, List<Vector3>>();
        readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        Transform target;
        Transform arrivalRing, inactiveGoal;
        public ControllerKind? OnlyController { get; set; }
        public bool Shows(ControllerKind kind) => !OnlyController.HasValue || OnlyController.Value == kind;
        LineRenderer startingArrow;
        readonly Shader shader;
        readonly Camera backgroundCamera;
        public readonly Camera Camera;
        public bool ShowTrails = true, ShowReference = true;
        public LabWorld()
        {
            root = new GameObject("Navigation lab world");
            shader = Resources.Load<Shader>("LabSurface");
            if (shader == null) throw new System.InvalidOperationException("Missing Resources/LabSurface.shader.");
            // Clear the full frame before drawing the scene viewport and the interface.
            GameObject backgroundObject = new GameObject("Lab background"); backgroundObject.transform.SetParent(root.transform);
            backgroundCamera = backgroundObject.AddComponent<Camera>();
            backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
            backgroundCamera.backgroundColor = new Color32(13, 22, 27, 255);
            backgroundCamera.cullingMask = 0;
            backgroundCamera.depth = -100;
            backgroundCamera.rect = new Rect(0,0,1,1);
            GameObject cameraObject = new GameObject("Lab camera"); cameraObject.transform.SetParent(root.transform);
            Camera = cameraObject.AddComponent<Camera>(); Camera.orthographic = true; Camera.orthographicSize = 13.4f;
            Camera.depth = 0;
            Camera.backgroundColor = new Color32(13, 22, 27, 255); Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.nearClipPlane = 0.1f; Camera.farClipPlane = 100;
            Camera.transform.position = new Vector3(17,25,-21); Camera.transform.LookAt(Vector3.zero);
        }
        Material Material(Color color)
        {
            if (materials.TryGetValue(color, out Material material)) return material;
            material = new Material(shader) { color = color };
            materials.Add(color, material);
            return material;
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
            { Transform t = root.transform.GetChild(i); if (t.gameObject != Camera.gameObject && t.gameObject != backgroundCamera.gameObject) { t.gameObject.SetActive(false); Object.Destroy(t.gameObject); } }
            foreach (Material m in materials.Values) Object.Destroy(m); materials.Clear();
            bodies.Clear(); trails.Clear(); arrows.Clear(); points.Clear();
            startingArrow = null;
            float half = (float)definition.settings.arenaSize / 2;
            Primitive(PrimitiveType.Cube,"Arena",new Vector3(0,-.08f,0),new Vector3(half*2,.12f,half*2),new Color32(42, 62, 68, 255),root.transform);
            for (int i = -(int)half; i <= (int)half; ++i)
            {
                bool major = i % 5 == 0;
                Color c = major ? new Color32(80, 107, 110, 255) : new Color32(55, 77, 81, 255);
                float width = major ? .031f : .019f;
                LineRenderer x = Line("Grid X",c,width,root.transform); x.positionCount=2; x.SetPosition(0,new Vector3(-half,0,i));x.SetPosition(1,new Vector3(half,0,i));
                LineRenderer z = Line("Grid Z",c,width,root.transform); z.positionCount=2; z.SetPosition(0,new Vector3(i,0,-half));z.SetPosition(1,new Vector3(i,0,half));
            }
            LineRenderer border = Line("Arena boundary", new Color32(104, 139, 139, 255), .055f, root.transform);
            border.positionCount = 5;
            border.SetPositions(new[] { new Vector3(-half,.025f,-half), new Vector3(half,.025f,-half),
                new Vector3(half,.025f,half), new Vector3(-half,.025f,half), new Vector3(-half,.025f,-half) });
            foreach (CircleObstacle o in definition.obstacles)
            {
                Primitive(PrimitiveType.Cylinder,"Obstacle "+o.id,At(o.center,.5f),new Vector3((float)o.radius*2, .5f, (float)o.radius*2),new Color32(100, 120, 128, 255),root.transform);
                Ring("Obstacle rim",o.center,(float)o.radius,new Color32(188, 199, 195, 255)).localPosition = At(o.center,1.01f);
                Ring("Obstacle clearance",o.center,(float)(o.radius+definition.settings.agentRadius+definition.settings.clearance),new Color(.38f,.46f,.50f));
            }
            Ring("Start",definition.start,.55f,new Color(.35f,.48f,.51f));
            inactiveGoal = definition.scenario == (int)ScenarioKind.MovingGoal
                ? Ring("Inactive goal", definition.secondGoal, .55f, new Color32(139, 155, 133, 255)) : null;
            target = Primitive(PrimitiveType.Cylinder,"Target",At(definition.goal,.13f),new Vector3(.38f,.13f,.38f),new Color(.58f,.92f,.56f),root.transform).transform;
            arrivalRing=Ring("Arrival radius",definition.goal,(float)definition.settings.arrivalRadius,new Color(.58f,.92f,.56f));
            foreach (ControllerKind kind in kinds)
            {
                Color color = kind == ControllerKind.Pfl3 ? Cyan : Amber;
                Transform body = new GameObject(kind.ToString()).transform; body.SetParent(root.transform);
                BuildFly(body, color);
                body.position=At(definition.start,kind==ControllerKind.Pfl3?.035f:0);
                body.rotation=Quaternion.Euler(0,(float)definition.initialHeading,0);bodies[kind]=body;
                // Different widths leave both traces visible when the paths coincide.
                float trailWidth = kinds.Count == 1 ? .085f : kind == ControllerKind.Proportional ? .20f : .072f;
                trails[kind]=Line(kind+" trajectory",color,trailWidth,root.transform);
                arrows[kind]=Line(kind+" reference",new Color(color.r*.65f,color.g*.65f,color.b*.65f),.035f,root.transform);
                points[kind]=new List<Vector3>();
            }
        }
        void BuildFly(Transform parent, Color color)
        {
            Color dark = Color.Lerp(color, new Color32(23, 34, 36, 255), .57f);
            FlyPart(parent, "Abdomen", new Vector3(0,.30f,-.17f), new Vector3(.24f,.20f,.44f), dark);
            FlyPart(parent, "Thorax", new Vector3(0,.37f,.08f), new Vector3(.29f,.24f,.32f), color);
            FlyPart(parent, "Head", new Vector3(0,.38f,.29f), new Vector3(.21f,.15f,.16f), dark);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform wing = FlyPart(parent, "Wing", new Vector3(side*.135f,.40f,-.15f),
                    new Vector3(.14f,.035f,.48f), Color.Lerp(color, Color.white, .5f));
                wing.localRotation = Quaternion.Euler(0, -side*22, 0);
                FlyPart(parent, "Eye", new Vector3(side*.093f,.397f,.30f), new Vector3(.065f,.07f,.09f), color);
            }
        }

        Transform FlyPart(Transform parent, string name, Vector3 offset, Vector3 size, Color color)
        {
            Transform part = Primitive(PrimitiveType.Sphere, name, Vector3.zero, size, color, parent).transform;
            part.localPosition = offset;
            return part;
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
                bool visible = Shows(k);
                if (bodies[k].gameObject.activeSelf != visible) bodies[k].gameObject.SetActive(visible);
                bodies[k].position=At(e.Position,k==ControllerKind.Pfl3?.035f:0);bodies[k].rotation=Quaternion.Euler(0,(float)e.Heading,0);
                List<Vector3> p=points[k];Vector3 now=At(e.Position,k==ControllerKind.Pfl3?.10f:.065f);
                if(p.Count==0||Vector3.Distance(p[p.Count-1],now)>.045f){p.Add(now);trails[k].positionCount=p.Count;trails[k].SetPosition(p.Count-1,now);}
                trails[k].enabled=ShowTrails && visible;
                arrows[k].enabled=ShowReference && visible;arrows[k].positionCount=2;
                arrows[k].SetPosition(0,At(e.Position,.9f));arrows[k].SetPosition(1,At(e.Position,.9f)+At(Angles.Forward(e.ReferenceHeading),0)*1.4f);
            }
            TrialEngine first = engines[0];
            target.position = At(first.Goal,.13f);
            arrivalRing.position = At(first.Goal,.035f);
            if (inactiveGoal != null)
                inactiveGoal.position = At(first.GoalSwitched ? first.Definition.goal : first.Definition.secondGoal,.035f);
        }
        public void SetStartingHeading(Vec2 start, double heading)
        {
            Quaternion rotation = Quaternion.Euler(0, (float)heading, 0);
            foreach (Transform body in bodies.Values) body.rotation = rotation;
            if (startingArrow == null)
            {
                startingArrow = Line("Rumbo de salida", Color.white, .18f, root.transform);
                startingArrow.positionCount = 5;
            }
            Vector3 origin = At(start, 1.05f);
            Vector3 forward = At(Angles.Forward(heading));
            Vector3 side = new Vector3(forward.z, 0, -forward.x);
            Vector3 tip = origin + 2.4f * forward;
            Vector3 back = tip - .56f * forward;
            startingArrow.SetPosition(0, origin);
            startingArrow.SetPosition(1, tip);
            startingArrow.SetPosition(2, back + .29f * side);
            startingArrow.SetPosition(3, tip);
            startingArrow.SetPosition(4, back - .29f * side);
        }
        public void Dispose()
        {
            foreach (Material material in materials.Values) Object.Destroy(material);
            if (root != null) Object.Destroy(root);
        }

        public void SetTopView(bool top)
        {Camera.transform.position=top?new Vector3(0,30,0):new Vector3(17,25,-21);Camera.transform.rotation=top?Quaternion.Euler(90,0,0):Quaternion.LookRotation(-Camera.transform.position);}
    }
}
