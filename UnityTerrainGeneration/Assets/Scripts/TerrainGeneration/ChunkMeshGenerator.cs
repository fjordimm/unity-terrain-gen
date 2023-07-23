using UnityEngine;

namespace UnityTerrainGeneration.TerrainGeneration
{
	[System.Flags]
	public enum LodTransitions
	{
		None = 0,
		Left = 1,
		Right = 2,
		Top = 4,
		Bottom = 8
	}

	internal static class ChunkMeshGenerator
	{
		private static readonly Color GREENGRASS = new(0.1f, 0.25f, 0.05f);
		private static readonly Color GRAYSTONE = new(0.21f, 0.21f, 0.21f);
		private static readonly Color WHITESNOW = new(0.5f, 0.5f, 0.6f);
		private static readonly Color DEBUGCOLOR = new(1f, 0f, 0f);

		// DEBUG:
		private static readonly Color[] DEBUG_LOD_COLORS =
		{
			new(1f, 0f, 0f),
			new(1f, 1f, 0f),
			new(0f, 1f, 0f),
			new(0f, 1f, 1f),
			new(0f, 0f, 1f),
			new(1f, 0f, 1f)
		};

		private static readonly Color DEBUG_COLOR_RED = new(1f, 0f, 0f);
		private static readonly Color DEBUG_COLOR_BLUE = new(0f, 0f, 1f);

		public static Mesh MakeMesh(TerrainGene terrainGene, int size, float chunkScale, long xOff, long zOff, LodTransitions lodTransitions)
		{
			int xSize = size;
			int zSize = size;

			if ((xSize + 3) * (zSize + 3) > 65535)
			{ Debug.LogException(new System.Exception("The chunk size is too big.")); }

			if (xSize % 2 != 0 || zSize % 2 != 0)
			{ Debug.LogException(new System.Exception("The chunk size must be even.")); }

			Vector3[] verticesPre;
			{
				verticesPre = new Vector3[(xSize + 3) * (zSize + 3)];

				for (int c = 0; c < xSize + 3; c++)
				{
					for (int r = 0; r < zSize + 3; r++)
					{
						/*int cc = c;
						int rr = r;

						if (doLodTransition)
						{
							if ((c < 2 || c > xSize) && r % 2 == 0)
							{ rr--; }

							//if ((r < 2 || r > zSize) && c % 2 == 0)
							//{ cc--; }
						}*/

						float xVal = chunkScale * (c - 1 + xSize * xOff);
						float zVal = chunkScale * (r - 1 + xSize * zOff);

						bool doZLodTran = (
							(lodTransitions.HasFlag(LodTransitions.Left) && c < 2)
							|| (lodTransitions.HasFlag(LodTransitions.Right) && c > xSize)
						) && r % 2 == 0;

						bool doXLodTran = (
							(lodTransitions.HasFlag(LodTransitions.Bottom) && r < 2)
							|| (lodTransitions.HasFlag(LodTransitions.Top) && r > zSize)
						) && c % 2 == 0;

						float yVal;
						if (doZLodTran || doXLodTran)
						{
							float y1 = terrainGene.HeightAt(xVal - (doXLodTran ? chunkScale : 0f), zVal - (doZLodTran ? chunkScale : 0f));
							float y2 = terrainGene.HeightAt(xVal + (doXLodTran ? chunkScale : 0f), zVal + (doZLodTran ? chunkScale : 0f));
							yVal = (y1 + y2) / 2f;
						}
						else
						{
							yVal = terrainGene.HeightAt(xVal, zVal);
						}

						verticesPre[c * (zSize + 3) + r] = new Vector3(xVal, yVal, zVal);
					}
				}
			}

			Vector3[] vertices;
			{
				vertices = new Vector3[(xSize + 1) * (zSize + 1)];

				for (int c = 0; c < xSize + 1; c++)
				{
					for (int r = 0; r < zSize + 1; r++)
					{
						vertices[c * (zSize + 1) + r] = verticesPre[(c + 1) * (zSize + 3) + (r + 1)];
					}
				}
			}

			int[] triangles;
			{
				triangles = new int[6 * xSize * zSize];

				for (int c = 0; c < xSize; c++)
				{
					for (int r = 0; r < zSize; r++)
					{
						int topLeft = (c) * (zSize + 1) + (r);
						int topRight = (c + 1) * (zSize + 1) + (r);
						int bottomLeft = (c) * (zSize + 1) + (r + 1);
						int bottomRight = (c + 1) * (zSize + 1) + (r + 1);

						triangles[6 * (c * zSize + r) + 0] = topLeft;
						triangles[6 * (c * zSize + r) + 1] = bottomLeft;
						triangles[6 * (c * zSize + r) + 2] = topRight;
						triangles[6 * (c * zSize + r) + 3] = topRight;
						triangles[6 * (c * zSize + r) + 4] = bottomLeft;
						triangles[6 * (c * zSize + r) + 5] = bottomRight;
					}
				}
			}

			Vector3[] normals = MakeNormals(verticesPre, xSize, zSize);

			Color[] colors;
			{
				colors = new Color[(xSize + 1) * (zSize + 1)];

				for (int i = 0; i < colors.Length; i++)
				{
					///*
					float steepness = Vector3.Angle(Vector3.up, normals[i]) / 45f;
					steepness = 1f / (1f + Mathf.Exp(-30f * (steepness - 0.9f)));
					Color col = Color.Lerp(GREENGRASS, GRAYSTONE, steepness);
					colors[i] = col;
					//*/

					// DEBUG:
					// colors[i] = Color.Lerp(DEBUG_COLOR_RED, DEBUG_COLOR_BLUE, Mathf.Sqrt(chunkScale));

					// colors[i] = GREENGRASS;
				}
			}

			Mesh mesh = new();
			{
				mesh.name = "ScriptGeneratedMesh";
				mesh.vertices = vertices;
				mesh.triangles = triangles;
				mesh.colors = colors;

				mesh.normals = normals;
				mesh.Optimize();
			};

			return mesh;
		}

