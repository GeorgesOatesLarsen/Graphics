#ifndef LIGHTWEIGHT_CORE_INCLUDED
#define LIGHTWEIGHT_CORE_INCLUDED

// -------------------------------------

#include "LightweightInput.cginc"
#include "LightweightLighting.cginc"

#if defined(_HARD_SHADOWS) || defined(_SOFT_SHADOWS) || defined(_HARD_SHADOWS_CASCADES) || defined(_SOFT_SHADOWS_CASCADES)
#define _SHADOWS
#endif

#if defined(_HARD_SHADOWS_CASCADES) || defined(_SOFT_SHADOWS_CASCADES)
#define _SHADOW_CASCADES
#endif

#ifdef _SHADOWS
#include "LightweightShadows.cginc"
#endif

#if defined(_SPECGLOSSMAP_BASE_ALPHA) || defined(_SPECGLOSSMAP) || defined(_SPECULAR_COLOR)
#define LIGHTWEIGHT_SPECULAR_HIGHLIGHTS
#endif

#define _DieletricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)

half SpecularReflectivity(half3 specular)
{
#if (SHADER_TARGET < 30)
    // SM2.0: instruction count limitation
    // SM2.0: simplified SpecularStrength
    return specular.r; // Red channel - because most metals are either monocrhome or with redish/yellowish tint
#else
    return max(max(specular.r, specular.g), specular.b);
#endif
}

half4 OutputColor(half3 color, half alpha)
{
#if defined(_ALPHABLEND_ON) || defined(_ALPHAPREMULTIPLY_ON)
    return LIGHTWEIGHT_LINEAR_TO_GAMMA(half4(color, alpha));
#else
    return half4(LIGHTWEIGHT_LINEAR_TO_GAMMA(color), 1);
#endif
}

VertOutput Vertex(VertInput v)
{
	VertOutput o = (VertOutput)0;

	UNITY_SETUP_INSTANCE_ID(v);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	o.normal = normalize(UnityObjectToWorldNormal(v.normal));

	o.meshUV0.xy = v.texcoord0.xy;// TRANSFORM_TEX(v.texcoord0, _MainTex);
#ifdef LIGHTMAP_ON
	o.meshUV0.zw = v.lightmapUV * unity_LightmapST.xy + unity_LightmapST.zw;
#endif

	//${ VertexShaderBody }
#ifdef VERTEX_CUSTOM
	VERTEX_CUSTOM;
#endif

	o.hpos = UnityObjectToClipPos(v.vertex);
	float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
	o.posWS.xyz = worldPos;
	o.viewDir.xyz = normalize(_WorldSpaceCameraPos - worldPos);

	// TODO: change to only support point lights per vertex. This will greatly simplify shader ALU
#if defined(_VERTEX_LIGHTS) && defined(_MULTIPLE_LIGHTS)
	half3 diffuse = half3(1.0, 1.0, 1.0);
	// pixel lights shaded = min(pixelLights, perObjectLights)
	// vertex lights shaded = min(vertexLights, perObjectLights) - pixel lights shaded
	// Therefore vertexStartIndex = pixelLightCount;  vertexEndIndex = min(vertexLights, perObjectLights)
	int vertexLightStart = min(globalLightCount.x, unity_LightIndicesOffsetAndCount.y);
	int vertexLightEnd = min(globalLightCount.y, unity_LightIndicesOffsetAndCount.y);
	for (int lightIter = vertexLightStart; lightIter < vertexLightEnd; ++lightIter)
	{
		int lightIndex = unity_4LightIndices0[lightIter];
		LightInput lightInput;
		INITIALIZE_LIGHT(lightInput, lightIndex);

		half3 lightDirection;
		half atten = ComputeLightAttenuationVertex(lightInput, o.normal, worldPos, lightDirection);
		o.fogCoord.yzw += LightingLambert(diffuse, lightDirection, normal, atten);
	}
#endif

#if defined(_LIGHT_PROBES_ON) && !defined(LIGHTMAP_ON)
	o.fogCoord.yzw += max(half3(0, 0, 0), ShadeSH9(half4(o.normal, 1)));
#endif

	UNITY_TRANSFER_FOG(o, o.hpos);
	return o;
}

#endif
