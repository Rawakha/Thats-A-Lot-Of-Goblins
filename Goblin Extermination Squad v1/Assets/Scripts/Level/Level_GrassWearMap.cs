using System;
using UnityEngine;

// Bounded CPU wear raster, mirrored to one R8 texture. Capsule strokes merge in-place; a root-cell tick cache
// avoids repainting the same packed crowd thousands of times. Allocates arrays once; Stamp/Sample allocate nothing.
public sealed class Level_GrassWearMap
{
    public readonly byte[] Pixels;
    public readonly int Width, Height;
    private readonly Vector2 size;
    private readonly int[] rootTicks;
    private int tick, budget;
    public int Work { get; private set; }
    public int Skipped { get; private set; }
    public bool Dirty { get; private set; }
    public Level_GrassWearMap(int width, int height, Vector2 metres)
    {
        if (width < 1 || height < 1 || width > 1024 || height > 1024 || !float.IsFinite(metres.x) || !float.IsFinite(metres.y) || metres.x <= 0 || metres.y <= 0)
            throw new ArgumentException("Wear map must be positive and at most 1024 square.");
        Width = width; Height = height; size = metres;
        Pixels = new byte[width*height]; rootTicks = new int[Pixels.Length];
    }
    public void Begin(int maxWork)
    {
        if (++tick == int.MaxValue) { Array.Clear(rootTicks,0,rootTicks.Length); tick = 1; }
        Work = 0; Skipped = 0; budget = Mathf.Max(0,maxWork);
    }
    public void Clear() { Array.Clear(Pixels,0,Pixels.Length); Dirty = true; }
    public void Uploaded() => Dirty = false;
    public float Sample(Vector2 point)
    {
        float u = point.x/size.x, v = point.y/size.y;
        if (u < 0 || v < 0 || u > 1 || v > 1) return 0;
        float x = u*Width-0.5f, y = v*Height-0.5f;
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        int x0 = Mathf.Clamp(ix,0,Width-1), x1 = Mathf.Clamp(ix+1,0,Width-1);
        int y0 = Mathf.Clamp(iy,0,Height-1), y1 = Mathf.Clamp(iy+1,0,Height-1);
        return Mathf.Lerp(Mathf.Lerp(Pixels[y0*Width+x0],Pixels[y0*Width+x1],x-ix),
            Mathf.Lerp(Pixels[y1*Width+x0],Pixels[y1*Width+x1],x-ix),y-iy)/255f;
    }
    public void Stamp(Vector2 from, Vector2 to, float radius, float strength)
    {
        if (!float.IsFinite(from.x) || !float.IsFinite(from.y) || !float.IsFinite(to.x) || !float.IsFinite(to.y) ||
            !float.IsFinite(radius) || !float.IsFinite(strength) || to.x < 0 || to.y < 0 || to.x >= size.x || to.y >= size.y || radius <= 0 || strength <= 0) return;
        int root = Mathf.Clamp((int)(to.y/size.y*Height),0,Height-1)*Width + Mathf.Clamp((int)(to.x/size.x*Width),0,Width-1);
        if (rootTicks[root] == tick) return;
        rootTicks[root] = tick;
        if (Work >= budget) { Skipped++; return; }
        // Ignore teleport-sized links. Paint only the destination rather than a stripe across the map.
        if ((to-from).sqrMagnitude > 64) from = to;
        int x0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(from.x,to.x)-radius)/size.x*Width),0,Width-1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(from.x,to.x)+radius)/size.x*Width),0,Width-1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt((Mathf.Min(from.y,to.y)-radius)/size.y*Height),0,Height-1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt((Mathf.Max(from.y,to.y)+radius)/size.y*Height),0,Height-1);
        Vector2 segment = to-from; float length = segment.sqrMagnitude;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            if (Work++ >= budget) { Work = budget; Skipped++; return; }
            Vector2 point = new Vector2((x+0.5f)/Width*size.x,(y+0.5f)/Height*size.y);
            float t = length > 0.000001f ? Mathf.Clamp01(Vector2.Dot(point-from,segment)/length) : 0;
            float distance = (point-from-segment*t).magnitude/radius;
            if (distance >= 1) continue;
            float edge = 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.65f,1,distance));
            int at = y*Width+x, value = Mathf.Min(255,Pixels[at]+Mathf.CeilToInt(strength*255*edge));
            if (value == Pixels[at]) continue;
            Pixels[at] = (byte)value; Dirty = true;
        }
    }
}
