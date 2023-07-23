using Unity.VisualScripting;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine.AI;

namespace UnityTerrainGeneration.TerrainGeneration
{
	internal class TerrainManager
	{
		private const int CHUNK_MESH_SIZE = 32;
		private const int NUM_LODS = 5;
		private const float LOD0_PARTIAL_CHUNK_SCALE = 100.0f; // The width of one triangle for the lowest LOD chunk
		private static readonly float[] PARTIAL_CHUNK_SCALES = new float[NUM_LODS]; // The width of one triangle for each LOD
		private static readonly float[] CHUNK_SCALES = new float[NUM_LODS]; // The scale of a chunk for each LOD

		private const long CHUNK_RENDER_DIST = 5;
		private const long SUB_CHUNK_FACTOR = CHUNK_RENDER_DIST * 2 + 1;

		private readonly MonoBehaviour controller;
		private readonly Transform originTran;
		private readonly Transform playerTran;
		private readonly Material terrainMat;
		private readonly int seed;

		private readonly System.Random rand;
		private readonly TerrainGene terrainGene;
		private readonly Dictionary<ChunkCoords, Chunk> chunkDict;
		private readonly LinkedList<(ChunkCoords coords, Chunk chunk)> activeChunkQueue;
		private readonly LinkedList<(ChunkCoords coords, int lod, Chunk chunk)> chunkMeshGenQueue;

		public TerrainManager(MonoBehaviour _controller, Transform _originTran, Transform _playerTran, Material _terrainMat, int _seed)
		{
			for (int i = 0; i < NUM_LODS; i++)
			{
				if (i == 0) PARTIAL_CHUNK_SCALES[i] = LOD0_PARTIAL_CHUNK_SCALE;
				else PARTIAL_CHUNK_SCALES[i] = PARTIAL_CHUNK_SCALES[i-1] / SUB_CHUNK_FACTOR;

				CHUNK_SCALES[i] = PARTIAL_CHUNK_SCALES[i] * CHUNK_MESH_SIZE;
			}

			controller = _controller;
			originTran = _originTran;
			playerTran = _playerTran;
			terrainMat = _terrainMat;
			seed = _seed;

			rand = new System.Random(seed);
			terrainGene = new TerrainGene(rand);

			chunkDict = new Dictionary<ChunkCoords, Chunk>();
			activeChunkQueue = new LinkedList<(ChunkCoords coords, Chunk chunk)>();
			chunkMeshGenQueue = new LinkedList<(ChunkCoords coords, int lod, Chunk chunk)>();
		}

		public void BeginGeneration()
		{
			controller.StartCoroutine(EnqueueChunksNearPlayer());
			controller.StartCoroutine(UpdateActiveChunkQueue());
			controller.StartCoroutine(UpdateChunkMeshGenQueue());
		}

		private IEnumerator EnqueueChunksNearPlayer()
		{
			while (true)
			{
				ChunkCoords goalCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, 0);

				// Spirals out from the player's position
				long cX = goalCoords.x;
				long cZ = goalCoords.z;
				byte cDirection = 0;
				int cLength = 1;
				int cLengthCounter = 0;
				while (cX >= goalCoords.x - CHUNK_RENDER_DIST && cX <= goalCoords.x + CHUNK_RENDER_DIST && cZ >= goalCoords.z - CHUNK_RENDER_DIST && cZ <= goalCoords.z + CHUNK_RENDER_DIST)
				{
					{
						ChunkCoords currentSpiralCoords = new ChunkCoords(cX, cZ);

						Chunk chunk;
						bool alreadyHasChunk = chunkDict.TryGetValue(currentSpiralCoords, out Chunk ch);
						if (alreadyHasChunk)
						{
							chunk = ch;
						}
						else
						{
							chunk = new Chunk(this);
							chunkDict.Add(currentSpiralCoords, chunk);
						}

						if (!chunk.ShouldBeActive)
						{
							chunk.ShouldBeActive = true;
							activeChunkQueue.AddLast((currentSpiralCoords, chunk));
						}
					}

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

					// In case the player moved in the middle of the coroutine, then restart the spiral
					ChunkCoords realChunkCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, 0);
					if (!goalCoords.Equals(realChunkCoords))
					{ break; }

					yield return null;
				}

