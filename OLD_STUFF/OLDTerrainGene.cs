
using UnityEngine;

namespace Anaximer.OLDTerrainGeneration
{
	internal class OLDTerrainGene
	{
		OpenSimplexNoise[] osnRough;
		OpenSimplexNoise[] osnMounds;

		public OLDTerrainGene(System.Random rand)
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

			/*x *= 0.001f;
			z *= 0.001f;

			float mounds = 0f;
			{
				int n = 11;

				float frq = 1f;
				float amp = 256f;
				float divisor = 0f;

				for (int i = 0; i < n; i++)
				{
					mounds += (float)osnMounds[i].Evaluate(x * frq, z * frq) * amp;
					divisor += amp;

					frq *= 2f;
					amp *= 0.4f;
				}

				mounds /= divisor;
			}

			float rough = 0f;
			{
				int n = 11;

				float frq = 1f;
				float amp = 256f;
				float divisor = 0f;

				for (int i = 0; i < n; i++)
				{
					rough += (float)osnRough[i].Evaluate(x * frq, z * frq) * amp;
					divisor += amp;

					frq *= 2f;
					amp *= 0.4f;
				}

				rough /= divisor;
			}

			return 100f * Mathf.Exp(1f / (1f + Mathf.Exp(-10f * mounds)) + rough);*/
		}
	}
}
