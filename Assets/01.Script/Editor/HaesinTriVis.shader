// 경복궁 건물 속 비우기(PalaceShellTool)용: 바깥 시점에서 실제로 보이는 삼각형을 표시한다.
// 패스 0 = 깊이만, 패스 1 = 깊이가 같은(맨 앞) 조각의 삼각형 번호(_TriBase + SV_PrimitiveID)에 앞면 1 / 뒷면 2 비트를 남김.
Shader "Hidden/HaesinTriVis"
{
    SubShader
    {
        Pass
        {
            ZWrite On ZTest LEqual Cull Off ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"
            float4 vert(float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            float4 frag() : SV_Target { return 0; }
            ENDCG
        }
        Pass
        {
            ZWrite Off ZTest LEqual Cull Off ColorMask 0
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 5.0
            #include "UnityCG.cginc"
            RWStructuredBuffer<uint> _Vis : register(u1);
            int _TriBase;
            int _FlipFace;
            float4 vert(float4 v : POSITION) : SV_POSITION { return UnityObjectToClipPos(v); }
            [earlydepthstencil]
            float4 frag(float4 pos : SV_POSITION, uint pid : SV_PrimitiveID, bool front : SV_IsFrontFace) : SV_Target
            {
                bool f = _FlipFace != 0 ? !front : front;
                uint prev;
                InterlockedOr(_Vis[(uint)_TriBase + pid], f ? 1u : 2u, prev);
                return 0;
            }
            ENDCG
        }
    }
}