				yield return null;
			}
		}

		private IEnumerator UpdateActiveChunkQueue()
		{
			while (true)
			{
				if (activeChunkQueue.Count > 0)
				{
					var llnode = activeChunkQueue.First;
					activeChunkQueue.RemoveFirst();

					if (IsWithinRenderDist(0, llnode.Value.coords))
					{
						if (!llnode.Value.chunk.HasMesh)
						{
							if (!llnode.Value.chunk.ShouldHaveMesh)
							{
								llnode.Value.chunk.ShouldHaveMesh = true;
								chunkMeshGenQueue.AddLast((llnode.Value.coords, 0, llnode.Value.chunk));
							}
						}
						else
						{
							llnode.Value.chunk.SetObjActive(true);
						}

						UpdateSubChunksRecursively(0, llnode.Value.coords, llnode.Value.chunk);

						activeChunkQueue.AddLast(llnode);
					}
					else
					{
						llnode.Value.chunk.ShouldBeActive = false;
						llnode.Value.chunk.SetObjActive(false);

						UpdateSubChunksRecursively(0, llnode.Value.coords, llnode.Value.chunk);
					}
				}

				yield return null;
			}
		}

		private void UpdateSubChunksRecursively(int lod, ChunkCoords coords, Chunk chunk)
		{
			if (lod >= NUM_LODS - 1)
			{ return; }

			if (IsWithinHalfRenderDist(lod, coords))
			{
				if (chunk.FinishedAllSubChunks)
				{
					for (int i = 0; i < SUB_CHUNK_FACTOR; i++)
					{
						for (int j = 0; j < SUB_CHUNK_FACTOR; j++)
						{
							chunk.SubChunks[i, j].SetObjActive(true);

							ChunkCoords subCoords = new ChunkCoords(coords.x * SUB_CHUNK_FACTOR + i, coords.z * SUB_CHUNK_FACTOR + j);
							UpdateSubChunksRecursively(lod + 1, subCoords, chunk.SubChunks[i, j]);
						}
					}

					chunk.SetObjActive(false);
				}
				else
				{
					if (chunk.SubChunks is null)
					{ chunk.MakeSubChunks(this); }

					int subChunkFinishedCount = 0;

					for (int i = 0; i < SUB_CHUNK_FACTOR; i++)
					{
						for (int j = 0; j < SUB_CHUNK_FACTOR; j++)
						{
							if (chunk.SubChunks[i, j].HasMesh)
							{
								subChunkFinishedCount++;
							}
							else if (!chunk.SubChunks[i, j].ShouldHaveMesh)
							{
								ChunkCoords subCoords = new ChunkCoords(coords.x * SUB_CHUNK_FACTOR + i, coords.z * SUB_CHUNK_FACTOR + j);

								chunk.SubChunks[i, j].ShouldHaveMesh = true;
								chunkMeshGenQueue.AddLast((subCoords, lod + 1, chunk.SubChunks[i, j]));
							}
						}
					}

					if (subChunkFinishedCount >= SUB_CHUNK_FACTOR * SUB_CHUNK_FACTOR)
					{ chunk.FinishedAllSubChunks = true; }
				}
			}
			else if (chunk.SubChunks is not null && chunk.FinishedAllSubChunks)
			{
				for (int i = 0; i < SUB_CHUNK_FACTOR; i++)
				{
					for (int j = 0; j < SUB_CHUNK_FACTOR; j++)
					{
						chunk.SubChunks[i, j].SetObjActive(false);

						ChunkCoords subCoords = new ChunkCoords(coords.x * SUB_CHUNK_FACTOR + i, coords.z * SUB_CHUNK_FACTOR + j);
						UpdateSubChunksRecursively(lod + 1, subCoords, chunk.SubChunks[i, j]);
					}
				}
			}
		}

		private IEnumerator UpdateChunkMeshGenQueue()
		{
			while (true)
			{
				if (chunkMeshGenQueue.Count > 0)
				{
					var llnode = chunkMeshGenQueue.First;
					chunkMeshGenQueue.RemoveFirst();

					if (true)
					{
						if (llnode.Value.chunk.HasMesh)
						{ Debug.LogWarning($"The chunkMeshGenQueue got to a chunk that already had a mesh (the lod is {llnode.Value.lod})."); }

						llnode.Value.chunk.GenerateMesh(this, PARTIAL_CHUNK_SCALES[llnode.Value.lod], llnode.Value.coords.x, llnode.Value.coords.z);
					}
				}

				yield return null;
			}
		}

		private bool IsWithinRenderDist(int lod, ChunkCoords chunkCoords)
		{
			ChunkCoords playerCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, lod);

			long coordX = chunkCoords.x;
			long coordZ = chunkCoords.z;
			long pcoordX = playerCoords.x;
			long pcoordZ = playerCoords.z;

			long xDiff = pcoordX - coordX; if (xDiff < 0) { xDiff = -xDiff; }
			long zDiff = pcoordZ - coordZ; if (zDiff < 0) { zDiff = -zDiff; }
			long dist = Math.Max(xDiff, zDiff);

			return dist <= CHUNK_RENDER_DIST;
		}

		private bool IsWithinHalfRenderDist(int lod, ChunkCoords chunkCoords)
		{
			ChunkCoords playerCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, lod);

			long coordX = chunkCoords.x;
			long coordZ = chunkCoords.z;
			long pcoordX = playerCoords.x;
			long pcoordZ = playerCoords.z;

			long xDiff = pcoordX - coordX; if (xDiff < 0) { xDiff = -xDiff; }
			long zDiff = pcoordZ - coordZ; if (zDiff < 0) { zDiff = -zDiff; }
			long dist = Math.Max(xDiff, zDiff);

			return dist <= CHUNK_RENDER_DIST / 2;
		}

		private sealed class Chunk
		{
			public bool ShouldBeActive { get; set; }
			public bool ShouldHaveMesh { get; set; }
			public bool HasMesh { get; private set; }
			public GameObject GameObj { get; }

			private Chunk[,] _subChunks;
			public Chunk[,] SubChunks { get => _subChunks; }
			public bool FinishedAllSubChunks { get; set; }

			public Chunk(TerrainManager terrainManager)
			{
				ShouldBeActive = false;
				ShouldHaveMesh = false;
				HasMesh = false;
				GameObj = new GameObject("ScriptGeneratedChunk");
				_subChunks = null;
				FinishedAllSubChunks = false;

				GameObj.transform.SetParent(terrainManager.originTran);
				GameObj.transform.localPosition = Vector3.zero;
				GameObj.AddComponent<MeshFilter>();
				GameObj.AddComponent<MeshRenderer>();
				GameObj.AddComponent<MeshCollider>();
				GameObj.GetComponent<Renderer>().material = terrainManager.terrainMat;
				GameObj.SetActive(false);
			}

			public void GenerateMesh(TerrainManager terrainManager, float chunkScale, long offX, long offZ)
			{
				if (HasMesh)
				{ Debug.LogWarning("Generating the mesh for a chunk that already has a mesh."); }

				Mesh mesh = ChunkMeshGenerator.MakeMesh(terrainManager.terrainGene, CHUNK_MESH_SIZE, chunkScale, offX, offZ, LodTransitions.None);

				GameObj.GetComponent<MeshFilter>().mesh = mesh;
				GameObj.GetComponent<MeshCollider>().sharedMesh = mesh;

				HasMesh = true;
			}

			public void SetObjActive(bool active)
			{
				if (active && !HasMesh)
				{ Debug.LogWarning("Setting chunk active while it has no mesh."); }

				GameObj.SetActive(active);
			}

			public void MakeSubChunks(TerrainManager terrainManager)
			{
				if (_subChunks is not null)
				{ Debug.LogWarning("Trying to make sub chunks while _subChunks isn't null."); }

				_subChunks = new Chunk[SUB_CHUNK_FACTOR, SUB_CHUNK_FACTOR];

				for (int i = 0; i < SUB_CHUNK_FACTOR; i++)
				{
					for (int j = 0; j < SUB_CHUNK_FACTOR; j++)
					{
						_subChunks[i, j] = new Chunk(terrainManager);
					}
				}
			}
		}

		private readonly struct ChunkCoords : IEquatable<ChunkCoords>
		{
			public readonly long x;
			public readonly long z;

			public ChunkCoords(long x, long z)
			{
				this.x = x;
				this.z = z;
			}

			#nullable enable
			public override bool Equals(object? obj) => obj is ChunkCoords other && this.Equals(other);
			public bool Equals(ChunkCoords that) => this.x == that.x && this.z == that.z;

			public override int GetHashCode()
			{
				uint xa = (uint)x;
				uint za = (uint)z;
				return (int)(xa | (za << 16));
			}
		}

		private ChunkCoords ToChunkCoords(float x, float z, int lod)
		{
			long outX;
			{
				float pre = x / CHUNK_SCALES[lod];
				if (pre < 0.0) { outX = (long)pre - 1; }
				else { outX = (long)pre; }
			}
			long outZ;
			{
				float pre = z / CHUNK_SCALES[lod];
				if (pre < 0.0) { outZ = (long)pre - 1; }
				else { outZ = (long)pre; }
			}

			return new ChunkCoords(outX, outZ);
		}

		private (float x, float z) ToRealCoords(ChunkCoords coords, int lod)
		{
			float outX = coords.x * CHUNK_SCALES[lod];
			float outZ = coords.z * CHUNK_SCALES[lod];

			return (outX, outZ);
		}
	}
}
