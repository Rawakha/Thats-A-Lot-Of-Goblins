#ifndef INTERIOR_CORRIDOR_INCLUDED
#define INTERIOR_CORRIDOR_INCLUDED

// ---------------------------------------------------------------------------
// Shared setup: build "room space" and the ray we trace through it.
//
//   +x = quad's local right   +y = quad's local up   +z = straight INTO the wall
//
// The basis is normalized, so it carries rotation only — the room's dimensions
// come from RoomSize in world units and do NOT inherit the quad's scale.
// `doorSize` keeps the quad's world size, because the aperture (where the ray
// starts) IS the quad, so that part must follow the transform.
//
// A Unity Quad's normal is local -Z, i.e. the visible face looks along -Z.
// The room therefore lives on the far side, along local +Z. Do not negate here:
// ViewDirWorldSpace is surface->camera, so camToSurface is camera->surface, and
// dot(camToSurface, +localZ) is positive exactly when we look at the front
// face. Negating intoWall makes rd.z negative for every visible pixel, which
// clamps tBackWall to ~1e5 and makes the back wall unreachable.
//
// ro / rd = ray origin / ray direction — the classic raymarching shorthand.
// ---------------------------------------------------------------------------
void InteriorSetup(float3 ViewDirWorldSpace, float2 UV,
                   out float3 right, out float3 up, out float3 intoWall,
                   out float3 ro, out float3 rd)
{
    right    = normalize(UNITY_MATRIX_M._m00_m10_m20);
    up       = normalize(UNITY_MATRIX_M._m01_m11_m21);
    intoWall = normalize(UNITY_MATRIX_M._m02_m12_m22);

    float2 doorSize = float2(length(UNITY_MATRIX_M._m00_m10_m20),
                             length(UNITY_MATRIX_M._m01_m11_m21));

    float3 camToSurface = -normalize(ViewDirWorldSpace);
    rd = float3(dot(camToSurface, right),
                dot(camToSurface, up),
                dot(camToSurface, intoWall));
    ro = float3((UV - 0.5) * doorSize, 0.0);   // start on the opening
}

// ---------------------------------------------------------------------------
// Box room — ray vs axis-aligned walls. RoomSize = (width, height, depth).
// ---------------------------------------------------------------------------
void InteriorCorridor_float(
    float3 ViewDirWorldSpace,
    float2 UV,
    float3 RoomSize,
    float RoomOffsetY,
    out float2 HitUV,
    out float3 NormalRoomSpace,
    out float Dist,
    out float3 NormalWorldSpace)
{
    float3 right, up, intoWall, ro, rd;
    InteriorSetup(ViewDirWorldSpace, UV, right, up, intoWall, ro, rd);

    float2 roomHalfSize = RoomSize.xy * 0.5;
    float roomDepth = RoomSize.z;
    float floorY   = -roomHalfSize.y + RoomOffsetY;
    float ceilingY =  roomHalfSize.y + RoomOffsetY;

    // Never divide by zero, and keep the sign so the wall we pick stays correct
    float safeDirX = rd.x >= 0 ? max(rd.x, 1e-5) : min(rd.x, -1e-5);
    float safeDirY = rd.y >= 0 ? max(rd.y, 1e-5) : min(rd.y, -1e-5);
    float safeDirZ = max(rd.z, 1e-5);

    // Ray distance (t) to each wall the ray is heading towards
    float tSideWall  = ((rd.x >= 0 ? roomHalfSize.x : -roomHalfSize.x) - ro.x) / safeDirX;
    float tFloorCeil = ((rd.y >= 0 ? ceilingY : floorY) - ro.y) / safeDirY;
    float tBackWall  = roomDepth / safeDirZ;

    // Nearest one wins — that is the surface we actually see
    float tNearest = min(min(tSideWall, tFloorCeil), tBackWall);
    float3 hitPos = ro + rd * tNearest;

    if (tBackWall <= tSideWall && tBackWall <= tFloorCeil)   // back wall
    {
        HitUV = hitPos.xy;
        NormalRoomSpace = float3(0, 0, -1);
    }
    else if (tSideWall <= tFloorCeil)                        // side wall
    {
        HitUV = hitPos.zy;
        NormalRoomSpace = float3(rd.x >= 0 ? -1 : 1, 0, 0);
    }
    else                                                     // floor or ceiling
    {
        HitUV = hitPos.xz;
        NormalRoomSpace = float3(0, rd.y >= 0 ? -1 : 1, 0);
    }
    NormalWorldSpace = NormalRoomSpace.x * right
                     + NormalRoomSpace.y * up
                     + NormalRoomSpace.z * intoWall;
    Dist = tNearest;
}

