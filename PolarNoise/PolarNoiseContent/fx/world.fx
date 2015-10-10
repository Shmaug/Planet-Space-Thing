float4x4 World;
float4x4 View;
float4x4 Projection;
float4x4 WorldTranspose;

bool OcculdMap = false;
float3 CameraPos;

float4 StarLights[4];

struct LandVertex
{
	float4 Position : POSITION0;
	float4 Normal : NORMAL0;
	float4 Color : COLOR0;
};
struct VertexShaderOutput
{
	float4 Position : POSITION0;
	float4 Color : COLOR0;
	float Light : TEXCOORD1;
};

VertexShaderOutput VSDiffuse(LandVertex input)
{
    VertexShaderOutput output;

    float4 worldPosition = mul(input.Position, World);
	output.Position = mul(mul(worldPosition, View), Projection);

	float4 norm = mul(input.Normal, WorldTranspose);
	output.Light = 0;
	output.Color = input.Color;
	if (!OcculdMap){
		for (int i = 0; i < 4; i++)
			output.Light += max(dot(normalize(worldPosition.xyz - StarLights[i].xyz), -norm) * StarLights[i].w, 0);
	}

    return output;
}
float4 PSDiffuse(VertexShaderOutput input) : COLOR0
{
	return saturate(input.Color * input.Light);
}

technique Land
{
    pass Pass1
	{
		AlphaBlendEnable = FALSE;
		VertexShader = compile vs_2_0 VSDiffuse();
		PixelShader = compile ps_2_0 PSDiffuse();
    }
}

float Time = 0;
struct WaterVertex
{
	float4 Position : POSITION0;
	float2 Polar : POSITION1;
	float4 Color : COLOR0;
};

float waveA[8];
float waveQ[8];
float2 waveD[8];
float waveW[8];
float waveP[8];

float3 P(float Q, float A, float2 D, float w, float x, float y, float p, float t){
	float dp = dot(D, float2(x, y));
	float term = w*dp + p*t;
	float s = sin(term);
	float c = cos(term);
	return float3(
		Q * A * D.x * c,
		A * s,
		Q * A * D.y * c
		);
}
float3 N(float Q, float A, float2 D, float w, float x, float y, float p, float t){
	float dp = dot(D, float2(x, y));
	float term = w*dp + p*t;
	float s = sin(term);
	float c = cos(term);
	return float3(
		D.x * w*A * c,
		Q * w*A * s,
		D.y * w*A * c
		);
}
void Gerstner(float2 inps, out float4 pos){
	float3 g = float3(0, 0, 0);
	for (int i = 0; i < 8; i++){
		g += P(waveQ[i], waveA[i], waveD[i], waveW[i], inps.x, inps.y, waveP[i], Time);
	}
	pos = float4(g,0);
}

VertexShaderOutput VSWater(WaterVertex input)
{
	VertexShaderOutput output;
	
	/*float4 g = float4(0, 0, 0, 0);
	Gerstner(input.Polar, g);
	input.Position += g;*/

	float4 worldPosition = mul(input.Position, World);
		output.Position = mul(mul(worldPosition, View), Projection);

	float4 norm = mul(normalize(input.Position), WorldTranspose);
		output.Light = 0;
	output.Color = input.Color;
	if (!OcculdMap){
		for (int i = 0; i < 4; i++)
			output.Light += max(dot(normalize(worldPosition.xyz - StarLights[i].xyz), -norm) * StarLights[i].w, 0);
	}

	return output;
}
technique Water
{
	pass Pass1
	{
		AlphaBlendEnable = FALSE;
		VertexShader = compile vs_2_0 VSWater();
		PixelShader = compile ps_2_0 PSDiffuse();
	}
}

struct AtmoVSIn
{
	float4 Position : POSITION0;
};
struct AtmoVSOut
{
	float4 Position : POSITION0;
};

#define PI 3.14159265359

AtmoVSOut VSAtmo(AtmoVSIn input)
{
	AtmoVSOut output;

	float4 worldPos = mul(input.Position, World);
	output.Position = mul(mul(worldPos, View), Projection);

	return output;
}
float4 PSAtmo(AtmoVSOut input) : COLOR0
{
	float4 color = float4(0, 0, 0, 1);
	return color;
}

technique Atmosphere
{
	pass Pass1
	{
		AlphaBlendEnable = TRUE;
		DestBlend = INVSRCALPHA;
		SrcBlend = SRCALPHA;
		VertexShader = compile vs_3_0 VSAtmo();
		PixelShader = compile ps_3_0 PSAtmo();
	}
}