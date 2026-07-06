// Shapes © Freya Holmér - https://twitter.com/FreyaHolmer/
// Website & Documentation - https://acegikmo.com/shapes/
#include "UnityCG.cginc"
#include "../Shapes.cginc"
#pragma target 3.0

UNITY_INSTANCING_BUFFER_START(Props)
PROP_DEF(int, _ScaleMode)
PROP_DEF(half4, _Color)
PROP_DEF(half4, _ColorEnd)
PROP_DEF(float3, _PointStart)
PROP_DEF(float3, _PointEnd)
PROP_DEF(half, _Thickness)
PROP_DEF(int, _ThicknessSpace)
SHAPES_DASH_PROPERTIES
SHAPES_FILL_PROPERTIES
UNITY_INSTANCING_BUFFER_END(Props)

#include "../DashUtils.cginc"
#include "../FillUtils.cginc"

#define IP_dash_coord intp0.x
#define IP_dash_spacePerPeriod intp0.y
#define IP_dash_thicknessPerPeriod intp0.z
#define IP_pxCoverage intp0.w

struct VertexInput {
	float4 vertex : POSITION;
	UNITY_VERTEX_INPUT_INSTANCE_ID
};
struct VertexOutput {
	float4 pos : SV_POSITION;
	half4 intp0 : TEXCOORD0;
	half colorBlend : TEXCOORD1;
	SHAPES_INTERPOLATOR_FILL(2)
	UNITY_FOG_COORDS(3)
	UNITY_VERTEX_INPUT_INSTANCE_ID
	UNITY_VERTEX_OUTPUT_STEREO
};

VertexOutput vert(VertexInput v) {
	UNITY_SETUP_INSTANCE_ID(v);
	VertexOutput o = (VertexOutput)0;
	UNITY_TRANSFER_INSTANCE_ID(v, o);
	UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

	float3 a = LocalToWorldPos( PROP(_PointStart) );
	float3 b = LocalToWorldPos( PROP(_PointEnd) );
	
	int scaleMode = PROP(_ScaleMode);
    half uniformScale = GetUniformScale();
	half scaleThickness = scaleMode == SCALE_MODE_UNIFORM ? uniformScale : 1;
	 
	
	half thickness = PROP(_Thickness) * scaleThickness;
	int thicknessSpace = PROP(_ThicknessSpace);

	half lineLength;
	half3 right;
	half3 normal;
	half3 forward;
	GetDirMag(b - a, /*out*/ forward, /*out*/ lineLength);
	float side = saturate( v.vertex.z );
	float3 vertOrigin = side > 0.5 ? b : a;
	half3 camForward = -DirectionToNearPlanePos(vertOrigin);
	half3 camLineNormal;

    if( lineLength < 0.0001 ){ // degenerate case (start == end)
        right   = CAM_RIGHT;
        normal  = CAM_UP;
        forward = CAM_FORWARD;
    	camLineNormal = normal;
    } else {
        bool prettyVertical = abs(forward.y) >= 0.99;
        half3 upRef = prettyVertical ? half3(1,0,0) : half3(0,1,0);
        normal = normalize(cross(upRef,forward));
        right = cross( normal, forward );
    	camLineNormal = normalize(cross(camForward, forward));
    }

    LineWidthData widthData = GetScreenSpaceWidthDataSimple( vertOrigin, camLineNormal, thickness, thicknessSpace );
    o.IP_pxCoverage = widthData.thicknessPixelsTarget;
    float radius = widthData.thicknessMeters * 0.5;
	
	half3 localOffset = v.vertex - half3( 0, 0, saturate( v.vertex.z ) ); //  if z >= 1 then subtract height (z) by 1 to make it a spherical offset
	localOffset *= radius;
	half3 vertPos = vertOrigin + localOffset.x * right + localOffset.y * normal;
	
	#ifdef CAP_ROUND
	    vertPos += localOffset.z * forward;
	#elif defined(CAP_SQUARE)
	    vertPos += (side*2-1) * forward * radius;
	#endif
	
	#if defined(CAP_SQUARE) || defined(CAP_ROUND)
	    half k = 2 * radius / lineLength + 1;
        half m = -radius / lineLength;
        o.colorBlend = k * side + m;
	#else
        o.colorBlend = side;
	#endif
	o.fillCoords = GetFillCoords( vertPos );
	
	// dashes
	if( IsDashed() ) {
		float projDist = dot( forward, vertPos - a ); // distance along line
		DashConfig dash = GetDashConfig(uniformScale);
		DashCoordinates dashCoords = GetDashCoordinates( dash, projDist, lineLength, widthData.thicknessMeters, widthData.pxPerMeter );
		o.IP_dash_coord = dashCoords.coord;
		o.IP_dash_spacePerPeriod = dashCoords.spacePerPeriod;
		o.IP_dash_thicknessPerPeriod = dashCoords.thicknessPerPeriod;
	}

	o.pos = WorldToClipPos( vertPos.xyz );
	UNITY_TRANSFER_FOG(o,o.pos);
	return o;
}

FRAG_OUTPUT_V4 frag( VertexOutput i ) : SV_Target {
	UNITY_SETUP_INSTANCE_ID(i);
	 
    int fillType = PROP( _FillType );
    half4 shape_color;
    if( fillType == FILL_TYPE_NONE ) {
        half4 colorStart = PROP(_Color);
        half4 colorEnd = PROP(_ColorEnd);
        shape_color = lerp( colorStart, colorEnd, saturate(i.colorBlend) );
    } else {
        shape_color = GetFillColor( i.fillCoords );
    }
    
    half shape_mask = 1;
	DashCoordinates dashCoords;
	dashCoords.coord = i.IP_dash_coord;
	dashCoords.spacePerPeriod = i.IP_dash_spacePerPeriod;
	dashCoords.thicknessPerPeriod = i.IP_dash_thicknessPerPeriod;
	ApplyDashMask( /*inout*/ shape_mask, dashCoords, 0, DASH_TYPE_BASIC, 0 );
	    
    shape_mask *= saturate(i.IP_pxCoverage);
	return SHAPES_OUTPUT( shape_color, shape_mask, i );
}