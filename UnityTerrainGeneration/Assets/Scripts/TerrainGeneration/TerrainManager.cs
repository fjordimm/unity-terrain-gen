using Unity.VisualScripting;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEditor.Experimental.GraphView;
using UnityEngine.AI;
using UnityEngine.Assertions;

namespace UnityTerrainGeneration.TerrainGeneration
{
	internal class TerrainManager
	{
		private const int CHUNK_MESH_SIZE = 32;
		private const int NUM_LODS = 5;
		private const float LOD0_PARTIAL_CHUNK_SCALE = 0.5f; // The width of one triangle for the lowest LOD chunk
		private static readonly float[] PARTIAL_CHUNK_SCALES = new float[NUM_LODS]; // The width of one triangle for each LOD
		private static readonly float[] CHUNK_SCALES = new float[NUM_LODS]; // The scale of a chunk for each LOD

		private const long CHUNK_RENDER_DIST = 5;

		private readonly MonoBehaviour controller;
		private readonly Transform originTran;
		private readonly Transform playerTran;
		private readonly Material terrainMat;
		private readonly int seed;

		private readonly System.Random rand;
		private readonly TerrainGene terrainGene;
		private readonly Dictionary<ChunkCoords, Chunk>[] chunkDicts;
		private readonly LinkedList<(ChunkCoords coords, Chunk chunk)>[] liveChunkQueues;
		private readonly LinkedList<(ChunkCoords coords, Chunk chunk)>[] meshGenQueues;

		public TerrainManager(MonoBehaviour _controller, Transform _originTran, Transform _playerTran, Material _terrainMat, int _seed)
		{
			for (int i = 0; i < NUM_LODS; i++)
			{
				PARTIAL_CHUNK_SCALES[i] = LOD0_PARTIAL_CHUNK_SCALE * (float)(1 << i);
				CHUNK_SCALES[i] = PARTIAL_CHUNK_SCALES[i] * CHUNK_MESH_SIZE;
			}

			controller = _controller;
			originTran = _originTran;
			playerTran = _playerTran;
			terrainMat = _terrainMat;
			seed = _seed;

			rand = new System.Random(seed);
			terrainGene = new TerrainGene(rand);

			chunkDicts = new Dictionary<ChunkCoords, Chunk>[NUM_LODS];
			for (int i = 0; i < NUM_LODS; i++)
			{ chunkDicts[i] = new Dictionary<ChunkCoords, Chunk>(); }

			liveChunkQueues = new LinkedList<(ChunkCoords coords, Chunk chunk)>[NUM_LODS];
			for (int i = 0; i < NUM_LODS; i++)
			{ liveChunkQueues[i] = new LinkedList<(ChunkCoords coords, Chunk chunk)>(); }

			meshGenQueues = new LinkedList<(ChunkCoords coords, Chunk chunk)>[NUM_LODS];
			for (int i = 0; i < NUM_LODS; i++)
			{ meshGenQueues[i] = new LinkedList<(ChunkCoords coords, Chunk chunk)>(); }
		}

		public void BeginGeneration()
		{
			for (int i = 0; i < NUM_LODS; i++)
			{
				controller.StartCoroutine(EnqueueChunksNearPlayer(i));
				controller.StartCoroutine(UpdateLiveChunkQueue(i));
				controller.StartCoroutine(UpdateMeshGenQueue(i));
			}
		}

		private IEnumerator EnqueueChunksNearPlayer(int lod)
		{
			while (true)
			{
				ChunkCoords goalCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, lod);

				long spiralRenderDist = CHUNK_RENDER_DIST + 1;

				// Spirals out from the player's position
				long cX = goalCoords.x;
				long cZ = goalCoords.z;
				byte cDirection = 0;
				int cLength = 1;
				int cLengthCounter = 0;
				while (cX >= goalCoords.x - spiralRenderDist && cX <= goalCoords.x + spiralRenderDist && cZ >= goalCoords.z - spiralRenderDist && cZ <= goalCoords.z + spiralRenderDist)
				{
					{
						ChunkCoords currentSpiralCoords = new ChunkCoords(cX, cZ);

						Chunk chunk;
						bool alreadyHasChunk = chunkDicts[lod].TryGetValue(currentSpiralCoords, out Chunk ch);
						if (alreadyHasChunk)
						{
							chunk = ch;
						}
						else
						{
							chunk = new Chunk(this);
							chunkDicts[lod].Add(currentSpiralCoords, chunk);
						}

						if (!chunk.InLiveQueue)
						{
							chunk.InLiveQueue = true;
							liveChunkQueues[lod].AddLast((currentSpiralCoords, chunk));
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
					ChunkCoords realChunkCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, lod);
					if (!goalCoords.Equals(realChunkCoords))
					{ break; }

					yield return null;
				}

				yield return null;
			}
		}

