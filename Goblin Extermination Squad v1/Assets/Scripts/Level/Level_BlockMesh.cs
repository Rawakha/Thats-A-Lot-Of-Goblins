using System;
using System.Collections.Generic;
using UnityEngine;

// Reusable closed low-poly stone: six broad faces plus eight clipped-corner facets. All adjoining faces share
// identical positions, with split normals for flat shading. Pivot is bottom-centre; unit size is 1 x 1 x 1.
// Mesh/list allocations are authoring-only. No runtime calls or subscriptions.
public static class Level_BlockMesh
{
    public enum Shape { Stone, PointedCap }
    public sealed class Data
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector3> normals = new List<Vector3>();
        public readonly List<Color> colours = new List<Color>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();
        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colours); mesh.SetUVs(0,uv);
            mesh.SetTriangles(triangles,0); mesh.RecalculateBounds(); return mesh;
        }
    }
    public static Data Build(float cornerCut, float variation, float taper, float lean, Color colour, float shadeVariation, float sideDarkening, int seed, Shape shape)
    {
        if (!float.IsFinite(cornerCut) || !float.IsFinite(variation) || !float.IsFinite(taper) || !float.IsFinite(lean) ||
            !float.IsFinite(shadeVariation) || !float.IsFinite(sideDarkening) ||
            !float.IsFinite(colour.r) || !float.IsFinite(colour.g) || !float.IsFinite(colour.b) || !float.IsFinite(colour.a))
            throw new ArgumentException("Stone shape and colour values must be finite.");
        cornerCut = Mathf.Clamp(cornerCut,0,0.35f); variation = Mathf.Clamp01(variation);
        taper = Mathf.Clamp(taper,0,0.2f); lean = Mathf.Clamp(lean,0,0.12f);
        var random = new System.Random(seed); var data = new Data();
        float shade = 1+((float)random.NextDouble()*2-1)*Mathf.Clamp(shadeVariation,0,0.3f);
        Vector2 shear = new Vector2(((float)random.NextDouble()*2-1)*lean,((float)random.NextDouble()*2-1)*lean);
        float[] cuts = new float[8];
        for (int i=0;i<8;i++) cuts[i] = cornerCut > 0 ? Mathf.Clamp(cornerCut*(1+((float)random.NextDouble()*2-1)*variation),0.001f,0.45f) : 0;
        Vector3 Deform(Vector3 p) => new Vector3(p.x*(1-taper*p.y)+shear.x*p.y,p.y,p.z*(1-taper*p.y)+shear.y*p.y);
        void Face(Vector3[] points, Vector3 facing)
        {
            for (int i=0;i<points.Length;i++) points[i] = Deform(points[i]);
            Vector3 normal = Vector3.Cross(points[1]-points[0],points[2]-points[0]).normalized;
            if (Vector3.Dot(normal,facing)<0) { Array.Reverse(points); normal = -normal; }
            Color tint = colour * (shade*(1-Mathf.Clamp(sideDarkening,0,0.4f)*(1-Mathf.Max(0,normal.y)))); tint.a = 1;
            int first = data.vertices.Count;
            foreach (Vector3 p in points)
            {
                data.vertices.Add(p); data.normals.Add(normal); data.colours.Add(tint);
                data.uv.Add(Mathf.Abs(normal.y)>0.5f ? new Vector2(p.x+0.5f,p.z+0.5f) : Mathf.Abs(normal.x)>0.5f ? new Vector2(p.z+0.5f,p.y) : new Vector2(p.x+0.5f,p.y));
            }
            for (int i=1;i<points.Length-1;i++) { data.triangles.Add(first); data.triangles.Add(first+i); data.triangles.Add(first+i+1); }
        }
        if (shape == Shape.PointedCap)
        {
            Vector3 tip = new Vector3(0,1,0);
            Vector3[] ring = {new Vector3(-0.5f,0,-0.5f),new Vector3(0.5f,0,-0.5f),new Vector3(0.5f,0,0.5f),new Vector3(-0.5f,0,0.5f)};
            Face((Vector3[])ring.Clone(),Vector3.down);
            for(int i=0;i<4;i++) Face(new[] {ring[i],ring[(i+1)%4],tip},(ring[i]+ring[(i+1)%4])*0.5f+Vector3.up*0.5f);
            return data;
        }
        int Corner(Vector3 p) => (p.x>0?1:0)|(p.y>0.5f?2:0)|(p.z>0?4:0);
        void BroadFace(Vector3[] corners, Vector3 facing)
        {
            if (cornerCut == 0) { Face(corners,facing); return; }
            var points = new Vector3[8];
            for(int i=0;i<4;i++)
            {
                Vector3 p = corners[i]; float cut = cuts[Corner(p)];
                points[i*2] = p+(corners[(i+3)%4]-p).normalized*cut;
                points[i*2+1] = p+(corners[(i+1)%4]-p).normalized*cut;
            }
            Face(points,facing);
        }
        for(int sign=-1;sign<=1;sign+=2)
        {
            float s=sign*0.5f, y=sign<0?0:1;
            BroadFace(new[] {new Vector3(s,0,-0.5f),new Vector3(s,1,-0.5f),new Vector3(s,1,0.5f),new Vector3(s,0,0.5f)},new Vector3(sign,0,0));
            BroadFace(new[] {new Vector3(-0.5f,y,-0.5f),new Vector3(-0.5f,y,0.5f),new Vector3(0.5f,y,0.5f),new Vector3(0.5f,y,-0.5f)},new Vector3(0,sign,0));
            BroadFace(new[] {new Vector3(-0.5f,0,s),new Vector3(0.5f,0,s),new Vector3(0.5f,1,s),new Vector3(-0.5f,1,s)},new Vector3(0,0,sign));
        }
        if (cornerCut>0)
        for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
        {
            Vector3 p=new Vector3(x*0.5f,y<0?0:1,z*0.5f); float c=cuts[Corner(p)];
            Face(new[] {p-new Vector3(x*c,0,0),p-new Vector3(0,y*c,0),p-new Vector3(0,0,z*c)},new Vector3(x,y,z));
        }
        return data;
    }
}
