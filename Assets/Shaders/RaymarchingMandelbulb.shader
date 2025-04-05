Shader "Fractal/RaymarchMandelbulb"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100

		Pass 
		{
			ZTest Always Cull Off ZWrite Off

			CGPROGRAM
				#pragma vertex vert
				#pragma fragment frag
				#include "UnityCG.cginc"

				struct v2f
				{
					float4 vertex : SV_POSITION;
					float2 uv : TEXCOORD0;
				};


				v2f vert(uint id : SV_VertexID) 
				{
					v2f o;

					float2 pos = float2((id << 1) & 2, id & 2); // (0, 0) (2, 0) (0, 2) triangle
					o.vertex = float4(pos * 2.0 - 1.0, 0, 1);
					o.uv = pos;
					return o;
				}

				float rho(float3 pos)
				{
					float3 squared = pow(pos, 2);
					float sum = squared.x + squared.y + squared.z;
					float sqrt = pow(sum, 0.5);
				}

				float phi(float3 pos) 
				{
					float divideXY = pos.x / pos.y;
					float arcT = atan2(divideXY, pos.z);
				}

				float DE_Mandelbulb(float3 p)
				{
					float3 z = p;
					float dr = 1.0;
					float r = 0.0;

					const int Power = 8;
					const int maxIterations = 12;

					for (int i = 0; i < maxIterations; i++) 
					{
						r = length(z);
						if (r > 4.0) break;

						if (r < 1e-6) break;

						float theta = acos(clamp(z.z / r, -1.0, 1.0));
						float phi = atan2(z.y, z.x);
						dr = pow(r, Power - 1.0) * Power * dr + 1.0;

						float zr = pow(r, Power);
						theta *= Power;
						phi *= Power;

						z = zr * float3(sin(theta)*cos(phi), sin(theta)*sin(phi), cos(theta)) + p;
					}
					return 0.5 * log(r) * r / dr;
				}

				float4 frag(v2f i) : SV_Target
				{
					// Reconstruct normalized device coordinates (NDC)
					float2 ndc = i.uv * 2.0 - 1.0;
					float3 ro = float3(0, 0, -3.0); // Camera origin
					float3 rd = normalize(float3(ndc.x, ndc.y, 1.5)); // Ray direction

					float dist = 0.0;
					float3 pos;
					const float maxDist = 100.0;
					const float minDist = 0.001;
					const int MaxSteps = 128;
					int steps = 0;


					for (int i = 0; i < MaxSteps; i++)
					{
						pos = ro + rd * dist;
						float d = DE_Mandelbulb(pos);
						if (isnan(d) || isinf(d) || d > 1000.0) return float4(1, 0, 0, 1); // red = bad
						if (d < minDist || d > maxDist) break;
						dist += d;
						steps++;
					}


					//return float4(0.5, 0, 0, 1); // background
					float shade = steps / (float)MaxSteps;
					return float4(shade, shade, shade, 1.0);
				}
			ENDCG
		}
	}
}