		private IEnumerator UpdateLiveChunkQueue(int lod)
		{
			while (true)
			{
				if (liveChunkQueues[lod].Count > 0)
				{
					var llnode = liveChunkQueues[lod].First;
					liveChunkQueues[lod].RemoveFirst();

					bool withinRendDist;
					bool withinLodGap;
					bool withinReasonableDist;
					TestWithinRendDist(lod, llnode.Value.coords, out withinRendDist, out withinLodGap, out withinReasonableDist);

					if (!llnode.Value.chunk.HasMesh && !llnode.Value.chunk.InMeshGenQueue && withinRendDist)
					{
						llnode.Value.chunk.InMeshGenQueue = true;
						meshGenQueues[lod].AddLast((llnode.Value.coords, llnode.Value.chunk));
					}

					bool chunkWasActive = llnode.Value.chunk.IsObjActive();
					bool chunkIsPseudolyActive = llnode.Value.chunk.NumSubChunksPseudolyActive == 4;
					
					if (llnode.Value.chunk.NumSubChunksPseudolyActive > 4)
					{ Debug.LogWarning("The value of NumSubChunksPseudolyActive for a chunk is greater than 4."); }

					if (!llnode.Value.chunk.HasMesh)
					{
						if (withinRendDist && !withinLodGap)
						{
							liveChunkQueues[lod].AddLast(llnode);
						}
						else
						{
							llnode.Value.chunk.InLiveQueue = false;
						}
					}
					else
					{
						if (withinRendDist && !withinLodGap)
						{
							llnode.Value.chunk.SetObjActive(true);
							liveChunkQueues[lod].AddLast(llnode);
						}
						else
						{
							if (withinLodGap && llnode.Value.chunk.NumSubChunksPseudolyActive < 4)
							{
								llnode.Value.chunk.SetObjActive(true);
								liveChunkQueues[lod].AddLast(llnode);
							}
							else if (!withinLodGap && withinReasonableDist && lod < NUM_LODS - 1)
							{
								bool thereIsAnActiveSuperChunk = false;

								ChunkCoords coordsI = llnode.Value.coords;
								int lodI = lod + 1;
								while (lodI < NUM_LODS)
								{
									Chunk chunkI;
									coordsI = new ChunkCoords(coordsI.x / 2, coordsI.z / 2);
									bool alreadyExists = chunkDicts[lodI].TryGetValue(coordsI, out chunkI);

									if (alreadyExists && chunkI.IsObjActive())
									{
										thereIsAnActiveSuperChunk = true;
										break;
									}

									lodI++;
								}

								if (!thereIsAnActiveSuperChunk)
								{
									liveChunkQueues[lod].AddLast(llnode);
								}
								else
								{
									llnode.Value.chunk.SetObjActive(false);
									llnode.Value.chunk.InLiveQueue = false;
								}
							}
							else
							{
								llnode.Value.chunk.SetObjActive(false);
								llnode.Value.chunk.InLiveQueue = false;
							}
						}
					}

					if (llnode.Value.chunk.IsObjActive() && !(chunkWasActive || chunkIsPseudolyActive))
					{
						llnode.Value.chunk.UpdateSuperChunk(this, lod, llnode.Value.coords, true);
					}
					else if (!chunkIsPseudolyActive && !llnode.Value.chunk.IsObjActive() && chunkWasActive)
					{
						llnode.Value.chunk.UpdateSuperChunk(this, lod, llnode.Value.coords, false);
					}
				}

				yield return null;
			}
		}

		private IEnumerator UpdateMeshGenQueue(int lod)
		{
			while (true)
			{
				if (meshGenQueues[lod].Count > 0)
				{
					var llnode = meshGenQueues[lod].First;
					meshGenQueues[lod].RemoveFirst();

					if (true) // Possibly later update this so it only generates the mesh if it is within render dist
					{
						if (llnode.Value.chunk.HasMesh)
						{ Debug.LogWarning($"The meshGenQueue got to a chunk that already had a mesh."); }

						llnode.Value.chunk.GenerateMesh(this, PARTIAL_CHUNK_SCALES[lod], llnode.Value.coords.x, llnode.Value.coords.z);
						llnode.Value.chunk.InMeshGenQueue = false;
					}
				}

				yield return null;
			}
		}

		private void TestWithinRendDist(int lod, ChunkCoords coords, out bool withinRendDist, out bool withinLodGap, out bool withinReasonableDist)
		{
			if (lod == NUM_LODS - 1)
			{
				ChunkCoords playerCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, lod);

				long coordX = coords.x;
				long coordZ = coords.z;
				long pcoordX = playerCoords.x;
				long pcoordZ = playerCoords.z;

				long xDiff = pcoordX - coordX; if (xDiff < 0) { xDiff = -xDiff; }
				long zDiff = pcoordZ - coordZ; if (zDiff < 0) { zDiff = -zDiff; }
				long dist = Math.Max(xDiff, zDiff);

				withinRendDist = dist <= CHUNK_RENDER_DIST;
				withinLodGap = dist <= CHUNK_RENDER_DIST / 2;
				withinReasonableDist = dist <= CHUNK_RENDER_DIST * 2;
			}
			else if (lod == 0)
			{
				ChunkCoords playerCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, lod);

