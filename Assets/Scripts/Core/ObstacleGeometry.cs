using System;
using System.Collections.Generic;

namespace NpcLab.Core
{
    public static class ObstacleGeometry
    {
        const double Epsilon = 1e-9;
        static double Cross(Vec2 a, Vec2 b) => a.x * b.z - a.z * b.x;

        public static Vec2 Rotate(Vec2 p, double degrees)
        {
            double angle = degrees * Angles.ToRadians;
            double sin = Math.Sin(angle), cos = Math.Cos(angle);
            return new Vec2(cos*p.x + sin*p.z, -sin*p.x + cos*p.z);
        }

        public static Vec2 WorldVertex(CircleObstacle o, int i) => o.center + Rotate(o.vertices[i], o.rotation);

        static double EdgeDistance(Vec2 p, Vec2 a, Vec2 b)
        {
            Vec2 edge = b - a;
            double t = Angles.Clamp(Vec2.Dot(p - a, edge) / Vec2.Dot(edge, edge), 0, 1);
            return Vec2.Distance(p, a + edge * t);
        }

        public static double Distance(CircleObstacle o, Vec2 world)
        {
            if (o.shape == ObstacleShape.Disc) return Math.Max(0, Vec2.Distance(world, o.center) - o.radius);
            Vec2 p = Rotate(world - o.center, -o.rotation);
            double closest = double.MaxValue;
            bool inside = false;
            for (int i = 0; i < o.vertices.Length; ++i)
            {
                Vec2 a = o.vertices[i], b = o.vertices[(i+1) % o.vertices.Length];
                closest = Math.Min(closest, EdgeDistance(p, a, b));
                if ((a.z > p.z) != (b.z > p.z) && p.x < (b.x-a.x)*(p.z-a.z)/(b.z-a.z)+a.x) inside = !inside;
            }
            return inside ? 0 : closest;
        }

        static bool OnEdge(Vec2 p, Vec2 a, Vec2 b) =>
            Math.Abs(Cross(b-a,p-a)) <= Epsilon && Vec2.Dot(p-a,p-b) <= Epsilon;

        static bool EdgesMeet(Vec2 a, Vec2 b, Vec2 c, Vec2 d) =>
            (Cross(b-a,c-a)*Cross(b-a,d-a) < 0 && Cross(d-c,a-c)*Cross(d-c,b-c) < 0) ||
            OnEdge(c,a,b) || OnEdge(d,a,b) || OnEdge(a,c,d) || OnEdge(b,c,d);

        public static void Validate(CircleObstacle o, double half)
        {
            if (!Angles.Finite(o.center.x) || !Angles.Finite(o.center.z) || !Angles.Finite(o.rotation))
                throw new ArgumentException("Posición o giro no válido.");
            if (o.shape == ObstacleShape.Disc)
            {
                if (!Angles.Finite(o.radius) || o.radius <= 0) throw new ArgumentException("El disco necesita un radio positivo.");
                if (Math.Abs(o.center.x)+o.radius > half || Math.Abs(o.center.z)+o.radius > half)
                    throw new ArgumentException("La pieza debe quedar dentro de la arena.");
                return;
            }
            if ((o.shape != ObstacleShape.Rectangle && o.shape != ObstacleShape.Polygon) ||
                o.vertices == null || o.vertices.Length < 3 || o.vertices.Length > 16)
                throw new ArgumentException("Usa entre 3 y 16 vértices por pieza.");
            double area = 0;
            for (int i=0; i<o.vertices.Length; ++i)
            {
                int next=(i+1)%o.vertices.Length;
                Vec2 a=o.vertices[i], b=o.vertices[next], c=o.vertices[(next+1)%o.vertices.Length], world=WorldVertex(o,i);
                if (!Angles.Finite(world.x) || !Angles.Finite(world.z) || Math.Abs(world.x)>half+Epsilon || Math.Abs(world.z)>half+Epsilon)
                    throw new ArgumentException("La pieza debe quedar dentro de la arena.");
                if (Vec2.Distance(a,b)<.035 || Math.Abs(Cross(b-a,c-b))<Epsilon)
                    throw new ArgumentException("Separa los vértices y evita tres seguidos en línea recta.");
                area += Cross(a,b);
                for (int j=i+2; j<o.vertices.Length; ++j)
                {
                    int k=(j+1)%o.vertices.Length;
                    if (k!=i && EdgesMeet(a,b,o.vertices[j],o.vertices[k]))
                        throw new ArgumentException("Los lados del polígono no pueden cruzarse.");
                }
            }
            if (Math.Abs(area)<.04) throw new ArgumentException("Dibuja una pieza con más superficie.");
        }

