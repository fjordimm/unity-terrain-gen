﻿using Unity.VisualScripting;
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

						if (!chunk.HasBeenEncountered)
						{
							chunk.HasBeenEncountered = true;
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
					bool withinHalfRendDist;
					bool withinReasonableDist;
					TestWithinRendDist(lod, llnode.Value.coords, out withinRendDist, out withinHalfRendDist, out withinReasonableDist);

					if (!llnode.Value.chunk.HasBeenQueuedForMeshGen && withinRendDist)
					{
						llnode.Value.chunk.HasBeenQueuedForMeshGen = true;
						meshGenQueues[lod].AddLast((llnode.Value.coords, llnode.Value.chunk));

						liveChunkQueues[lod].AddLast(llnode);
					}
					else if (llnode.Value.chunk.HasMesh)
					{
						if (withinRendDist && !withinHalfRendDist)
						{
							llnode.Value.chunk.SetObjActive(true);
							liveChunkQueues[lod].AddLast(llnode);
						}
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

					if (true)
					{
						if (llnode.Value.chunk.HasMesh)
						{ Debug.LogWarning($"The meshGenQueue got to a chunk that already had a mesh."); }

						llnode.Value.chunk.GenerateMesh(this, PARTIAL_CHUNK_SCALES[lod], llnode.Value.coords.x, llnode.Value.coords.z);
					}
				}

				yield return null;
			}
		}

		private void TestWithinRendDist(int lod, ChunkCoords coords, out bool withinRendDist, out bool withinHalfRendDist, out bool withinReasonableDist)
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
				withinHalfRendDist = dist <= CHUNK_RENDER_DIST / 2;
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
				withinHalfRendDist = false;
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
				withinHalfRendDist = dist <= CHUNK_RENDER_DIST / 2;
				withinReasonableDist = dist <= CHUNK_RENDER_DIST * 2;
			}
		}

		private sealed class Chunk
		{
			public bool HasBeenEncountered { get; set; }
			public bool HasBeenQueuedForMeshGen { get; set; }
			public bool HasMesh { get; private set; }
			public GameObject GameObj { get; }

			public Chunk(TerrainManager terrainManager)
			{
				HasBeenEncountered = false;
				HasBeenQueuedForMeshGen = false;
				HasMesh = false;
				GameObj = new GameObject("ScriptGeneratedChunk");

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
