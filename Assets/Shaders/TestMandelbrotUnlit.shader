Shader "Unlit/TestMandelbrotUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // Map UV coordinates (0 to 1) to Mandelbrot complex plane
                float2 c = i.uv * float2(2.0, 2.0) + float2(-1.5, -1);
                
                // Animated wave effect to add morph effect
                c += float2(sin(_Time.y * 0.2 + (uv.x * 10)) * 0.1, cos(_Time.y * 0.2 + (uv.y * 10)) * 0.1);
                //c += float2(sin(_Time.x * 0.2 + (uv.x * 5)) * 0.1, cos(_Time.x * 0.2 + (uv.y * 5)) * 0.1);
                
                float2 z = float2(0.0, 0.0);
                float iter = 0.0;
                const float max_iter = 32.0; // Balance for mobile
                
                // Unroll loop hint for the GPU compiler to maximize speed
                [unroll(32)]
                for (int n = 0; n < max_iter; n++)
                {
                    // Standard Mandelbrot iteration formula
                    float x = (z.x * z.x - z.y * z.y) + c.x;
                    float y = (2.0 * z.x * z.y) + c.y;
                    
                    // Increase the escape point to smooth edges
                    if ((x * x * y * y) > 20.0)
                    {
                        iter = float(n);
                        break;
                    }
                    z = float2(x, y);
                }
                
                // Smooth coloring algorithm to remove harsh gradient bands
                if (iter < max_iter)
                {
                    float log_zn = log(z.x * z.x + z.y * z.y) / 2.0;
                    float nu = log(log_zn / log(2.0)) / log(2.0);
                    iter = iter + 1.0 - nu;
                }
                
                // Inigo Quilez's color palette formula:
                // Color = A + B * cos(6.28318 * ( C * t + D)) 
                // 6.28318 = 2 * PI or one full cos cycle
                // A = DC Offset; affects brightness/darkness, 
                // B = Amplitude; affects contrast (colorfulness)
                // C = Frequency; How many times the color cycle repeats
                // D = Phase; The starting point of the cycle, shifts the color cycle
                // t = input
                
                // Do this for each color channel separately
                
                // Generate a pretty color palette based on escape speed
                float t = iter / max_iter;
                fixed3 col = fixed3(
                    0.1 + 0.2 * cos(6.28318 * (t + 0.0)),
                    0.1 + 0.2 * cos(6.28318 * (t + 0.3)),
                    0.1 + 0.1 * cos(6.28318 * (t + 0.6))
                    );
                
                // If it never escapedd, color it dark/black 
                if (iter >= max_iter) col = fixed3(0.05, 0.02, 0.1);
                
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
