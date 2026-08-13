#ifndef __OUTLINE_CORE__
#define __OUTLINE_CORE__

  ////Outline
        /////
		#ifndef MAX2
        #define MAX2(v) max(v.x, v.y)
        #endif
        #ifndef MIN2
        #define MIN2(v) min(v.x, v.y)
        #endif
        #ifndef MAX3
        #define MAX3(v) max(v.x, max(v.y, v.z))
        #endif
        #ifndef MIN3
        #define MIN3(v) min(v.x, min(v.y, v.z))
        #endif
        #ifndef MAX4
        #define MAX4(v) max(v.x, max(v.y, max(v.z, v.w)))
        #endif
        #ifndef MIN4
        #define MIN4(v) min(v.x, min(v.y, min(v.z, v.w)))
        #endif

        float remap(float value, float inputMin, float inputMax, float outputMin, float outputMax)
        {
            return (value - inputMin) * ((outputMax - outputMin) / (inputMax - inputMin)) + outputMin;
        }

        float3 GetNormal(float2 uv)
        {
            float3 normal = SampleSceneNormals(saturate(uv));
            float normalLengthSq = max(dot(normal, normal), 1e-8);
            return normal * rsqrt(normalLengthSq);
        }

        float DirectionalSampleNormal(float2 uv, float3 offset)
        {
            float3 n_c = GetNormal(uv);
            float3 n_l = GetNormal(uv - offset.xz);
            float3 n_r = GetNormal(uv + offset.xz);
            float3 n_u = GetNormal(uv + offset.zy);
            float3 n_d = GetNormal(uv - offset.zy);

            float4 angularDifference = 1.0 - saturate(float4(
                dot(n_l, n_c),
                dot(n_r, n_c),
                dot(n_u, n_c),
                dot(n_d, n_c)));

            // Combining the average with the strongest change rejects tiny isolated
            // normal-map details while retaining intentional creases.
            float averageDifference = dot(angularDifference, float4(0.25, 0.25, 0.25, 0.25));
            float strongestDifference = MAX4(angularDifference);
            return lerp(averageDifference, strongestDifference, 0.35);
        }

        float3 SobelSampleNormal(float2 uv, float3 offset)
        {        
            float3 n_c = GetNormal(uv);
            float3 n_l = GetNormal(uv - offset.xz);
            float3 n_r = GetNormal(uv + offset.xz);
            float3 n_u = GetNormal(uv + offset.zy);
            float3 n_d = GetNormal( uv - offset.zy);

            return (n_c - n_l) +
                   (n_c - n_r) +
                   (n_c - n_u) +
                   (n_c - n_d);
        }            

        float ShapeOutlineSignal(float signal, float threshold, float multiplier, float bias)
        {
            signal = saturate(abs(signal));

            // Use screen derivatives to make the threshold transition cover roughly
            // one pixel. This is cheaper and more stable than a separate blur pass.
            float antiAliasWidth = max(fwidth(signal) * 1.25, 0.002);
            float thresholdMask = smoothstep(
                max(0.0, threshold - antiAliasWidth),
                min(1.0, threshold + antiAliasWidth),
                signal);

            float shapedSignal = pow(
                saturate(signal * max(multiplier, 0.0)),
                max(bias, 0.0001));

            return saturate(thresholdMask * shapedSignal);
        }

        float RelativeForegroundDepthEdge(float centerEyeDepth, float neighbourEyeDepth)
        {
            // Only draw on the foreground side of a discontinuity. This avoids
            // double-width silhouettes and ignores gradual changes across flat faces.
            // Scale the relative difference so the asset's existing 0-1 threshold
            // controls remain useful across both near and distant geometry.
            return (max(neighbourEyeDepth - centerEyeDepth, 0.0) /
                    max(centerEyeDepth, 0.0001)) * 50.0;
        }

        //////////////////////////////////////////////////////////////////////////////////////
        //This MeshEdges function is from repository of Alexander Federwisch
        //https://github.com/Daodan317081/reshade-shaders
        ///BSD 3-Clause License
        // Copyright (c) 2018-2019, Alexander Federwisch
        // All rights reserved.
         float MeshEdges(float depthC, float4 depth1, float4 depth2) 
         {
            /******************************************************************************
                Outlines type 2:
                This method calculates how flat the plane around the center pixel is.
                Can be used to draw the polygon edges of a mesh and its outline.
            ******************************************************************************/
            float depthCenter = depthC;
            float4 depthCardinal = float4(depth1.x, depth2.x, depth1.z, depth2.z);
            float4 depthInterCardinal = float4(depth1.y, depth2.y, depth1.w, depth2.w);
            //Calculate the min and max depths
            float2 mind = float2(MIN4(depthCardinal), MIN4(depthInterCardinal));
            float2 maxd = float2(MAX4(depthCardinal), MAX4(depthInterCardinal));
            float span = MAX2(maxd) - MIN2(mind) + 0.00001;

            //Normalize values
            depthCenter /= span;
            depthCardinal /= span;
            depthInterCardinal /= span;
            //Calculate the (depth-wise) distance of the surrounding pixels to the center
            float4 diffsCardinal = abs(depthCardinal - depthCenter);
            float4 diffsInterCardinal = abs(depthInterCardinal - depthCenter);
            //Calculate the difference of the (opposing) distances
            float2 meshEdge = float2(
                max(abs(diffsCardinal.x - diffsCardinal.y), abs(diffsCardinal.z - diffsCardinal.w)),
                max(abs(diffsInterCardinal.x - diffsInterCardinal.y), abs(diffsInterCardinal.z - diffsInterCardinal.w))
            );

            return MAX2(meshEdge);
        }
        /////////////////////////////////////////////////////////////////////////////////////



