using UnityEngine;

namespace UnityTerrainGeneration.TerrainGeneration
{
	internal class TerrainGene
	{
		OpenSimplexNoise[] osnRough;
		OpenSimplexNoise[] osnMounds;

		public TerrainGene(System.Random rand)
		{
			osnRough = new OpenSimplexNoise[30];
			for (int i = 0; i < osnRough.Length; i++)
			{ osnRough[i] = new OpenSimplexNoise(rand.Next()); }

			osnMounds = new OpenSimplexNoise[30];
			for (int i = 0; i < osnMounds.Length; i++)
			{ osnMounds[i] = new OpenSimplexNoise(rand.Next()); }
		}

		public float HeightAt(float x, float z)
		{
			x *= 0.5f;
			z *= 0.5f;

			return 1f * (float)osnRough[0].Evaluate(x, z);
		}
	}
}