		private static Vector3[] MakeNormals(Vector3[] verticesPre, int xSize, int zSize)
		{
			Vector3[] retPre = new Vector3[verticesPre.Length];

			for (int c = 0; c < xSize + 2; c++)
			{
				for (int r = 0; r < zSize + 2; r++)
				{
					int topLeft = (c) * (zSize + 3) + (r);
					int topRight = (c + 1) * (zSize + 3) + (r);
					int bottomLeft = (c) * (zSize + 3) + (r + 1);
					int bottomRight = (c + 1) * (zSize + 3) + (r + 1);

					Vector3 vertexTopLeft = verticesPre[topLeft];
					Vector3 vertexTopRight = verticesPre[topRight];
					Vector3 vertexBottomLeft = verticesPre[bottomLeft];
					Vector3 vertexBottomRight = verticesPre[bottomRight];

					{
						Vector3 normal = Vector3.Cross(vertexBottomLeft - vertexTopLeft, vertexTopRight - vertexTopLeft).normalized;
						retPre[topLeft] += normal;
						retPre[topRight] += normal;
						retPre[bottomLeft] += normal;
					}

					{
						Vector3 normal = Vector3.Cross(vertexTopRight - vertexBottomRight, vertexBottomLeft - vertexBottomRight).normalized;
						retPre[topRight] += normal;
						retPre[bottomLeft] += normal;
						retPre[bottomRight] += normal;
					}
				}
			}

			for (int i = 0; i < retPre.Length; i++)
			{
				retPre[i].Normalize();
			}

			Vector3[] ret;
			{
				ret = new Vector3[(xSize + 1) * (zSize + 1)];

				for (int c = 0; c < xSize + 1; c++)
				{
					for (int r = 0; r < zSize + 1; r++)
					{
						ret[c * (zSize + 1) + r] = retPre[(c + 1) * (zSize + 3) + (r + 1)];
					}
				}
			}

			return ret;
		}
	}
}
