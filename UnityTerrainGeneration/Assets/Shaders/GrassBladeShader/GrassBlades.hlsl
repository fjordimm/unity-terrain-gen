
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

float4 _BaseColor;
float4 _TipColor;

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
    InputData lightingInput = (InputData) 0;
    lightingInput.positionWS = input.positionWS;
    lightingInput.normalWS = input.normalWS;
    lightingInput.viewDirectionWS = GetViewDirectionFromPosition(input.positionWS);
    lightingInput.shadowCoord = CalculateShadowCoord(input.positionWS, input.positionCS);
    
    float colorLerp = input.uv;
    float3 albedo = lerp(_BaseColor.rgb, _TipColor.rgb, input.uv);
    
    SurfaceData surfaceInput = (SurfaceData) 0;
    surfaceInput.albedo = albedo;
    surfaceInput.alpha = 1;
    surfaceInput.specular = 1;
    surfaceInput.smoothness = 0.5;
    surfaceInput.occlusion = 1;
    return UniversalFragmentBlinnPhong(lightingInput, surfaceInput);
}

#endif