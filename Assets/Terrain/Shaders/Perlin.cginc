#include "Constants.cginc"

#define DEFSEED 3145 //default seed

int HashInt(int x) {
	x += x << 15;
	x ^= x >> 4;
	x += x >> 5;
	x ^= x << 11;
	return x;
}

int RandomInt(int2 pos, int seed) {//2D
	int x = HashInt(pos.x + (seed ^ 0xbae9581c)) ^
		HashInt(pos.y + (seed ^ 0xcb647be7));
	return HashInt(x);
}

float Fade(float x/*0 - 1*/) {
	return x * x * x * (x * (x * 6.0 - 15.0) + 10.0);//0 - 1
}

float CornerValue2D(int2 cornerOfset, int2 posi, float2 dpos, int seed) {//2D
	float random = (float)(RandomInt(posi + cornerOfset, seed) & 2047);//range 0 - 2047
	static const float mul = (1.0 / 2047.0) * (2.0 * PI);
	float angle = random * mul;
	float2 gradient = float2(cos(angle), sin(angle));

	float2 toDpos = dpos - (float2)cornerOfset;
	return dot(gradient, toDpos);
}

float Perlin2D(float2 pos, int seed)
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
void Perlin2D_float(in float2 pos, out float scalar, int seed = DEFSEED) {
	scalar = .5 + .5 * Perlin2D(pos, seed);
}

float LayeredPerlin2D(float2 pos, float octaveCount, float minorFreq, int seed, float persistance = 0.5)
{
	float value = 0.0;
	float freq = minorFreq;
	float ampl = 1.0;
	float octavesLeft = octaveCount;

	for (uint i = 0; i < (uint)octaveCount + 1; ++i)
	{
		value += ampl * Perlin2D(freq * pos, seed)
			* min(1., octavesLeft);

		freq *= 2.;
		ampl *= persistance;
		seed = HashInt(seed);
		octavesLeft -= 1.;
	}
	return value;
}