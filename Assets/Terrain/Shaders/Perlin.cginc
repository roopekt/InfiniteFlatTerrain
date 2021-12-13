#define PI 3.14159265358979323846
#define DEFSEED 3145 //default seed

static const float3 gradVecs3D[16] = {//3D
	float3( 1,  1,  0),
	float3(-1,  1,  0),
	float3( 1, -1,  0),
	float3(-1, -1,  0),
	float3( 1,  0,  1),
	float3(-1,  0,  1),
	float3( 1,  0, -1),
	float3(-1,  0, -1),
	float3( 0,  1,  1),
	float3( 0, -1,  1),
	float3( 0,  1, -1),
	float3( 0, -1, -1),
	float3( 1,  1,  0),
	float3( 0, -1,  1),
	float3(-1,  1,  0),
	float3( 0, -1, -1)
};

int HashInt(int x) {
	x += x << 15;
	x ^= x >> 4;
	x += x >> 5;
	x ^= x << 11;
	return x;
}

int RandomInt(int3 pos, int seed) {//3D
	int x = HashInt(pos.x + (seed ^ 0xbae9581c)) ^
			 HashInt(pos.y + (seed ^ 0xcb647be7)) ^
			 HashInt(pos.z + (seed ^ 0x1b61e983));
	return HashInt(x);
}

int RandomInt(int2 pos, int seed) {//2D
	int x = HashInt(pos.x + (seed ^ 0xbae9581c)) ^
		HashInt(pos.y + (seed ^ 0xcb647be7));
	return HashInt(x);
}

float Fade(float x/*0 - 1*/) {
	return x * x * x * (x * (x * 6.0 - 15.0) + 10.0);//0 - 1
}

float CornerValue3D(int3 cornerOfset, int3 posi, float3 dpos, int seed) {//3D
	float3 gradient = gradVecs3D[RandomInt(posi + cornerOfset, seed) & 15];
	float3 toDpos = dpos - (float3)cornerOfset;
	return dot(gradient, toDpos);
}

float CornerValue2D(int2 cornerOfset, int2 posi, float2 dpos, int seed) {//2D
	float random = (float)(RandomInt(posi + cornerOfset, seed) & 2047);//0 - 2047
	static const float mul = (1.0 / 2047.0) * (2.0 * PI);
	float angle = random * mul;
	float2 gradient = float2(cos(angle), sin(angle));

	float2 toDpos = dpos - (float2)cornerOfset;
	return dot(gradient, toDpos);
}

float Perlin3D(float3 pos, int seed = DEFSEED)
{
	int3 posi = (int3)floor(pos);

	float3 dpos = pos - floor(pos);//frac doesn't work correctly for negatives
	float3 smooth = float3(Fade(dpos.x), Fade(dpos.y), Fade(dpos.z));//smoothed dpos

	//corner values
	float c000 = CornerValue3D(int3(0, 0, 0), posi, dpos, seed);
	float c001 = CornerValue3D(int3(0, 0, 1), posi, dpos, seed);
	float c010 = CornerValue3D(int3(0, 1, 0), posi, dpos, seed);
	float c011 = CornerValue3D(int3(0, 1, 1), posi, dpos, seed);
	float c100 = CornerValue3D(int3(1, 0, 0), posi, dpos, seed);
	float c101 = CornerValue3D(int3(1, 0, 1), posi, dpos, seed);
	float c110 = CornerValue3D(int3(1, 1, 0), posi, dpos, seed);
	float c111 = CornerValue3D(int3(1, 1, 1), posi, dpos, seed);

	//lerp
	float a00 = lerp(c000, c001, smooth.z);
	float a01 = lerp(c010, c011, smooth.z);
	float a10 = lerp(c100, c101, smooth.z);
	float a11 = lerp(c110, c111, smooth.z);
	float b0 = lerp(a00, a01, smooth.y);
	float b1 = lerp(a10, a11, smooth.y);
	return lerp(b0, b1, smooth.x);
}

float Perlin2D(float2 pos, int seed = DEFSEED)
{
	int2 posi = (int2)floor(pos);

	float2 dpos = pos - floor(pos);//frac, but corrected for negatives
	float2 smooth = float2(Fade(dpos.x), Fade(dpos.y));//smoothed dpos

	//corner values
	float c00 = CornerValue2D(int2(0, 0), posi, dpos, seed);
	float c01 = CornerValue2D(int2(0, 1), posi, dpos, seed);
	float c10 = CornerValue2D(int2(1, 0), posi, dpos, seed);
	float c11 = CornerValue2D(int2(1, 1), posi, dpos, seed);

	//lerp
	float a0 = lerp(c00, c01, smooth.y);
	float a1 = lerp(c10, c11, smooth.y);
	return lerp(a0, a1, smooth.x);
}

//for shader graphs
void Perlin3D_float(in float3 pos, out float scalar, int seed = DEFSEED)
{
	scalar = .5 + .5 * Perlin3D(pos, seed);
}
void Perlin2D_float(in float2 pos, out float scalar, int seed = DEFSEED) {
	scalar = .5 + .5 * Perlin2D(pos, seed);
}

float LayeredPerlin3D(float3 pos, uint octaveCount, float minorFreq, float persistance = 0.5, int seed = DEFSEED)
{
	float value = 0.0;
	float maxValue = 0.0;
	float freq = minorFreq;
	float ampl = 1.0;
	for (uint i = 0; i < octaveCount; ++i)
	{
		value += ampl * Perlin3D(freq * pos, seed);

		maxValue += ampl;
		freq *= 2;
		ampl *= persistance;
		seed = HashInt(seed);
	}
	return value / maxValue;
}

float LayeredPerlin2D(float2 pos, uint octaveCount, float minorFreq, float persistance = 0.5, int seed = DEFSEED)
{
	float value = 0.0;
	float maxValue = 0.0;
	float freq = minorFreq;
	float ampl = 1.0;
	for (uint i = 0; i < octaveCount; ++i)
	{
		value += ampl * Perlin2D(freq * pos, seed);

		maxValue += ampl;
		freq *= 2;
		ampl *= persistance;
		seed = HashInt(seed);
	}
	return value / maxValue;
}