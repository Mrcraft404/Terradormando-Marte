// Efecto de visión dañada del rover (Built-in Render Pipeline, se usa desde OnRenderImage).
// _Intensity 0..1: aberración cromática, líneas de barrido, ruido y desplazamientos tipo VHS.
Shader "Hidden/MarsRobot/DamageVision"
{
    Properties
    {
        _MainTex ("Imagen", 2D) = "white" {}
        _Intensity ("Intensidad", Range(0, 1)) = 0
        _Glitch ("Corte de señal", Range(0, 1)) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Intensity;
            float _Glitch;

            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float t = _Time.y;

                // Desplazamiento horizontal de bandas (cinta VHS dañada).
                float band = floor(uv.y * 24.0 + t * 3.0);
                float jitter = (hash(float2(band, floor(t * 12.0))) - 0.5) * 0.04 * _Intensity;
                jitter *= step(1.0 - _Intensity * 0.6, hash(float2(band * 1.7, floor(t * 8.0))));
                uv.x += jitter;

                // Aberración cromática: R y B separados.
                float2 off = float2(0.006, 0.0) * _Intensity;
                fixed r = tex2D(_MainTex, uv + off).r;
                fixed g = tex2D(_MainTex, uv).g;
                fixed b = tex2D(_MainTex, uv - off).b;
                fixed3 col = fixed3(r, g, b);

                // Líneas de barrido y ruido de estática.
                float scan = 0.92 + 0.08 * sin(uv.y * 900.0 + t * 30.0);
                col *= lerp(1.0, scan, _Intensity);
                float n = hash(uv * 512.0 + t);
                col = lerp(col, fixed3(n, n, n), _Intensity * 0.18);

                // Corte de señal: pantalla casi negra con estática fuerte.
                col = lerp(col, fixed3(n, n, n) * 0.6, _Glitch);
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
