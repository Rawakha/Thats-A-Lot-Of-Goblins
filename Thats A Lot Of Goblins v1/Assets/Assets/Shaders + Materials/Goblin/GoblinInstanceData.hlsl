#ifndef GOBLIN_INSTANCE_DATA_INCLUDED
#define GOBLIN_INSTANCE_DATA_INCLUDED

#if !defined(SHADERGRAPH_PREVIEW)
UNITY_INSTANCING_BUFFER_START(GoblinInstanceProperties)
    UNITY_DEFINE_INSTANCED_PROP(float4, _GoblinColorFlash)
UNITY_INSTANCING_BUFFER_END(GoblinInstanceProperties)
#endif

void GetGoblinInstanceData_float(
    float4 DefaultColor,
    float DefaultFlash,
    out float4 InstanceColor,
    out float InstanceFlash)
{
#if defined(SHADERGRAPH_PREVIEW)
    InstanceColor = DefaultColor;
    InstanceFlash = DefaultFlash;
#else
#if UNITY_ANY_INSTANCING_ENABLED
        float4 data = UNITY_ACCESS_INSTANCED_PROP(
            GoblinInstanceProperties,
            _GoblinColorFlash);

        InstanceColor = float4(data.rgb, 1.0);
        InstanceFlash = data.a;
#else
    InstanceColor = DefaultColor;
    InstanceFlash = DefaultFlash;
#endif
#endif
}

#endif