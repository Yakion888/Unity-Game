Shader "Custom/URP_AirDistortion"
{
    Properties
    {
        _NoiseTex ("Noise Texture (RG Distortion)", 2D) = "white" {}
        _DistortStrength ("Distortion Strength", Range(0, 0.1)) = 0.02
        _SpeedX ("Speed X", Float) = 0.5
        _SpeedY ("Speed Y", Float) = 0.5
        _EdgeSoft ("Edge Softness", Range(0.01, 0.3)) = 0.1  // 新增：边缘柔和度
        _EdgeAtten ("Edge Attenuate", Range(0, 1)) = 0.8     // 新增：边缘扭曲衰减
    }
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "RenderType"="Transparent" 
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha  // 开启透明混合，弱化硬边
        
        Pass
        {
            Name "AirDistortionPass"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 screenPosition : TEXCOORD1;
                float2 uvLocal      : TEXCOORD2; // 原始UV，用于边缘衰减
            };

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                float _DistortStrength;
                float _SpeedX;
                float _SpeedY;
                float _EdgeSoft;
                float _EdgeAtten;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.uv = TRANSFORM_TEX(input.uv, _NoiseTex);
                output.screenPosition = ComputeScreenPos(output.positionCS);
                output.uvLocal = input.uv; // 传递原始UV
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                // 1. 计算边缘衰减（面片四周扭曲强度渐变，消除硬轮廓）
                // 将UV转到中心 [-0.5,0.5]
                float2 centeredUV = input.uvLocal - 0.5;
                float edgeFactor = length(centeredUV);
                // 边缘软过渡
                float edgeMask = 1 - smoothstep(0.5 - _EdgeSoft, 0.5, edgeFactor);
                // 最终扭曲强度 = 基础强度 * 边缘遮罩 * 全局衰减
                float finalDistort = _DistortStrength * edgeMask * _EdgeAtten;

                // 2. 滚动噪声
                float2 uvOffset = float2(_SpeedX, _SpeedY) * _Time.y;
                float4 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, input.uv + uvOffset);
                float2 distort = (noise.rg * 2.0 - 1.0) * finalDistort;

                // 3. 屏幕UV + 软钳位（防止采样越界黑边）
                float2 screenUV = input.screenPosition.xy / input.screenPosition.w;
                screenUV += distort;
                // 软钳位：限制在 0~1 内，避免越界
                screenUV = saturate(screenUV); 

                // 4. 采样场景颜色 + 边缘透明度混合
                half3 sceneColor = SampleSceneColor(screenUV);
                half alpha = edgeMask; // 边缘透明淡出

                return half4(sceneColor, alpha);
            }
            ENDHLSL
        }
    }
}