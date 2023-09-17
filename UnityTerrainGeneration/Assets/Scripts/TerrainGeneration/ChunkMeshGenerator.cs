using System.Threading.Tasks;
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
		private static readonly Color DEBUG_COLOR_RED = new(1f, 0f, 0f);
		private static readonly Color DEBUG_COLOR_BLUE = new(0f, 0f, 1f);

		public static Mesh MakeMesh(TerrainGene terrainGene, int size, float chunkScale, long xOff, long zOff, LodTransitions lodTransitions = LodTransitions.None)
		{
			if ((size + 3) * (size + 3) > 65535)
			{ Debug.LogException(new System.Exception("The chunk size is too big.")); }

			if (size % 2 != 0)
			{ Debug.LogException(new System.Exception("The chunk size must be even.")); }

			Vector3[] verticesPre;
			{
				verticesPre = new Vector3[(size + 3) * (size + 3)];

				for (int c = 0; c < size + 3; c++)
				{
					for (int r = 0; r < size + 3; r++)
					{
						float xVal = chunkScale * (c - 1);
						float zVal = chunkScale * (r - 1);

						float xValOff = chunkScale * (c - 1 + size * xOff);
						float zValOff = chunkScale * (r - 1 + size * zOff);

						bool doZLodTran = (
							(lodTransitions.HasFlag(LodTransitions.Left) && c < 2)
							|| (lodTransitions.HasFlag(LodTransitions.Right) && c > size)
						) && r % 2 == 0;

						bool doXLodTran = (
							(lodTransitions.HasFlag(LodTransitions.Bottom) && r < 2)
							|| (lodTransitions.HasFlag(LodTransitions.Top) && r > size)
						) && c % 2 == 0;

						float yVal;
						if (doZLodTran || doXLodTran)
						{
							float y1 = terrainGene.HeightAt(xValOff - (doXLodTran ? chunkScale : 0f), zValOff - (doZLodTran ? chunkScale : 0f));
							float y2 = terrainGene.HeightAt(xValOff + (doXLodTran ? chunkScale : 0f), zValOff + (doZLodTran ? chunkScale : 0f));
							yVal = (y1 + y2) / 2f;
						}
						else
						{
							yVal = terrainGene.HeightAt(xValOff, zValOff);
						}

						verticesPre[c * (size + 3) + r] = new Vector3(xVal, yVal, zVal);
					}
				}
			}

			Vector3[] vertices;
			{
				vertices = new Vector3[(size + 1) * (size + 1)];

				for (int c = 0; c < size + 1; c++)
				{
					for (int r = 0; r < size + 1; r++)
					{
						vertices[c * (size + 1) + r] = verticesPre[(c + 1) * (size + 3) + (r + 1)];
					}
				}
			}

			int[] triangles;
			{
				triangles = new int[6 * size * size];

				for (int c = 0; c < size; c++)
				{
					for (int r = 0; r < size; r++)
					{
						int topLeft = (c) * (size + 1) + (r);
						int topRight = (c + 1) * (size + 1) + (r);
						int bottomLeft = (c) * (size + 1) + (r + 1);
						int bottomRight = (c + 1) * (size + 1) + (r + 1);

						triangles[6 * (c * size + r) + 0] = topLeft;
						triangles[6 * (c * size + r) + 1] = bottomLeft;
						triangles[6 * (c * size + r) + 2] = topRight;
						triangles[6 * (c * size + r) + 3] = topRight;
						triangles[6 * (c * size + r) + 4] = bottomLeft;
						triangles[6 * (c * size + r) + 5] = bottomRight;
					}
				}
			}

			Vector3[] normals;
			{
				Vector3[] normalsPre = new Vector3[verticesPre.Length];

				for (int c = 0; c < size + 2; c++)
				{
					for (int r = 0; r < size + 2; r++)
					{
						int topLeft = (c) * (size + 3) + (r);
						int topRight = (c + 1) * (size + 3) + (r);
						int bottomLeft = (c) * (size + 3) + (r + 1);
						int bottomRight = (c + 1) * (size + 3) + (r + 1);

						Vector3 vertexTopLeft = verticesPre[topLeft];
						Vector3 vertexTopRight = verticesPre[topRight];
						Vector3 vertexBottomLeft = verticesPre[bottomLeft];
						Vector3 vertexBottomRight = verticesPre[bottomRight];

						{
							Vector3 normal = Vector3.Cross(vertexBottomLeft - vertexTopLeft, vertexTopRight - vertexTopLeft).normalized;
							normalsPre[topLeft] += normal;
							normalsPre[topRight] += normal;
							normalsPre[bottomLeft] += normal;
						}

						{
							Vector3 normal = Vector3.Cross(vertexTopRight - vertexBottomRight, vertexBottomLeft - vertexBottomRight).normalized;
							normalsPre[topRight] += normal;
							normalsPre[bottomLeft] += normal;
							normalsPre[bottomRight] += normal;
						}
					}
				}

				for (int i = 0; i < normalsPre.Length; i++)
				{
					normalsPre[i].Normalize();
				}

				normals = new Vector3[(size + 1) * (size + 1)];

				for (int c = 0; c < size + 1; c++)
				{
					for (int r = 0; r < size + 1; r++)
					{
						normals[c * (size + 1) + r] = normalsPre[(c + 1) * (size + 3) + (r + 1)];
					}
				}
			}

			Vector2[] uv;
			{
				uv = new Vector2[(size + 1) * (size + 1)];

				for (int c = 0; c < size + 1; c++)
				{
					for (int r = 0; r < size + 1; r++)
					{
						uv[c * (size + 1) + r] = new Vector2((c + 0.5f) / (float)(size + 1), (r + 0.5f) / (float)(size + 1));
					}
				}
			}

			Mesh mesh = new();
			{
				mesh.name = "ScriptGeneratedMesh";
				mesh.vertices = vertices;
				mesh.triangles = triangles;
				mesh.uv = uv;
				mesh.normals = normals;

				mesh.Optimize();
			};

			return mesh;
		}

		private const int TEXTURE_TASK_YIELD_INTERVAL = 500;

		public static async Task<Texture2D> MakeTexture(TerrainGene terrainGene, int sizePre, float chunkScale, long xOff, long zOff, int textureRescaler)
		{
			int size = sizePre * textureRescaler;

			float chunkScalePixel = chunkScale / (float)textureRescaler;

			float[] precalculatedHeights = new float[(size + 1) * (size + 1)];
			for (int c = 0; c < size + 1; c++)
			{
				for (int r = 0; r < size + 1; r++)
				{
					float xCoord = chunkScalePixel * (c + size * xOff);
					float zCoord = chunkScalePixel * (r + size * zOff);

					precalculatedHeights[r * (size + 1) + c] = terrainGene.HeightAt(xCoord, zCoord);
				}
			}

			Color[] colors = new Color[(size + 1) * (size + 1)];
			for (int c = 0; c < size + 1; c++)
			{
				for (int r = 0; r < size + 1; r++)
				{
					float xCoord = chunkScalePixel * (c + size * xOff);
					float zCoord = chunkScalePixel * (r + size * zOff);

					float height = precalculatedHeights[r * (size + 1) + c];

					float heightWest;
					if (c > 0)
					{ heightWest = precalculatedHeights[r * (size + 1) + (c - 1)]; }
					else
					{ heightWest =  terrainGene.HeightAt(xCoord - chunkScalePixel, zCoord); }

					float heightEast;
					if (c < size)
					{ heightEast = precalculatedHeights[r * (size + 1) + (c + 1)]; }
					else
					{ heightEast =  terrainGene.HeightAt(xCoord + chunkScalePixel, zCoord); }

					float heightSouth;
					if (r > 0)
					{ heightSouth = precalculatedHeights[(r - 1) * (size + 1) + c]; }
					else
					{ heightSouth =  terrainGene.HeightAt(xCoord, zCoord - chunkScalePixel); }

					float heightNorth;
					if (r < size)
					{ heightNorth = precalculatedHeights[(r + 1) * (size + 1) + c]; }
					else
					{ heightNorth =  terrainGene.HeightAt(xCoord, zCoord + chunkScalePixel); }

					float slopeHoriz = Mathf.Abs(heightEast - heightWest);
					float slopeVert = Mathf.Abs(heightNorth - heightSouth);
					float slope = (slopeHoriz + slopeVert) / chunkScalePixel;

					colors[r * (size + 1) + c] = terrainGene.GroundColorAt(xCoord, zCoord, height, slope);

					if (false && (r * (size + 1) + c) % TEXTURE_TASK_YIELD_INTERVAL == 0)
					{ await Task.Yield(); }
				}
			}

			Texture2D texture = new Texture2D(size + 1, size + 1);

			texture.SetPixels(colors);
			texture.Apply();

			return texture;
		}
	}
}
