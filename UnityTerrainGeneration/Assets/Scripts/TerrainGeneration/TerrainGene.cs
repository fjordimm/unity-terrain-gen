using System;
using Unity.VisualScripting;
using UnityEngine;

namespace UnityTerrainGeneration.TerrainGeneration
{
	internal class TerrainGene
	{
		OpenSimplexNoise[] osnRough;

		public TerrainGene(System.Random rand)
		{
			osnRough = new OpenSimplexNoise[25];
			for (int i = 0; i < osnRough.Length; i++)
			{ osnRough[i] = new OpenSimplexNoise(rand.Next()); }
		}

		public float HeightAt(float _x, float _z)
		{
			double x = 5.0 * _x;
			double z = 5.0 * _z;

			double y = 0f;

			double ampl = 3.0;
			double freq = 0.005;
			for (int i = 0; i < osnRough.Length; i++)
			{
				y += ampl * osnRough[i].Evaluate(x * freq, z * freq);
				ampl *= 0.5;
				freq *= 1.7;
			}

			y = 0.7 * Math.Exp(y);
			return (float)y;
		}

		private static readonly Color GREENGRASS = new(0.1f, 0.25f, 0.05f);
		private static readonly Color GRAYSTONE = new(0.31f, 0.31f, 0.31f);
		private static readonly Color WHITESNOW = new(0.5f, 0.5f, 0.6f);
		private static readonly Color DEBUG_COLOR_RED = new(1f, 0f, 0f);
		private static readonly Color DEBUG_COLOR_BLUE = new(0f, 0f, 1f);
		public Color GroundColorAt(float _x, float _z, float precalculatedHeightValue, float precalculatedSlopeValue)
		{
			Color ret;

			// ret = new Color(Random.Range(0f, 1f), Random.Range(0f, 1f), Random.Range(0f, 1f));
			// ret = Color.Lerp(Color.black, Color.white, (precalculatedHeightValue - 0f) / 10f);

			if (precalculatedSlopeValue > 3.1f)
			{ ret = GRAYSTONE; }
			else
			{ ret = GREENGRASS; }

			return ret;
		}
	}
}
