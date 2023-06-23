/*
		private IEnumerator ChunkTime()
		{
			while (true)
			{
				int goalX;
				{
					float pre = (PlayerTran.position.x / CHUNKSCALE);
					if (pre < 0f) { goalX = (int)pre - 1; }
					else { goalX = (int)pre; }
				}
				int goalZ;
				{
					float pre = (PlayerTran.position.z / CHUNKSCALE);
					if (pre < 0f) { goalZ = (int)pre - 1; }
					else { goalZ = (int)pre; }
				}

				// Spirals out from the player's position
				int cX = goalX;
				int cZ = goalZ;
				byte cDirection = 0;
				int cLength = 1;
				int cLengthCounter = 0;
				while (cX >= goalX - FullRenderDist && cX <= goalX + FullRenderDist && cZ >= goalZ - FullRenderDist && cZ <= goalZ + FullRenderDist)
				{
					Chunk chunk;
					if (!chunkDict.Has(cX, cZ))
					{
						GameObject chunkGameObj = new("ScriptGeneratedChunk");
						chunkGameObj.transform.SetParent(transform);
						chunkGameObj.transform.localPosition = Vector3.zero;
						chunkGameObj.AddComponent<MeshFilter>();
						chunkGameObj.AddComponent<MeshRenderer>();
						chunkGameObj.AddComponent<MeshCollider>();
						chunkGameObj.GetComponent<Renderer>().material = TerrainMat;

						chunk = new Chunk(chunkGameObj);

						chunkDict.Add(cX, cZ, chunk);
					}
					else
					{
						chunk = chunkDict.Get(cX, cZ);
					}

					if (!chunk.ShouldBeActive)
					{
						chunk.ShouldBeActive = true;
						activeChunks.Enqueue((chunk, cX, cZ));
					}

					int goalLOD = CalculateLOD(goalX - cX, goalZ - cZ);

					bool leftIsInferior = CalculateLOD(goalX - (cX - 1), goalZ - cZ) < goalLOD;
					bool rightIsInferior = CalculateLOD(goalX - (cX + 1), goalZ - cZ) < goalLOD;
					bool aboveIsInferior = CalculateLOD(goalX - cX, goalZ - (cZ - 1)) < goalLOD;
					bool belowIsInferior = CalculateLOD(goalX - cX, goalZ - (cZ + 1)) < goalLOD;

					if (chunk.MeshLODs[goalLOD, leftIsInferior?1:0, rightIsInferior?1:0, aboveIsInferior?1:0, belowIsInferior?1:0] is null)
					{
						Mesh mesh = ChunkMeshGenerator.MakeMesh(terrainGene, goalLOD, CHUNKSCALE, cX, cZ, leftIsInferior, rightIsInferior, aboveIsInferior, belowIsInferior);
						chunk.MeshLODs[goalLOD, leftIsInferior?1:0, rightIsInferior?1:0, aboveIsInferior?1:0, belowIsInferior?1:0] = mesh;
					}

					chunk.SetRealMesh(goalLOD, leftIsInferior?1:0, rightIsInferior?1:0, aboveIsInferior?1:0, belowIsInferior?1:0);
					chunk.ObjRef.SetActive(true);

					if (cLengthCounter == cLength)
					{
						cLengthCounter = 0;
						if (cDirection == 0)
						{
							cDirection = 1;
						}
						else if (cDirection == 1)
						{
							cDirection = 2;
							cLength++;
						}
						else if (cDirection == 2)
						{
							cDirection = 3;
						}
						else if (cDirection == 3)
						{
							cDirection = 0;
							cLength++;
						}
					}
					cLengthCounter++;
					if (cDirection == 0) { cX++; }
					else if (cDirection == 1) { cZ++; }
					else if (cDirection == 2) { cX--; }
					else if (cDirection == 3) { cZ--; }

					int realGoalX;
					{
						float pre = (PlayerTran.position.x / CHUNKSCALE);
						if (pre < 0f) { realGoalX = (int)pre - 1; }
						else { realGoalX = (int)pre; }
					}
					int realGoalZ;
					{
						float pre = (PlayerTran.position.z / CHUNKSCALE);
						if (pre < 0f) { realGoalZ = (int)pre - 1; }
						else { realGoalZ = (int)pre; }
					}
					if (goalX != realGoalX || goalZ != realGoalZ)
					{ break; }

					if (!ForceLoadChunks)
					{ yield return null; }
				}

				if (ForceLoadChunks) ForceLoadChunks = false;

				yield return null;
			}
		}
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Anaximer.OLDTerrainGeneration
{
	public class OLDTerrainController : MonoBehaviour
	{
		[SerializeField] private Transform PlayerTran;
		[SerializeField] private Material TerrainMat;
		[SerializeField] private bool ForceLoadChunks;

		private int _fullRenderDist;
		public int FullRenderDist
		{
			get => _fullRenderDist;
			set { _fullRenderDist = value; UpdateFog(); }
		}

		private int _qualityRenderDist;
		public int QualityRenderDist
		{
			get => _qualityRenderDist;
			set { _qualityRenderDist = value; }
		}

		private const int LODSIZE = 7; // Do not make this greater than 7
		private const float CHUNKSCALE = 0.5f;

		private int Seed { get; } = 823447;
		private ChunkDictionary[] chunkDicts;
		private Queue<(Chunk chunk, int x, int z, int level)> activeChunks;
		private System.Random rand;
		private TerrainGene terrainGene;

		void Start()
		{
			chunkDicts = new ChunkDictionary[30];
			for (int i = 0; i < chunkDicts.Length; i++)
			{ chunkDicts[i] = new(); }

			activeChunks = new();
			rand = new(Seed);
			terrainGene = new(rand);

			RenderSettings.fog = true;
			RenderSettings.fogMode = FogMode.Linear;

			FullRenderDist = 80;
			QualityRenderDist = 70;

			// start coroutines

			{
				GameObject chunkGameObj = new("ScriptGeneratedChunk");
				chunkGameObj.transform.SetParent(transform);
				chunkGameObj.transform.localPosition = Vector3.zero;
				chunkGameObj.AddComponent<MeshFilter>();
				chunkGameObj.AddComponent<MeshRenderer>();
				chunkGameObj.AddComponent<MeshCollider>();
				chunkGameObj.GetComponent<Renderer>().material = TerrainMat;

				Mesh mesh = ChunkMeshGenerator.MakeMesh(terrainGene, 10, 1f, 0, 0);

				chunkGameObj.GetComponent<MeshFilter>().mesh = mesh;
				chunkGameObj.GetComponent<MeshCollider>().sharedMesh = mesh;
			}
		}

		private void UpdateFog()
		{
			RenderSettings.fogStartDistance = 0.01f * FullRenderDist * CHUNKSCALE;
			RenderSettings.fogEndDistance = 50.9f * FullRenderDist * CHUNKSCALE;
		}



		private sealed class Chunk
		{
			public GameObject ObjRef { get; }
			public bool ShouldBeActive { get; set; }
		
			private readonly Mesh[] _meshLODs;
			public Mesh[] MeshLODs
			{ get => _meshLODs; }

			public Chunk(GameObject objRef)
			{
				ObjRef = objRef;
				ShouldBeActive = false;
				_meshLODs = new Mesh[LODSIZE];
			}

			public void SetRealMesh(int lod)
			{
				ObjRef.GetComponent<MeshFilter>().mesh = MeshLODs[lod];
				ObjRef.GetComponent<MeshCollider>().sharedMesh = MeshLODs[lod];
			}
		}

		private class ChunkDictionary
		{
			private class Yes
			{

			}
		}

		private class MatrixDictionary<T> where T : class
		{
			private const int CAPACITY = 4093;
			private const int HALFCAPACITY = CAPACITY / 2;
			private readonly BagOfT[,] bags;

			public MatrixDictionary()
			{
				bags = new BagOfT[CAPACITY, CAPACITY];
			}

			public bool Has(int x, int z)
			{
				BagOfT bag = bags[ToIndex(x), ToIndex(z)];

				if (bag is null)
				{ return false; }
				else
				{ return bag.Has(x, z); }
			}

			public T Get(int x, int z)
			{
				BagOfT bag = bags[ToIndex(x), ToIndex(z)];

				if (bag is null)
				{ return null; }
				else
				{ return bag.Get(x, z); }
			}

			public void Add(int x, int z, T t)
			{
				BagOfT bag = bags[ToIndex(x), ToIndex(z)];

				if (bag is null)
				{
					BagOfT b = new();
					b.Append(t, x, z);
					bags[ToIndex(x), ToIndex(z)] = b;
				}
				else
				{
					bag.Append(t, x, z);
				}
			}

			private static int ToIndex(int val) =>
				System.Math.Abs((val + HALFCAPACITY) % CAPACITY);

			private class BagOfT
			{
				private Node first;

				public BagOfT()
				{
					first = null;
				}

				public void Append(T t, int x, int z)
				{
					if (first is null)
					{
						first = new Node(t, x, z, null);
					}
					else
					{
						Node node = new(t, x, z, first);
						first = node;
					}
				}

				public bool Has(int x, int z)
				{
					Node c = first;

					while (c is not null && (c.X != x || c.Z != z))
					{ c = c.Next; }

					return c is not null;
				}

				public T Get(int x, int z)
				{
					Node c = first;

					while (c is not null && (c.X != x || c.Z != z))
					{ c = c.Next; }

					return c.TheT;
				}

				private class Node
				{
					public int X { get; }
					public int Z { get; }
					public T TheT { get; }
					public Node Next { get; set; }
					public Node(T _TheT, int _X, int _Z, Node _Next)
					{
						TheT = _TheT;
						X = _X;
						Z = _Z;
						Next = _Next;
					}
				}
			}
		}
	}
}
