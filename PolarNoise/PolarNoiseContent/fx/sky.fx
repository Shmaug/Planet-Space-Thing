float4x4 W;
float4x4 V;
float4x4 P;
float3 CamPos;

texture tex;
samplerCUBE samp = sampler_state{
	Texture = <tex>;
	magfilter = LINEAR;
	minfilter = LINEAR;
	mipfilter = LINEAR;
	AddressU = Mirror;
	AddressV = Mirror;
};

struct VertexShaderInput
{
    float4 Position : POSITION0;
};

struct VertexShaderOutput
{
	float4 Position : POSITION0;
	float3 TexCoord : TEXCOORD0;
};

VertexShaderOutput VertexShaderFunction(VertexShaderInput input)
{
    VertexShaderOutput output;

    output.Position = mul(mul(mul(input.Position, W), V), P);
	output.TexCoord = input.Position;

    return output;
}

float4 PixelShaderFunction(VertexShaderOutput input) : COLOR0
{
	return texCUBE(samp, normalize(input.TexCoord));
}

technique Technique1
{
    pass Pass1
    {
        VertexShader = compile vs_2_0 VertexShaderFunction();
        PixelShader = compile ps_2_0 PixelShaderFunction();
    }
}
