using UnityEngine;

namespace UnityTerrainGeneration.TerrainGeneration
{
	internal static class ChunkMeshGenerator
	{
		private static readonly Color GREENGRASS = new(0.1f, 0.25f, 0.05f);
		private static readonly Color GRAYSTONE = new(0.21f, 0.21f, 0.21f);
		private static readonly Color WHITESNOW = new(0.5f, 0.5f, 0.6f);
		private static readonly Color DEBUGCOLOR = new(1f, 0f, 0f);

		public static Mesh MakeMesh(TerrainGene terrainGene, int size, float chunkScale, int xOff, int zOff)
		{
			int xSize = size;
			int zSize = size;

			if ((xSize + 3) * (zSize + 3) > 65535)
			{ Debug.LogException(new System.Exception("The chunk size is too big.")); }

			Vector3[] verticesPre;
			{
				verticesPre = new Vector3[(xSize + 3) * (zSize + 3)];

				for (int c = 0; c < xSize + 3; c++)
				{
					for (int r = 0; r < zSize + 3; r++)
					{
						float xVal = chunkScale * (c + 2 * xOff);
						float zVal = chunkScale * (r + 2 * zOff);

						verticesPre[c * (zSize + 3) + r] = new Vector3(xVal, terrainGene.HeightAt(xVal, zVal), zVal);
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
					/*float steepness = Vector3.Angle(Vector3.up, normals[i]) / 45f;
					steepness = 1f / (1f + Mathf.Exp(-30f * (steepness - 0.7f)));

					Color col = Color.Lerp(GREENGRASS, GRAYSTONE, steepness);
					colors[i] = col;*/
					colors[i] = GREENGRASS;
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
