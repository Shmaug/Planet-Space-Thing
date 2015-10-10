// The number of sample points taken along the ray
static const int nSamples = 4;
static const float fSamples = 4.0f;
// Mie phase assymetry factor
static const float g = -0.98f;
static const float g2 = 0.9604f;
// Shader Constants
float4x4 WorldViewProjection;
float3 v3CameraPos;   // The camera's current position
float3 v3LightPos;   // The direction vector to the light source
float3 v3InvWavelength;  // 1 / pow(wavelength, 4) for the red, green, and blue channels
float fCameraHeight;  // The camera's current height
float fCameraHeight2;  // fCameraHeight^2
float fOuterRadius;   // The outer (atmosphere) radius
float fOuterRadius2;  // fOuterRadius^2
float fInnerRadius;   // The inner (planetary) radius
float fInnerRadius2;  // fInnerRadius^2
float fKrESun;	// Kr * ESun
float fKmESun;	// Km * ESun
float fKr4PI;	// Kr * 4 * PI
float fKm4PI;	// Km * 4 * PI
float fScaleDepth;   // The scale depth (the altitude at which the average atmospheric density is found)
float fInvScaleDepth;  // 1 / fScaleDepth
float fScale;	// 1 / (fOuterRadius - fInnerRadius)
float fScaleOverScaleDepth; // fScale / fScaleDepth
// The scale equation calculated by Vernier's Graphical Analysis
float scale(float fCos)
{
	float x = 1.0 - fCos;
	return fScaleDepth * exp(-0.00287 + x*(0.459 + x*(3.83 + x*(-6.80 + x*5.25))));
}
// Calculates the Mie phase function
float getMiePhase(float fCos, float fCos2, float g, float g2)
{
	return 1.5 * ((1.0 - g2) / (2.0 + g2)) * (1.0 + fCos2) / pow(abs(1.0 + g2 - 2.0*g*fCos), 1.5);
}
// Calculates the Rayleigh phase function
float getRayleighPhase(float fCos2)
{
	//return 1.0;
	return 0.75 + 0.75*fCos2;
}
// Returns the near intersection point of a line and a sphere
float getNearIntersection(float3 v3Pos, float3 v3Ray, float fDistance2, float fRadius2)
{
	float B = 2.0 * dot(v3Pos, v3Ray);
	float C = fDistance2 - fRadius2;
	float fDet = max(0.0, B*B - 4.0 * C);
	return 0.5 * (-B - sqrt(fDet));
}
// Returns the far intersection point of a line and a sphere
float getFarIntersection(float3 v3Pos, float3 v3Ray, float fDistance2, float fRadius2)
{
	float B = 2.0 * dot(v3Pos, v3Ray);
	float C = fDistance2 - fRadius2;
	float fDet = max(0.0, B*B - 4.0 * C);
	return 0.5 * (-B + sqrt(fDet));
}
struct VS_IN
{
	float3 Position  : POSITION0;
};
struct PS_IN
{
	float4 Position : SV_POSITION;
	float3 PositionWS : TEXCOORD0;
};
PS_IN VS(VS_IN input)
{
	PS_IN output = (PS_IN)0;
	output.PositionWS = input.Position;
	output.Position = mul(float4(input.Position, 1), WorldViewProjection);
	return output;
}
float4 PS_GroundFromSpace(PS_IN input) : SV_Target
{
	// Get the ray from the camera to the vertex and its length (which is the far point of the ray passing through the atmosphere)
	float3 v3Pos = input.PositionWS;
	float3 v3Ray = v3Pos - v3CameraPos;
	v3Pos = normalize(v3Pos);
	float fFar = length(v3Ray);
	v3Ray /= fFar;
	// Calculate the closest intersection of the ray with the outer atmosphere (which is the near point of the ray passing through the atmosphere)
	float fNear = getNearIntersection(v3CameraPos, v3Ray, fCameraHeight2, fOuterRadius2);
	// Calculate the ray's starting position, then calculate its scattering offset
	float3 v3Start = v3CameraPos + v3Ray * fNear;
		fFar -= fNear;
	float fDepth = exp((fInnerRadius - fOuterRadius) * fInvScaleDepth);
	float fCameraAngle = dot(-v3Ray, v3Pos);
	float fLightAngle = dot(v3LightPos, v3Pos);
	float fCameraScale = scale(fCameraAngle);
	float fLightScale = scale(fLightAngle);
	float fCameraOffset = fDepth*fCameraScale;
	float fTemp = (fLightScale + fCameraScale);
	// Initialize the scattering loop variables
	//gl_FrontColor = vec4(0.0, 0.0, 0.0, 0.0);
	float fSampleLength = fFar / fSamples;
	float fScaledLength = fSampleLength * fScale;
	float3 v3SampleRay = v3Ray * fSampleLength;
		float3 v3SamplePoint = v3Start + v3SampleRay * 0.5;
		// Now loop through the sample rays
		float3 v3FrontColor = float3(0.0, 0.0, 0.0);
		float3 v3Attenuate;
	for (int i = 0; i<nSamples; i++)
	{
		float fHeight = length(v3SamplePoint);
		float fDepth = exp(fScaleOverScaleDepth * (fInnerRadius - fHeight));
		float fScatter = fDepth*fTemp - fCameraOffset;
		v3Attenuate = exp(-fScatter * (v3InvWavelength * fKr4PI + fKm4PI));
		v3FrontColor += v3Attenuate * (fDepth * fScaledLength);
		v3SamplePoint += v3SampleRay;
	}
	float3 c0 = v3FrontColor * (v3InvWavelength * fKrESun + fKmESun);
		float3 c1 = v3Attenuate;
		float3 PlanetColor = float3(0.25, 0.25, 0.25);
		return float4(c0 + PlanetColor * c1, 1);
}
float4 PS_SkyFromSpace(PS_IN input) : SV_Target
{
	// Get the ray from the camera to the vertex and its length (which is the far point of the ray passing through the atmosphere)
	float3 v3Pos = input.PositionWS;
	float3 v3Ray = v3Pos - v3CameraPos;
	float fFar = length(v3Ray);
	v3Ray /= fFar;
	// Calculate the closest intersection of the ray with the outer atmosphere (which is the near point of the ray passing through the atmosphere)
	float fNear = getNearIntersection(v3CameraPos, v3Ray, fCameraHeight2, fOuterRadius2);
	// Calculate the ray's start and end positions in the atmosphere, then calculate its scattering offset
	float3 v3Start = v3CameraPos + v3Ray * fNear;
		fFar -= fNear;
	float fStartAngle = dot(v3Ray, v3Start) / fOuterRadius;
	float fStartDepth = exp(-fInvScaleDepth);
	float fStartOffset = fStartDepth*scale(fStartAngle);
	// Initialize the scattering loop variables
	float fSampleLength = fFar / fSamples;
	float fScaledLength = fSampleLength * fScale;
	float3 v3SampleRay = v3Ray * fSampleLength;
		float3 v3SamplePoint = v3Start + v3SampleRay * 0.5;
		// Now loop through the sample rays
		float3 v3FrontColor = float3(0.0, 0.0, 0.0);
		for (int i = 0; i<nSamples; i++)
		{
			float fHeight = length(v3SamplePoint);
			float fDepth = exp(fScaleOverScaleDepth * (fInnerRadius - fHeight));
			float fLightAngle = dot(v3LightPos, v3SamplePoint) / fHeight;
			float fCameraAngle = dot(v3Ray, v3SamplePoint) / fHeight;
			float fScatter = (fStartOffset + fDepth*(scale(fLightAngle) - scale(fCameraAngle)));
			float3 v3Attenuate = exp(-fScatter * (v3InvWavelength * fKr4PI + fKm4PI));
				v3FrontColor += v3Attenuate * (fDepth * fScaledLength);
			v3SamplePoint += v3SampleRay;
		}
	// Finally, scale the Mie and Rayleigh colors and set up the varying variables for the pixel shader
	float3 c0 = v3FrontColor * (v3InvWavelength * fKrESun);
		float3 c1 = v3FrontColor * fKmESun;
		float3 v3Direction = v3CameraPos - v3Pos;
		float fCos = dot(v3LightPos, v3Direction) / length(v3Direction);
	float fCos2 = fCos*fCos;
	float3 color = getRayleighPhase(fCos2) * c0 + getMiePhase(fCos, fCos2, g, g2) * c1;
		float4 AtmoColor = float4(color, color.b);
		return AtmoColor;
}
technique10 SkyFromSpace
{
	pass P0
	{
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetGeometryShader(0);
		SetPixelShader(CompileShader(ps_4_0, PS_SkyFromSpace()));
	}
}
technique10 GroundFromSpace
{
	pass P0
	{
		SetVertexShader(CompileShader(vs_4_0, VS()));
		SetGeometryShader(0);
		SetPixelShader(CompileShader(ps_4_0, PS_GroundFromSpace()));
	}
}