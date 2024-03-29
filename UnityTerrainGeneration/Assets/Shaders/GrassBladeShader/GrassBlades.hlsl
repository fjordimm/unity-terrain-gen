
// Most code copied directly from https://www.youtube.com/watch?v=DeATXF4Szqo
// Also got some stuff from https://gist.github.com/phi-lira/225cd7c5e8545be602dca4eb5ed111ba

#ifndef GRASSBLADES_INCLUDED
#define GRASSBLADES_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "NMGGrassBladeGraphicsHelpers.hlsl"

struct DrawVertex
{
    float3 positionWS;
    float height;
};

struct DrawTriangle
{
    float3 lightingNormalWS;
    DrawVertex vertices[3];
};

StructuredBuffer<DrawTriangle> _DrawTriangles;

struct VertexOutput
{
    float uv : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    float3 normalWS : TEXCOORD2;
    
    float4 positionCS : SV_POSITION;
};

float4 _GrassBladeBaseColor;
float4 _GrassBladeTipColor;

VertexOutput Vertex(uint vertexID: SV_VertexID)
{
    VertexOutput output = (VertexOutput) 0;
    
    DrawTriangle tri = _DrawTriangles[vertexID / 3];
    DrawVertex input = tri.vertices[vertexID % 3];
    
    output.positionWS = input.positionWS;
    output.normalWS = tri.lightingNormalWS;
    output.uv = input.height;
    output.positionCS = TransformWorldToHClip(input.positionWS);
    
    return output;
}

half4 Fragment(VertexOutput input) : SV_Target
{
    SurfaceData surfaceData;
    InitializeStandardLitSurfaceData(input.uv, surfaceData);
    
    surfaceData.albedo = lerp(_GrassBladeBaseColor.rgb, _GrassBladeTipColor.rgb, input.uv);
    
    float3 normalWS = input.normalWS;
    
    #ifdef LIGHTMAP_ON
    half3 bakedGI = SampleLightmap(input.uv, normalWS);
    #else
    half3 bakedGI = SampleSH(input.normalWS);
    #endif
    
    float3 positionWS = input.positionWS;
    half3 viewDirectionWS = SafeNormalize(GetCameraPositionWS() - positionWS);
    
    BRDFData brdfData;
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.specular, surfaceData.smoothness, surfaceData.alpha, brdfData);
    
    Light mainLight = GetMainLight();
    
    half3 color = GlobalIllumination(brdfData, bakedGI, surfaceData.occlusion, normalWS, viewDirectionWS);
    color += LightingPhysicallyBased(brdfData, mainLight, normalWS, viewDirectionWS);
    color += surfaceData.emission;
    
    return half4(color, surfaceData.alpha);
    
    /*
    InputData lightingInput = (InputData)0;
    lightingInput.positionWS = input.positionWS;
    lightingInput.normalWS = input.normalWS;
    lightingInput.viewDirectionWS = GetViewDirectionFromPosition(input.positionWS);
    lightingInput.shadowCoord = CalculateShadowCoord(input.positionWS, input.positionCS);
    
    float colorLerp = input.uv;
    float3 albedo = lerp(_GrassBladeBaseColor.rgb, _GrassBladeTipColor.rgb, input.uv);
    
    SurfaceData surfaceInput = (SurfaceData)0;
    surfaceInput.albedo = albedo;
    surfaceInput.alpha = 1;
    surfaceInput.specular = 1;
    surfaceInput.smoothness = 0.5;
    surfaceInput.occlusion = 1;
    return UniversalFragmentBlinnPhong(lightingInput, surfaceInput);
    */
}

#endif