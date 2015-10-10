float4x4 World;
float4x4 View;
float4x4 Projection;

struct Vertex
{
	float4 Position : POSITION0;
	float4 Color : COLOR0;
};

struct VertexShaderOutput
{
	float4 Position : POSITION0;
	float3 texCoord : TEXCOORD0;
	float4 Color : COLOR0;
};

VertexShaderOutput VS(Vertex input)
{
    VertexShaderOutput output;

    float4 worldPosition = mul(input.Position, World);
	output.Position = mul(mul(worldPosition, View), Projection);
	output.texCoord = input.Position;
	output.Color = input.Color;

    return output;
}

float4 PSsfc(VertexShaderOutput input) : COLOR0
{
	return input.Color;
}

technique Surface
{
	pass Pass1
	{
		VertexShader = compile vs_2_0 VS();
		PixelShader = compile ps_2_0 PSsfc();
	}
}