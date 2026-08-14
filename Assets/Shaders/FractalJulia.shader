// Supports Julia pathing animation with FractalJuliaBackground.cs
Shader "Unlit/FractalJulia"
{
 Properties
    {
        [Toggle] _UseJulia ("Use Julia Set", Float) = 1
        _MaxIter ("Max Iterations", Range(16, 256)) = 64
        _Zoom ("Zoom", Float) = 1.5
        _Center ("Center (xy)", Vector) = (0, 0, 0, 0)
        _AspectCorrect ("Aspect Correct", Range(0, 1)) = 1
        [Header(Animation)]
        _AnimSpeed ("Global Anim Speed", Float) = 1.0
        _ZoomAmount ("Zoom Breathe Amount", Float) = 0.3
        _ZoomSpeed ("Zoom Breathe Speed", Float) = 0.15
        _PanSpeed ("Pan Speed", Float) = 0.05
        _PanAmount ("Pan Amount", Float) = 0.0
        _SwirlAmount ("Swirl Amount", Float) = 0.0
        _SwirlSpeed ("Swirl Speed", Float) = 0.2
        [Header(Julia Orbit)]
        _JuliaC ("Julia C (xy, script-driven)", Vector) = (-0.8, 0.156, 0, 0)
        _JuliaOrbit ("Julia Orbit Radius", Float) = 0.18
        _JuliaOrbitSpeed ("Julia Orbit Speed", Float) = 0.25
        [Header(Palette)]
        // Inigo Quilez cosine palette: color = A + B * cos(TAU * (C * t + D)),
        // evaluated per RGB channel. Tweak these in the material inspector:
        //   A (_PalOffset): base brightness per channel (the color it hovers around)
        //   B (_PalAmp):    contrast per channel (how far it swings from A)
        //   C (_PalFreq):   cycles per palette repeat. KEEP THESE INTEGERS, or the
        //                   flowing color animation will visibly snap once per cycle.
        //   D (_PalPhase):  where in the cycle each channel starts (0..1)
        // Example palettes (A | B | C | D):
        //   Rainbow (current): (.5,.5,.5) | (.5,.5,.5) | (1,1,1) | (0,.33,.67)
        //   Blue/gold:         (.5,.5,.5) | (.5,.5,.5) | (1,1,1) | (.3,.2,.2)
        //   Deep ocean:        (.2,.4,.6) | (.2,.3,.4) | (1,1,1) | (.6,.7,.8)
        //   Ember:             (.6,.3,.2) | (.4,.3,.2) | (1,1,1) | (0,.1,.2)
        //   Two-tone pulse:    (.5,.5,.5) | (.5,.5,.5) | (2,1,1) | (.5,.2,.25)
        _PalOffset ("Palette Offset (A)", Vector) = (0.5, 0.5, 0.5, 0)
        _PalAmp ("Palette Amplitude (B)", Vector) = (0.5, 0.5, 0.5, 0)
        _PalFreq ("Palette Frequency (C)", Vector) = (1, 1, 1, 0)
        _PalPhase ("Palette Phase (D)", Vector) = (0, 0.33, 0.67, 0)
        [Header(Color)]
        _ColorScale ("Color Cycles", Float) = 3.0
        _ColorOffset ("Color Offset", Float) = 0.0
        _ColorFlowSpeed ("Color Flow Speed", Float) = 0.15
        _Saturation ("Saturation", Range(0, 2)) = 1.0
        _Brightness ("Brightness", Range(0, 2)) = 1.0
        _InsideColor ("Inside Color", Color) = (0, 0, 0, 1)
        _Glow ("Edge Glow", Range(0, 2)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" }
        LOD 100
        Cull Off
        ZWrite Off
        ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
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

            // _PhaseA: x = zoom breathe, y = pan X, z = pan Y, w = swirl
            // _PhaseB: x = reserved (orbit now baked into _JuliaC), y = color flow
            float4 _PhaseA;
            float4 _PhaseB;
            
            float _UseJulia;
            float _MaxIter;
            float _Zoom;
            float4 _Center;
            float _AspectCorrect;
            float _ZoomAmount;
            float _PanAmount;
            float _SwirlAmount;
            float4 _JuliaC;
            float4 _PalOffset;
            float4 _PalAmp;
            float4 _PalFreq;
            float4 _PalPhase;
            float _ColorScale;
            float _ColorOffset;
            float _Saturation;
            float _Brightness;
            fixed4 _InsideColor;
            float _Glow;
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            // Inigo Quilez cosine palette: A + B * cos(TAU * (C * t + D)).
            half3 Palette(float t)
            {
                const float TAU = 6.28318530718;
                return saturate(_PalOffset.xyz + _PalAmp.xyz * cos(TAU * (_PalFreq.xyz * frac(t) + _PalPhase.xyz)));
            }
            fixed4 frag (v2f i) : SV_Target
            {
                // Centered, aspect-corrected coordinate (though _AspectCorrect looks weird so I usually keep it off)
                float2 p = i.uv - 0.5;
                if (_AspectCorrect > 0.5)
                {
                    p.x *= _ScreenParams.x / max(_ScreenParams.y, 1.0);
                }
                // Optional swirl
                if (abs(_SwirlAmount) > 0.0001)
                {
                    float r = length(p);
                    float ang = _SwirlAmount * sin(_PhaseA.w + r * 6.0);
                    float s, co;
                    sincos(ang, s, co);
                    p = float2(p.x * co - p.y * s, p.x * s + p.y * co);
                }
                // Breathing zoom + gentle drift
                float zoom = max(_Zoom * (1.0 + _ZoomAmount * sin(_PhaseA.x)), 1e-4);
                float2 pan = float2(sin(_PhaseA.y), cos(_PhaseA.z)) * _PanAmount;
                float scale = 3.0 / zoom;
                // Julia: -> z, orbiting constant c
                // Mandelbrot: -> c, z starts at origin
                float2 zc, c;
                if (_UseJulia > 0.5)
                {
                    zc = (_Center.xy + pan) + p * scale;
                    c = _JuliaC.xy;
                }
                else
                {
                    zc = float2(0.0, 0.0);
                    c = (_Center.xy + pan) + p * scale;
                }
                // Escape-time loop
                const float bailout = 256.0;
                int maxIter = (int)_MaxIter;
                float zx = zc.x, zy = zc.y;
                float zx2 = zx * zx, zy2 = zy * zy;
                int n = 0;
                bool escaped = false;
                for (n = 0; n < maxIter; n++)
                {
                    zy = 2.0 * zx * zy + c.y;
                    zx = zx2 - zy2 + c.x;
                    zx2 = zx * zx;
                    zy2 = zy * zy;
                    if (zx2 + zy2 > bailout) { escaped = true; break; }
                }
                if (!escaped)
                {
                    return _InsideColor;
                }
                // Smooth iteration count
                float mag2 = zx2 + zy2;
                // nu = log2(log2(|z|)) ; iter = n + 1 - nu
                float iter = float(n) + 1.0 - log2(0.5 * log2(mag2));
                // Flowing color coordinate
                float t = sqrt(saturate(iter / _MaxIter));
                half3 col = Palette(t * _ColorScale + _ColorOffset + _PhaseB.y);
                // Saturation / brightness
                half lum = dot(col, half3(0.299, 0.587, 0.114));
                col = lerp(lum.xxx, col, _Saturation) * _Brightness;
                // Edge glow 
                half edge = 1.0 - (half)t;
                half e2 = edge * edge;
                col += _Glow * e2 * e2;
                return fixed4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}