#if UNITY_VERSION >= 202310
        float GetClosenessBoostEyeDepth(float3 viewDirWS, float eyeDepth)
        {
            float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
            float cameraDistance = - eyeDepth / dot(viewDirWS, cameraForward);
            float dis = cameraDistance * 0.01;
            float disBoost = smoothstep(_BoostFar, _BoostNear, dis) * _ClosenessBoostThickness + 1;
            return disBoost;
        }

        float GetDistanceFadeEyeDepth(float3 viewDirWS, float eyeDepth)
        {
            // float linearDepth = (1.0 / depth01 - _ZBufferParams.y) / _ZBufferParams.x;
            // float eyeDepth = LinearEyeDepth(linearDepth, _ZBufferParams);
            float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
            float cameraDistance = - eyeDepth / dot(viewDirWS, cameraForward);
            float dis = cameraDistance * 0.01;
            float disBoost = smoothstep(_FadeFar, _FadeNear, dis) + 0.0001;
            return disBoost;            
        }
#else

        #if UNITY_VERSION >= 202230
            float GetClosenessBoostEyeDepth(float3 viewDirWS, float eyeDepth)
            {
                float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
                float cameraDistance = - eyeDepth / dot(viewDirWS, cameraForward);
                float dis = cameraDistance * 0.01;
                float disBoost = smoothstep(_BoostFar, _BoostNear, dis) * _ClosenessBoostThickness + 1;
                return disBoost;
            }

            float GetDistanceFadeEyeDepth(float3 viewDirWS, float eyeDepth)
            {
                // float linearDepth = (1.0 / depth01 - _ZBufferParams.y) / _ZBufferParams.x;
                // float eyeDepth = LinearEyeDepth(linearDepth, _ZBufferParams);
                float3 cameraForward = -UNITY_MATRIX_V[2].xyz;
                float cameraDistance = - eyeDepth / dot(viewDirWS, cameraForward);
                float dis = cameraDistance * 0.01;
                float disBoost = smoothstep(_FadeFar, _FadeNear, dis) + 0.0001;
                return disBoost;            
            }
        #endif

        float GetClosenessBoost(float3 viewPos, float depth01)
        {
            viewPos = viewPos * depth01;
            float dis = length (viewPos) * 0.01;
            float disBoost = smoothstep(_BoostFar, _BoostNear, dis) * _ClosenessBoostThickness + 1;
            return disBoost;
        }

        float GetDistanceFade(float3 viewPos, float depth01)
        {            
            viewPos = viewPos * depth01;
            float dis = length (viewPos) * 0.01;
            float disBoost = smoothstep(_FadeFar, _FadeNear, dis) + 0.0001;
            return disBoost;            
        }
