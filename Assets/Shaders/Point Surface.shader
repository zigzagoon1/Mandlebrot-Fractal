Shader "Graph/Point Surface" {
	Properties {
		_Smoothness ("Smoothness", Range(0,1)) = 0.5
	}
	SubShader {
		CGPROGRAM
			#pragma surface surf Standard fullforwardshadows addshadow
			#pragma instancing_options assumeuniformscaling procedural:proc
			#pragma target 4.5

			struct Input {
				float3 worldPos;
			};
		#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
			StructuredBuffer<float3> _Positions;
			StructuredBuffer<float4> _Colors;
		#endif

			float _Step;

			void proc() {
				#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
					float3 position = _Positions[unity_InstanceID];
					unity_ObjectToWorld = 0.0;
					unity_ObjectToWorld._m03_m13_m23_m33 = float4(position, 1.0);
					unity_ObjectToWorld._m00_m11_m22 = _Step;
				#endif
			}

			void surf (Input i, inout SurfaceOutputStandard o) {
				#if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
					float4 color = (float4)_Colors[unity_InstanceID];
					o.Albedo = color;
				#endif
			}
		ENDCG
	}
	FallBack "Diffuse"
	
}