// ---------------------------------------------------------------------------
// Pipe room — ray vs elliptical cylinder plus a flat cap.
// Same inputs as InteriorCorridor_float plus a Mask output for alpha clipping:
// type "InteriorPipe" into the Custom Function node's Name field.
//
// RoomSize = (width, height, depth). width == height gives a circular pipe;
// differing values give an oval one. RoomOffsetY raises the axis, exactly as it
// raises the box's floor/ceiling pair.
// ---------------------------------------------------------------------------
void InteriorPipe_float(
    float3 ViewDirWorldSpace,
    float2 UV,
    float3 RoomSize,
    float RoomOffsetY,
    out float2 HitUV,
    out float3 NormalRoomSpace,
    out float Dist,
    out float3 NormalWorldSpace,
    out float Mask)
{
    float3 right, up, intoWall, ro, rd;
    InteriorSetup(ViewDirWorldSpace, UV, right, up, intoWall, ro, rd);

    float radiusX = max(RoomSize.x * 0.5, 1e-4);
    float radiusY = max(RoomSize.y * 0.5, 1e-4);
    float roomDepth = RoomSize.z;

    // Work in a space where the ellipse is the unit circle: divide out the radii
    float2 radii = float2(radiusX, radiusY);
    float2 originCircle = float2(ro.x, ro.y - RoomOffsetY) / radii;
    float2 dirCircle    = rd.xy / radii;

    // The quad is a rectangle but the mouth is a circle, so the quad's corners
    // start outside the pipe. Pull them onto the rim, otherwise the quadratic
    // below picks the far wall and the corners show through the pipe.
    float distFromAxis = length(originCircle);   // 1.0 = exactly on the pipe wall
    Mask = 1.0 - step(1.0, distFromAxis);        // 1 inside the ellipse, 0 in the corners
    if (distFromAxis > 1.0)
    {
        originCircle /= distFromAxis;
        ro.xy = originCircle * radii + float2(0.0, RoomOffsetY);
    }

    // |origin + t*dir|^2 = 1  ->  A t^2 + 2B t + C = 0  (B is the half-coefficient)
    float A = max(dot(dirCircle, dirCircle), 1e-8);
    float B = dot(originCircle, dirCircle);
    float C = dot(originCircle, originCircle) - 1.0;   // <= 0: we start inside the pipe

    // C <= 0 guarantees a real solution and exactly one positive root
    float tPipeWall = (-B + sqrt(max(B * B - A * C, 0.0))) / A;
    float tBackCap  = roomDepth / max(rd.z, 1e-5);

    float tNearest = min(tPipeWall, tBackCap);
    float3 hitPos = ro + rd * tNearest;

    if (tBackCap <= tPipeWall)               // flat back cap
    {
        HitUV = hitPos.xy;
        NormalRoomSpace = float3(0, 0, -1);
    }
    else                                     // curved wall
    {
        // Hit point in the cross-section plane, with the pipe axis at the origin
        float2 radialPos = float2(hitPos.x, hitPos.y - RoomOffsetY);

        // atan2 wraps once per revolution. Feeding (x, y) in this order puts
        // that seam at the bottom of the pipe, where it is least visible.
        float angle = atan2(radialPos.x, radialPos.y);

        // u = arc length, v = depth. Both in world units, so the single Tiling
        // float keeps the same meaning it has for the box.
        HitUV = float2(angle * (radiusX + radiusY) * 0.5, hitPos.z);

        // Ellipse gradient is (x/rx^2, y/ry^2); negate it to face the axis
        NormalRoomSpace = -normalize(float3(radialPos.x / (radiusX * radiusX),
                                            radialPos.y / (radiusY * radiusY), 0.0));
    }
    NormalWorldSpace = NormalRoomSpace.x * right
                     + NormalRoomSpace.y * up
                     + NormalRoomSpace.z * intoWall;
    Dist = tNearest;
}
#endif