#endif

        float MinFloats(float a, float b, float c, float d)
        {
            return min(min(a, b), min(c, d));
        }

        float MaxFloats(float a, float b, float c, float d)
        {
            return max(max(a, b), max(c, d));
        }

        float SampleDepth9Tiles(float2 uv, float3 offset, float3 viewPos, float centerDisBoost, float centerDepth, out float distanceFade)
        {
            offset *= centerDisBoost;

            // Diagonal taps must be normalised or diagonal outlines become thicker.
            float2 diagonalOffset = offset.xy * 0.70710678;

            float d_c = centerDepth;
            float d_l = SampleSceneDepth(saturate(uv - offset.xz));
            float d_r = SampleSceneDepth(saturate(uv + offset.xz));
            float d_u = SampleSceneDepth(saturate(uv + offset.zy));
            float d_d = SampleSceneDepth(saturate(uv - offset.zy));
            float d_lu = SampleSceneDepth(saturate(uv + diagonalOffset * float2(-1,  1)));
            float d_ld = SampleSceneDepth(saturate(uv + diagonalOffset * float2(-1, -1)));
            float d_ru = SampleSceneDepth(saturate(uv + diagonalOffset * float2( 1,  1)));
            float d_rd = SampleSceneDepth(saturate(uv + diagonalOffset * float2( 1, -1)));

            float de_c = LinearEyeDepth(d_c, _ZBufferParams);
            float de_l = LinearEyeDepth(d_l, _ZBufferParams);
            float de_r = LinearEyeDepth(d_r, _ZBufferParams);
            float de_u = LinearEyeDepth(d_u, _ZBufferParams);
            float de_d = LinearEyeDepth(d_d, _ZBufferParams);
            float de_lu = LinearEyeDepth(d_lu, _ZBufferParams);
            float de_ld = LinearEyeDepth(d_ld, _ZBufferParams);
            float de_ru = LinearEyeDepth(d_ru, _ZBufferParams);
            float de_rd = LinearEyeDepth(d_rd, _ZBufferParams);

            distanceFade = 1;
            if (_EnableDistantFade == 1)
            {
#if UNITY_VERSION >= 202310
                float closeEyeDepth = min(MinFloats(de_l, de_r, de_u, de_d),
                                          MinFloats(de_lu, de_ld, de_ru, de_rd));
                closeEyeDepth = min(de_c, closeEyeDepth);
                distanceFade = GetDistanceFadeEyeDepth(viewPos, closeEyeDepth);                
#else
                float d01_c = Linear01Depth(d_c, _ZBufferParams);
                float closeDepth01 = min(MinFloats(
                    Linear01Depth(d_l, _ZBufferParams),
                    Linear01Depth(d_r, _ZBufferParams),
                    Linear01Depth(d_u, _ZBufferParams),
                    Linear01Depth(d_d, _ZBufferParams)), MinFloats(
                    Linear01Depth(d_lu, _ZBufferParams),
                    Linear01Depth(d_ld, _ZBufferParams),
                    Linear01Depth(d_ru, _ZBufferParams),
                    Linear01Depth(d_rd, _ZBufferParams)));
                closeDepth01 = min(d01_c, closeDepth01);
                distanceFade = GetDistanceFade(viewPos, closeDepth01);
#endif
            }

            float4 cardinalEdges = float4(
                RelativeForegroundDepthEdge(de_c, de_l),
                RelativeForegroundDepthEdge(de_c, de_r),
                RelativeForegroundDepthEdge(de_c, de_u),
                RelativeForegroundDepthEdge(de_c, de_d));
            float4 diagonalEdges = float4(
                RelativeForegroundDepthEdge(de_c, de_lu),
                RelativeForegroundDepthEdge(de_c, de_ld),
                RelativeForegroundDepthEdge(de_c, de_ru),
                RelativeForegroundDepthEdge(de_c, de_rd));

            float diff = max(MAX4(cardinalEdges), MAX4(diagonalEdges));
            diff = smoothstep(_NineTilesThreshold, 1.0, saturate(diff));

            float uvMask = smoothstep(0.0, _NineTileBottomFix, uv.y);
            diff *= uvMask;
            return diff;
        }

        float SampleDepth5Tiles(float2 uv, float3 offset, float3 viewPos, float centerDisBoost, float centerDepth, out float distanceFade)
        {
            offset *= centerDisBoost;

            float d_c = centerDepth;
            float d_l = SampleSceneDepth(saturate(uv - offset.xz));
            float d_r = SampleSceneDepth(saturate(uv + offset.xz));
            float d_u = SampleSceneDepth(saturate(uv + offset.zy));
            float d_d = SampleSceneDepth(saturate(uv - offset.zy));

            float de_c = LinearEyeDepth(d_c, _ZBufferParams);
            float de_l = LinearEyeDepth(d_l, _ZBufferParams);
            float de_r = LinearEyeDepth(d_r, _ZBufferParams);
            float de_u = LinearEyeDepth(d_u, _ZBufferParams);
            float de_d = LinearEyeDepth(d_d, _ZBufferParams);

            distanceFade = 1;
            if (_EnableDistantFade == 1)
            {
#if UNITY_VERSION >= 202310
                float closeEyeDepth = min(de_c, MinFloats(de_l,  de_r,  de_u,  de_d ));
                distanceFade = GetDistanceFadeEyeDepth(viewPos, closeEyeDepth);     
                
#else
                //get the smallest(closest) depth01 arround the center pixel
                float d01_c = Linear01Depth(d_c, _ZBufferParams);
                float closeDepth01 = min(d01_c, MinFloats(
                    Linear01Depth(d_l, _ZBufferParams),
                    Linear01Depth(d_r, _ZBufferParams),
                    Linear01Depth(d_u, _ZBufferParams),
                    Linear01Depth(d_d, _ZBufferParams)));
                distanceFade = GetDistanceFade(viewPos, closeDepth01);
#endif
            }

            float4 depthEdges = float4(
                RelativeForegroundDepthEdge(de_c, de_l),
                RelativeForegroundDepthEdge(de_c, de_r),
                RelativeForegroundDepthEdge(de_c, de_u),
                RelativeForegroundDepthEdge(de_c, de_d));
            return MAX4(depthEdges);
        }
        ///

#endif
