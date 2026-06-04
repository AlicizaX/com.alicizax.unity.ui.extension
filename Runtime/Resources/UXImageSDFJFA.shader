Shader "Hidden/UI/UXImageSDFJFA"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _SourceTex;
        sampler2D _SeedTex;
        float4 _SourceRect;
        float4 _SpriteRect;
        float4 _TextureSize;
        float _Spread;
        float _Step;

        float2 PixelPosition(float2 uv)
        {
            return floor(uv * _TextureSize.xy);
        }

        float SourceAlphaAtPixel(float2 pixel)
        {
            float2 local = (pixel + 0.5 - _SpriteRect.xy) / max(_SpriteRect.zw - _SpriteRect.xy, float2(0.0001, 0.0001));
            float insideRect = step(0.0, local.x) * step(0.0, local.y) * step(local.x, 1.0) * step(local.y, 1.0);
            float2 uv = _SourceRect.xy + local * _SourceRect.zw;
            return tex2D(_SourceTex, uv).a * insideRect;
        }

        float4 FragSeed(v2f_img input) : SV_Target
        {
            float2 pixel = PixelPosition(input.uv);
            float inside = step(0.5, SourceAlphaAtPixel(pixel));
            float right = step(0.5, SourceAlphaAtPixel(pixel + float2(1, 0)));
            float left = step(0.5, SourceAlphaAtPixel(pixel + float2(-1, 0)));
            float up = step(0.5, SourceAlphaAtPixel(pixel + float2(0, 1)));
            float down = step(0.5, SourceAlphaAtPixel(pixel + float2(0, -1)));
            float edge = step(0.5, abs(inside - right) + abs(inside - left) + abs(inside - up) + abs(inside - down));
            return edge > 0.5 ? float4(pixel, 0.0, 1.0) : float4(-1.0, -1.0, 0.0, 0.0);
        }

        void TestCandidate(float2 pixel, float2 uv, inout float4 best, inout float bestDistance)
        {
            float4 candidate = tex2D(_SeedTex, uv);
            if (candidate.w > 0.5)
            {
                float distance = dot(candidate.xy - pixel, candidate.xy - pixel);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }
        }

        float4 FragJump(v2f_img input) : SV_Target
        {
            float2 pixel = PixelPosition(input.uv);
            float2 offset = _Step * _TextureSize.zw;
            float4 best = float4(-1.0, -1.0, 0.0, 0.0);
            float bestDistance = 1.0e20;

            TestCandidate(pixel, input.uv + offset * float2(-1, -1), best, bestDistance);
            TestCandidate(pixel, input.uv + offset * float2(0, -1), best, bestDistance);
            TestCandidate(pixel, input.uv + offset * float2(1, -1), best, bestDistance);
            TestCandidate(pixel, input.uv + offset * float2(-1, 0), best, bestDistance);
            TestCandidate(pixel, input.uv, best, bestDistance);
            TestCandidate(pixel, input.uv + offset * float2(1, 0), best, bestDistance);
            TestCandidate(pixel, input.uv + offset * float2(-1, 1), best, bestDistance);
            TestCandidate(pixel, input.uv + offset * float2(0, 1), best, bestDistance);
            TestCandidate(pixel, input.uv + offset * float2(1, 1), best, bestDistance);

            return best;
        }

        fixed4 FragResolve(v2f_img input) : SV_Target
        {
            float2 pixel = PixelPosition(input.uv);
            float4 seed = tex2D(_SeedTex, input.uv);
            if (seed.w <= 0.5)
                return 0;

            float inside = step(0.5, SourceAlphaAtPixel(pixel));
            float distance = length(seed.xy - pixel);
            float normalizedDistance = saturate(distance / max(_Spread, 0.0001));
            float value = inside > 0.5
                ? 0.5 + normalizedDistance * 0.5
                : 0.5 - normalizedDistance * 0.5;
            return fixed4(value, value, value, value);
        }
        ENDCG

        Pass
        {
            Name "Seed"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragSeed
            #pragma target 3.0
            ENDCG
        }

        Pass
        {
            Name "Jump"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragJump
            #pragma target 3.0
            ENDCG
        }

        Pass
        {
            Name "Resolve"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragResolve
            #pragma target 3.0
            ENDCG
        }
    }
}