				long coordX = coords.x;
				long coordZ = coords.z;
				long pcoordX = playerCoords.x;
				long pcoordZ = playerCoords.z;

				long coordXr; if (coordX >= 0) coordXr = (coordX / 2) * 2; else coordXr = ((coordX - 1) / 2) * 2;
				long coordZr; if (coordZ >= 0) coordZr = (coordZ / 2) * 2; else coordZr = ((coordZ - 1) / 2) * 2;
				long pcoordXr; if (pcoordX >= 0) pcoordXr = (pcoordX / 2) * 2; else pcoordXr = ((pcoordX - 1) / 2) * 2;
				long pcoordZr; if (pcoordZ >= 0) pcoordZr = (pcoordZ / 2) * 2; else pcoordZr = ((pcoordZ - 1) / 2) * 2;

				long xDiffR = pcoordXr - coordXr; if (xDiffR < 0) { xDiffR = -xDiffR; }
				long zDiffR = pcoordZr - coordZr; if (zDiffR < 0) { zDiffR = -zDiffR; }
				long distR = Math.Max(xDiffR, zDiffR);

				withinRendDist = distR <= CHUNK_RENDER_DIST;
				withinLodGap = false;
				withinReasonableDist = distR <= CHUNK_RENDER_DIST * 2;
			}
			else
			{
				ChunkCoords playerCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, lod);

				long coordX = coords.x;
				long coordZ = coords.z;
				long pcoordX = playerCoords.x;
				long pcoordZ = playerCoords.z;

				long coordXr; if (coordX >= 0) coordXr = (coordX / 2) * 2; else coordXr = ((coordX - 1) / 2) * 2;
				long coordZr; if (coordZ >= 0) coordZr = (coordZ / 2) * 2; else coordZr = ((coordZ - 1) / 2) * 2;
				long pcoordXr; if (pcoordX >= 0) pcoordXr = (pcoordX / 2) * 2; else pcoordXr = ((pcoordX - 1) / 2) * 2;
				long pcoordZr; if (pcoordZ >= 0) pcoordZr = (pcoordZ / 2) * 2; else pcoordZr = ((pcoordZ - 1) / 2) * 2;

				long xDiff = pcoordX - coordX; if (xDiff < 0) { xDiff = -xDiff; }
				long zDiff = pcoordZ - coordZ; if (zDiff < 0) { zDiff = -zDiff; }
				long dist = Math.Max(xDiff, zDiff);

				long xDiffR = pcoordXr - coordXr; if (xDiffR < 0) { xDiffR = -xDiffR; }
				long zDiffR = pcoordZr - coordZr; if (zDiffR < 0) { zDiffR = -zDiffR; }
				long distR = Math.Max(xDiffR, zDiffR);

				withinRendDist = distR <= CHUNK_RENDER_DIST;
				withinLodGap = dist <= CHUNK_RENDER_DIST / 2;
				withinReasonableDist = dist <= CHUNK_RENDER_DIST * 2;
			}
		}

		private sealed class Chunk
		{
			public bool InLiveQueue { get; set; }
			public bool InMeshGenQueue { get; set; }
			public bool HasMesh { get; private set; }
			public sbyte NumSubChunksPseudolyActive { get; private set; }

			// only public for debugging. change back to private
			public GameObject GameObj { get; }

			public Chunk(TerrainManager terrainManager)
			{
				InLiveQueue = false;
				InMeshGenQueue = false;
				HasMesh = false;
				GameObj = new GameObject("ScriptGeneratedChunk");
				NumSubChunksPseudolyActive = 0;

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

			public bool IsObjActive()
			{
				return GameObj.activeSelf;
			}

			public void UpdateSuperChunk(TerrainManager terrainManager, int lod, ChunkCoords coords, bool polarity)
			{
				if (lod < NUM_LODS - 1)
				{
					Chunk superChunk;
					ChunkCoords superCoords = new ChunkCoords(coords.x / 2, coords.z / 2);
					bool alreadyExists = terrainManager.chunkDicts[lod + 1].TryGetValue(superCoords, out superChunk);

					if (!alreadyExists)
					{
						superChunk = new Chunk(terrainManager);
						terrainManager.chunkDicts[lod].Add(superCoords, superChunk);
					}

					if (polarity)
					{
						if (superChunk.NumSubChunksPseudolyActive == 3 && !superChunk.IsObjActive())
						{ UpdateSuperChunk(terrainManager, lod + 1, superCoords, true); }

						superChunk.NumSubChunksPseudolyActive++;
					}
					else
					{
						if (superChunk.NumSubChunksPseudolyActive == 4 && !superChunk.IsObjActive())
						{ UpdateSuperChunk(terrainManager, lod + 1, superCoords, false); }

						superChunk.NumSubChunksPseudolyActive--;
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

		private static ChunkCoords ToSuperCoords(ChunkCoords coords)
		{
			long x = coords.x >= 0 ? coords.x / 2 : (coords.x - 1) / 2;
			long z = coords.z >= 0 ? coords.z / 2 : (coords.z - 1) / 2;
			return new ChunkCoords(x, z);
		}
	}
}
