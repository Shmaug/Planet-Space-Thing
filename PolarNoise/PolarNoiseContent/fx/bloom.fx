sampler screenSamp : register(s0);
sampler bloomSamp : register(s1);

float bloomThresh = .85f;

float4 PSextract(float2 Coord : TEXCOORD0) : COLOR0
{
	float4 color = tex2D(screenSamp, Coord);
	float4 e = float4(0, 0, 0, 0);
	if (color.r + color.g + color.b > 2.f)
		e = saturate((color - bloomThresh) / (1 - bloomThresh));
	return e;
}
technique Extract
{
	pass extract
	{
		PixelShader = compile ps_2_0 PSextract();
	}
}

////////////////////// BLUR //////////////////////
#define KERNEL_SIZE (7 * 2 + 1)
float weights[KERNEL_SIZE];
float2 offsets[KERNEL_SIZE];

float4 PSblur(float2 Coord : TEXCOORD0) : COLOR0
{
	float4 color = float4(0, 0, 0, 0);
	for (int i = 0; i < KERNEL_SIZE; ++i)
		color += tex2D(screenSamp, Coord + offsets[i]) * weights[i];

	return color;
}
technique Blur
{
	pass blur
	{
		PixelShader = compile ps_2_0 PSblur();
	}
}

////////////////////// COMBINE //////////////////////

float bloomIntensity = 2;
float bloomSaturation = .9;

float baseIntensity = 1;
float baseSaturation = 1;

float4 AdjustSaturation(float4 col, float sat){
	// The constants 0.3, 0.59, and 0.11 are chosen because the
	// human eye is more sensitive to green light, and less to blue.
	float gray = dot(col, float3(.3, .56, .11));
	return lerp(gray, col, sat);
}

float4 PScombine(float2 Coord : TEXCOORD0) : COLOR0
{
	float4 screen = tex2D(screenSamp, Coord);
	float4 bloom = tex2D(bloomSamp, Coord);

	bloom = AdjustSaturation(bloom, bloomSaturation) * bloomIntensity;
	screen = AdjustSaturation(screen, baseSaturation) * baseIntensity;
	screen *= (1 - saturate(bloom));

	return screen + bloom;
}
technique Combine
{
	pass combine
	{
		PixelShader = compile ps_2_0 PScombine();
	}
}


////////////////////// VOLUMETRIC SCATTER //////////////////////
#define SCATTER_SAMPLES 8
float Exposure = 1.f;
float Density = .9f;
float Decay = .5f;
float Weight = 1.f;
float2 lightPosition;

float2 pixel;

float4 PSscatter(float2 Coord : TEXCOORD0) : COLOR0
{
	float4 color = tex2D(screenSamp, Coord);
	
	float t = 1;
	float dT = Density / SCATTER_SAMPLES;
	float decay = 1;
	for (int i = 0; i < SCATTER_SAMPLES; i++){
		float2 p = lerp(lightPosition, Coord, t);
		t -= dT;
		color += tex2D(screenSamp, p) * decay * Weight;
		decay *= Decay;
	}
	color *= Exposure;
	
	return color;
}
technique Scatter
{
	pass scatter
	{
		PixelShader = compile ps_2_0 PSscatter();
	}
}


