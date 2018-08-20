#include "UnityCG.cginc"
#include "UnityGBuffer.cginc"
#include "Quaternion.cginc"
#include "Random.cginc"

struct appdata
{
	float4 vertex : POSITION;
	float3 normal : NORMAL;
	float2 uv : TEXCOORD0;
	uint vIdx : SV_VertexID;
};

struct v2f
{
	float4 vertex : SV_POSITION;
	float2 uv : TEXCOORD0;
	float3 wPos : TEXCOORD1;
	uint vIdx : TEXCOORD2;
	float3 bary : TEXCOORD3;
	float3 normal : NORMAL;
};

StructuredBuffer<float3> _VertBuffer;
sampler2D _MainTex;
float4 _MainTex_ST;

sampler2D _UVMap;
float4 _UVMap_TexelSize;

half4 _Color,_Spec,_Line;
float _EdgeThreshold, _GSize;
			
v2f vert (appdata v)
{
	v2f o = (v2f)0;
	v.vertex.xyz = _VertBuffer[v.vIdx];
	v.vertex.y *= -1;
	o.vertex = UnityObjectToClipPos(v.vertex);
	o.wPos = mul(unity_ObjectToWorld, v.vertex).xyz;
	o.uv = v.uv;
	o.vIdx = v.vIdx;

	return o;
}

float edgeLength(float3 v0, float3 v1, float3 v2) {
	float l = distance(v0, v1);
	l = max(l, distance(v1, v2));
	l = max(l, distance(v2, v0));
	return l;
}

void cube(float3 center,float3 normal, float size, float4 rot, v2f o, inout TriangleStream<v2f> triStream)
{
    float3 normals[6] = {
        float3(-1, 0, 0), float3( 1, 0, 0),
        float3( 0,-1, 0), float3( 0, 1, 0),
        float3( 0, 0,-1), float3( 0, 0, 1),
    };
    float3 rights[6] =
    {
        float3( 0, 0,-1), float3( 0, 0, 1),
        float3(-1, 0, 0), float3( 1, 0, 0),
        float3( 0,-1, 0), float3( 0, 1, 0),
    };

    float4 q = fromToRotation(normals[0], normal);

    for (int i = 0; i < 6; i++)
    {
        float3 normal = rotateWithQuaternion(normals[i], q);
        float3 right = rotateWithQuaternion(rights[i], q);
        float3 up = cross(normal, right);
        float3 p =  size * normal;

        normal = rotateWithQuaternion(normal, rot);

        float3 pos = p + size * (-right - up);
        pos = rotateWithQuaternion(pos, rot);
        o.wPos = pos + center;
        o.normal = UnityObjectToWorldNormal(normal);
        o.vertex = UnityWorldToClipPos(o.wPos);
        triStream.Append(o);

        pos = p + size * (right - up);
        pos = rotateWithQuaternion(pos, rot);
        o.wPos = pos + center;
        o.normal = UnityObjectToWorldNormal(normal);
        o.vertex = UnityWorldToClipPos(o.wPos);
        triStream.Append(o);

        pos = p + size * (-right + up);
        pos = rotateWithQuaternion(pos, rot);
        o.wPos = pos + center;
        o.normal = UnityObjectToWorldNormal(normal);
        o.vertex = UnityWorldToClipPos(o.wPos);
        triStream.Append(o);

        pos = p + size * ( right + up);
        pos = rotateWithQuaternion(pos, rot);
        o.wPos = pos + center;
        o.normal = UnityObjectToWorldNormal(normal);
        o.vertex = UnityWorldToClipPos(o.wPos);
        triStream.Append(o);
        triStream.RestartStrip();
    }

}

void getPlanePos(float3 center, float size, inout float3 pos, inout float3 normal)
{

}

[maxvertexcount(24)]
void geom(triangle v2f input[3], inout TriangleStream<v2f> triStream)
{
	v2f p0 = input[0];
	v2f p1 = input[1];
	v2f p2 = input[2];

	p0.bary = half3(1, 0, 0);
	p1.bary = half3(0, 1, 0);
	p2.bary = half3(0, 0, 1);

    float t = frac(_Time.x - rand(p0.vIdx * 0.0000032552) * 10) * 5 - 4;
    //t = 0;
    float st = saturate(t);

	half3 normal = normalize(cross(p0.wPos - p1.wPos, p2.wPos - p0.wPos));
	p0.normal = p1.normal = p2.normal = normal;
    half3 center = (p0.wPos + p1.wPos + p2.wPos) / 3 + (float3(0, st*st * 0.5, 0) + normal * st*0.25);
    float3 toDir = normalize(center - _WorldSpaceCameraPos);
    float3 axis = cross(normal, toDir) + float3(0,1,0);
    float4 rot = getAngleAxisRotation(axis, st * 2.0 * 8);

    half size = abs(p0.wPos - p1.wPos) + abs(p1.wPos - p2.wPos) + abs(p2.wPos - p0.wPos);
    size *= (1 - st) * (-1 < t) * .33;

	if (edgeLength(p0.wPos.xyz, p1.wPos.xyz, p2.wPos.xyz) < _EdgeThreshold) {
        cube(center, normal, size, rot, p0, triStream);
    }
}
			
void frag (
    v2f i,
    out half4 outGBuffer0 : SV_Target0,
    out half4 outGBuffer1 : SV_Target1,
    out half4 outGBuffer2 : SV_Target2,
    out half4 outEmission : SV_Target3)
{
	float2 uv = tex2D(_UVMap, i.uv);
	if (any(uv <= 0 || uv >= 1))
		discard;
    
    float gsize = lerp(_GSize, _GSize * 0.1, i.wPos.z * 0.1);
	half3 d = fwidth(frac(i.wPos*gsize));
    half3 a3 = smoothstep(half3(0, 0, 0), d * 0.5, frac(i.wPos * gsize));
	half w = 1.0 - min(min(a3.x, a3.y), a3.z);

    half diff = saturate(dot(i.normal, float3(0.0, 1, 0.0)));
    half3 view = UnityWorldSpaceViewDir(i.wPos);
    half3 h = normalize(view + float3(0.0, 1.0, 0.0));
    half spec = pow(max(0, dot(i.normal, h)), 64.0);
    //diff = 0;

	half3 c = _Color.rgb;

    

    UnityStandardData data;

    data.diffuseColor = _Color;
    data.occlusion = 1;
    data.specularColor = _Spec;
    data.smoothness = 0.5;
    data.normalWorld = i.normal;

    UnityStandardDataToGbuffer(data, outGBuffer0, outGBuffer1, outGBuffer2);

    outEmission = w * _Line;
}

half4 shadow_cast(v2f i):SV_Target
{
    return 0;
}
