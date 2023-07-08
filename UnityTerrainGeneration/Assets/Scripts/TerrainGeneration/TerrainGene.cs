using Unity.VisualScripting;
using UnityEngine;

namespace UnityTerrainGeneration.TerrainGeneration
{
	internal class TerrainGene
	{
		OpenSimplexNoise[] osnRough;
		OpenSimplexNoise[] osnMounds;

		public TerrainGene(System.Random rand)
		{
			osnRough = new OpenSimplexNoise[15];
			for (int i = 0; i < osnRough.Length; i++)
			{ osnRough[i] = new OpenSimplexNoise(rand.Next()); }
		}

		public float HeightAt(float _x, float _z)
		{
			double x = 0.5 * _x;
			double z = 0.5 * _z;

			double y = 0f;

			double ampl = 5.0;
			double freq = 0.03125;
			for (int i = 0; i < osnRough.Length; i++)
			{
				y += ampl * osnRough[i].Evaluate(x * freq, z * freq);
				ampl *= 0.5;
				freq *= 2.0;
			}

			return (float)y;
		}
	}
}
