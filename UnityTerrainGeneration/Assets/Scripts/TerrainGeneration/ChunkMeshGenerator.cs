using System.Threading.Tasks;
using Unity.VisualScripting;
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

						triangles[6 * (c * size + r) + 3] = bottomRight;
						triangles[6 * (c * size + r) + 4] = topRight;
						triangles[6 * (c * size + r) + 5] = bottomLeft;
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

		public static async Task<(Texture2D, Texture2D)> MakeTexture(TerrainGene terrainGene, int sizePre, float chunkScalePre, long xOff, long zOff, int textureRescaler)
		{
			int size = sizePre * textureRescaler;

			float chunkScale = chunkScalePre / (float)textureRescaler;

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

						float yVal = terrainGene.HeightAt(xValOff, zValOff);

						verticesPre[c * (size + 3) + r] = new Vector3(xVal, yVal, zVal);

						if (false && (r * (size + 3) + c) % 50 == 0)
						{ await Task.Yield(); }
					}
				}
			}

			Vector3[] normalsPre;
			Vector3[] normals;
			{
				normalsPre = new Vector3[verticesPre.Length];

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

			Color[] colors;
			Color[] normalsColors;
			{
				colors = new Color[(size + 1) * (size + 1)];
				normalsColors = new Color[(size + 1) * (size + 1)];

				for (int c = 0; c < size + 1; c++)
				{
					for (int r = 0; r < size + 1; r++)
					{
						Vector3 vertex = verticesPre[(c + 1) * (size + 3) + (r + 1)];
						Vector3 normal = normalsPre[(c + 1) * (size + 3) + (r + 1)];
						// Vector3 realMeshNormal = normals[(c / textureRescaler * textureRescaler) * (size + 1) + (r / textureRescaler * textureRescaler)];

						Vector3 normalWest = normalsPre[(c + 1 - 1) * (size + 3) + (r + 1)];
						Vector3 normalEast = normalsPre[(c + 1 + 1) * (size + 3) + (r + 1)];
						Vector3 normalSouth = normalsPre[(c + 1) * (size + 3) + (r + 1 - 1)];
						Vector3 normalNorth = normalsPre[(c + 1) * (size + 3) + (r + 1 + 1)];
						Vector3 normalAvg = (normalWest + normalEast + normalSouth + normalNorth).normalized;

						float steepness = (1f - normal.normalized.y);

						Vector3 normalForColor = normal;
						Color normalColor = new Color(normalForColor.x * 0.5f + 0.5f, normalForColor.z * 0.5f + 0.5f, normalForColor.y * 0.5f + 0.5f);
						normalColor = new Color(0.5f, 0.5f, 1f);

						colors[r * (size + 1) + c] = terrainGene.GroundColorAt(vertex.x, vertex.z, vertex.y, steepness);
						normalsColors[r * (size + 1) + c] = normalColor;
					}
				}
			}

			Texture2D mainTexture = new Texture2D(size + 1, size + 1);
			mainTexture.SetPixels(colors);
			mainTexture.Apply();

			Texture2D normalMap = new Texture2D(size + 1, size + 1, TextureFormat.RGBA32, true, true);
			normalMap.filterMode = FilterMode.Trilinear;
			normalMap.wrapMode = TextureWrapMode.Clamp;
			normalMap.SetPixels(normalsColors);
			normalMap.Apply();

			return (mainTexture, normalMap);
		}
	}
}