        // Sweep the centre of a moving disc against the edge strips and rounded corners.
        public static bool Sweep(CircleObstacle o, Vec2 from, Vec2 to, double padding, out double fraction)
        {
            if (o.shape == ObstacleShape.Disc) return Geometry.SegmentCircle(from,to,o.center,o.radius+padding,out fraction);
            Vec2 p=Rotate(from-o.center,-o.rotation), end=Rotate(to-o.center,-o.rotation), motion=end-p;
            fraction=1;
            bool found=false;
            for(int i=0;i<o.vertices.Length;++i)
            {
                Vec2 a=o.vertices[i], b=o.vertices[(i+1)%o.vertices.Length], edge=b-a;
                double length=edge.Length;
                Vec2 along=edge/length, normal=new Vec2(-along.z,along.x);
                double signed=Vec2.Dot(p-a,normal), approach=Vec2.Dot(motion,normal);
                if (Math.Abs(approach)>Epsilon && signed*approach < 0)
                {
                    double t=Math.Max(0,(Math.Sign(signed)*padding-signed)/approach);
                    double span=Vec2.Dot(p+motion*t-a,along);
                    if(t<=fraction && span>=0 && span<=length) { fraction=t; found=true; }
                }
                double corner;
                if (Geometry.SegmentCircle(p,end,a,padding,out corner) && corner<=fraction) { fraction=corner; found=true; }
            }
            return found;
        }

        public static void Silhouette(CircleObstacle o, Vec2 position, double padding, out double left, out double right)
        {
            Vec2 toward=o.center-position;
            double middle=Angles.Heading(toward);
            if (o.shape == ObstacleShape.Disc)
            {
                double r=o.radius+padding;
                double offset=Math.Asin(Angles.Clamp(r/Math.Max(r,toward.Length),0,1))/Angles.ToRadians+8;
                left=middle-offset; right=middle+offset;
                return;
            }
            double lo=180, hi=-180;
            for(int i=0;i<o.vertices.Length;++i)
            {
                Vec2 delta=WorldVertex(o,i)-position;
                double angle=Angles.Wrap(Angles.Heading(delta)-middle);
                double arc=Math.Asin(Angles.Clamp(padding/Math.Max(padding,delta.Length),0,1))/Angles.ToRadians;
                lo=Math.Min(lo,angle-arc); hi=Math.Max(hi,angle+arc);
            }
            left=middle+lo-8; right=middle+hi+8;
        }

        // Ear clipping also preserves the footprint of concave, hand-drawn pieces.
        public static int[] Triangles(Vec2[] vertices)
        {
            var remaining = new List<int>();
            double area=0;
            for(int i=0;i<vertices.Length;++i) { remaining.Add(i); area+=Cross(vertices[i],vertices[(i+1)%vertices.Length]); }
            if (area<0) remaining.Reverse();
            var triangles=new List<int>();
            while(remaining.Count>2)
            {
                bool clipped=false;
                for(int i=0;i<remaining.Count;++i)
                {
                    int a=remaining[(i+remaining.Count-1)%remaining.Count], b=remaining[i], c=remaining[(i+1)%remaining.Count];
                    Vec2 p=vertices[a], q=vertices[b], r=vertices[c];
                    if (Cross(q-p,r-q)<=Epsilon) continue;
                    bool occupied=false;
                    foreach(int j in remaining)
                    {
                        if(j==a || j==b || j==c) continue;
                        Vec2 v=vertices[j];
                        if(Cross(q-p,v-p)>=-Epsilon && Cross(r-q,v-q)>=-Epsilon && Cross(p-r,v-r)>=-Epsilon)
                        { occupied=true; break; }
                    }
                    if(occupied) continue;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    remaining.RemoveAt(i); clipped=true; break;
                }
                if(!clipped) throw new ArgumentException("No se puede cerrar ese contorno.");
            }
            return triangles.ToArray();
        }
    }
}
