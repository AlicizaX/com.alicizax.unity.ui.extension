Shader "UI/UXImageAdaptiveEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _SDFTex ("SDF Texture", 2D) = "white" {}
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _ShadowColor ("Shadow Color", Color) = (0,0,0,0.5)
        _Color ("Tint", Color) = (1,1,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 sampleRect : TEXCOORD1;
                float4 effect0 : TEXCOORD2;
                float4 effect1 : TEXCOORD3;
                float3 sdfRect0 : NORMAL;
                float4 effect2 : TANGENT;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 sampleRect : TEXCOORD1;
                float4 effect0 : TEXCOORD2;
                float4 effect1 : TEXCOORD3;
                float4 effect2 : TEXCOORD4;
                float4 sdfRect : TEXCOORD5;
                float4 worldPosition : TEXCOORD6;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            sampler2D _SDFTex;
            fixed4 _OutlineColor;
            fixed4 _ShadowColor;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                OUT.sampleRect = v.sampleRect;
                OUT.effect0 = v.effect0;
                OUT.effect1 = v.effect1;
                OUT.effect2 = v.effect2;
                OUT.sdfRect = float4(v.sdfRect0, v.effect2.y);
                OUT.color = v.color * _Color;
                return OUT;
            }

            half SampleAlpha(float2 uv, float4 sampleRect)
            {
                uv = clamp(uv, sampleRect.xy, sampleRect.zw);
                return (tex2D(_MainTex, uv) + _TextureSampleAdd).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 source = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                half sourceAlpha = source.a;

                half2 texel = max(abs(_MainTex_TexelSize.xy), half2(0.000001, 0.000001));
                half outlineEnabled = step(0.5, IN.effect0.x);
                half shadowEnabled = step(0.5, IN.effect0.y);
                half sdfShadowEnabled = step(1.5, IN.effect0.y);
                half2 outlineDistance = IN.effect1.xy;
                half2 shadowOffset = IN.effect1.zw;
                half shadowSoftness = max(IN.effect2.x, 0.001);
                half useGraphicAlpha = step(0.5, IN.effect0.z);
                half outlineSoftness = max(IN.effect2.w, 0.001);

                half2 outlineStep = texel * abs(outlineDistance);
                half outlineAlpha = 0.0;
                if (outlineEnabled > 0.5)
                {
                    outlineAlpha = max(outlineAlpha, SampleAlpha(IN.texcoord + half2(outlineStep.x, 0), IN.sampleRect));
                    outlineAlpha = max(outlineAlpha, SampleAlpha(IN.texcoord - half2(outlineStep.x, 0), IN.sampleRect));
                    outlineAlpha = max(outlineAlpha, SampleAlpha(IN.texcoord + half2(0, outlineStep.y), IN.sampleRect));
                    outlineAlpha = max(outlineAlpha, SampleAlpha(IN.texcoord - half2(0, outlineStep.y), IN.sampleRect));
                    outlineAlpha = max(outlineAlpha, SampleAlpha(IN.texcoord + outlineStep, IN.sampleRect));
                    outlineAlpha = max(outlineAlpha, SampleAlpha(IN.texcoord - outlineStep, IN.sampleRect));
                    outlineAlpha = max(outlineAlpha, SampleAlpha(IN.texcoord + half2(outlineStep.x, -outlineStep.y), IN.sampleRect));
                    outlineAlpha = max(outlineAlpha, SampleAlpha(IN.texcoord + half2(-outlineStep.x, outlineStep.y), IN.sampleRect));
                    outlineAlpha = saturate((outlineAlpha - sourceAlpha) / outlineSoftness);
                }

                half2 shadowUv = IN.texcoord - shadowOffset * texel;
                half shadowAlpha = 0.0;
                half2 shadowStep = texel * shadowSoftness;
                if (shadowEnabled > 0.5)
                {
                    if (sdfShadowEnabled > 0.5)
                    {
                        float2 localUv = (IN.texcoord - IN.sampleRect.xy) / max(IN.sampleRect.zw - IN.sampleRect.xy, float2(0.000001, 0.000001));
                        float2 sdfUv = IN.sdfRect.xy + (IN.sdfRect.zw - IN.sdfRect.xy) * localUv - shadowOffset * texel;
                        half sdfValue = tex2D(_SDFTex, clamp(sdfUv, IN.sdfRect.xy, IN.sdfRect.zw)).a;
                        shadowAlpha = smoothstep(0.0, 1.0, saturate(sdfValue * 2.0));
                    }
                    else
                    {
                        shadowAlpha = SampleAlpha(shadowUv, IN.sampleRect);
                        shadowAlpha += SampleAlpha(shadowUv + half2(shadowStep.x, 0), IN.sampleRect);
                        shadowAlpha += SampleAlpha(shadowUv - half2(shadowStep.x, 0), IN.sampleRect);
                        shadowAlpha += SampleAlpha(shadowUv + half2(0, shadowStep.y), IN.sampleRect);
                        shadowAlpha += SampleAlpha(shadowUv - half2(0, shadowStep.y), IN.sampleRect);
                        shadowAlpha = saturate(shadowAlpha * 0.2);
                    }
                }

                fixed outlineOpacity = _OutlineColor.a;
                fixed shadowOpacity = _ShadowColor.a;
                fixed outlineContribution = outlineAlpha * outlineOpacity;
                fixed shadowContribution = shadowAlpha * shadowOpacity;
                fixed effectAlpha = saturate(max(shadowContribution, outlineContribution) * (1.0 - sourceAlpha));
                fixed graphicAlpha = lerp(1.0, IN.color.a, useGraphicAlpha);
                effectAlpha *= graphicAlpha;
                fixed resultAlpha = saturate(sourceAlpha + effectAlpha * (1.0 - sourceAlpha));
                fixed shadowWeight = shadowContribution >= outlineContribution ? 1.0 : 0.0;
                fixed3 effectColor = lerp(_OutlineColor.rgb, _ShadowColor.rgb, shadowWeight);
                fixed3 premultiplied = source.rgb * sourceAlpha + effectColor * effectAlpha * (1.0 - sourceAlpha);
                fixed4 result;
                result.rgb = premultiplied / max(resultAlpha, 0.0001);
                result.a = resultAlpha;

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDCG
        }
    }
}
