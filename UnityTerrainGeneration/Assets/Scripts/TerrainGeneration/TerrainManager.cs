using Unity.VisualScripting;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEditor.Experimental.GraphView;

namespace UnityTerrainGeneration.TerrainGeneration
{
	internal class TerrainManager
	{
		private readonly int ChunkMeshSize = 32;
		private readonly int NumLods = 4;
		private readonly float ChunkLod0PartialScale = 8.0f; // The width of one triangle for the lowest LOD chunk
		private readonly float[] ChunkScales; // The scale of a chunk for each LOD

		private readonly long RenderDist = 4;

		private readonly MonoBehaviour controller;
		private readonly Transform originTran;
		private readonly Transform playerTran;
		private readonly Material terrainMat;
		private readonly ulong seed;

		private readonly System.Random rand;
		private readonly TerrainGene terrainGene;
		private readonly Dictionary<ChunkCoords, Chunk>[] chunkDicts;
		private readonly LinkedList<(ChunkCoords coords, int lod, Chunk chunk)> chunkQueue;

		public TerrainManager(MonoBehaviour _controller, Transform _originTran, Transform _playerTran, Material _terrainMat, ulong _seed)
		{
			ChunkScales = new float[NumLods];
			for (int i = 0; i < ChunkScales.Length; i++)
			{
				ChunkScales[i] = ChunkLod0PartialScale * ChunkMeshSize / (float)(1 << i);
			}

			controller = _controller;
			originTran = _originTran;
			playerTran = _playerTran;
			terrainMat = _terrainMat;
			seed = _seed;

			rand = new System.Random((int)seed);
			terrainGene = new TerrainGene(rand);
			chunkDicts = new Dictionary<ChunkCoords, Chunk>[NumLods];
			for (int i = 0; i < NumLods; i++)
			{ chunkDicts[i] = new Dictionary<ChunkCoords, Chunk>(); }
			chunkQueue = new LinkedList<(ChunkCoords coords, int lod, Chunk chunk)>();

			// DEBUG:
			/*
			for (int i = NumLods - 1; i >= 0; i--)
			{
				string thingy = "";
				for (float f = -100f; f <= 100f; f += 3f)
				{
					thingy += String.Format("{0, -3}", ToChunkCoords(f, 0f, i).x);
				}
				Debug.Log($"Lod #{i}: " + thingy);
			}
			*/
		}

		public void BeginGeneration()
		{
			controller.StartCoroutine(EnqueueChunksNearPlayer());
			controller.StartCoroutine(UpdateChunkQueue());
		}

		private IEnumerator EnqueueChunksNearPlayer()
		{
			while (true)
			{
				for (int lod = NumLods - 1; lod >= 0; lod--)
				{
					ChunkCoords goalCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, lod);

					// Possible Optimization: precalculate the chunk render distances
					long chunkRenderDist = RenderDist * 2;

					// Spirals out from the player's position
					long cX = goalCoords.x;
					long cZ = goalCoords.z;
					byte cDirection = 0;
					int cLength = 1;
					int cLengthCounter = 0;
					while (cX >= goalCoords.x - chunkRenderDist && cX <= goalCoords.x + chunkRenderDist && cZ >= goalCoords.z - chunkRenderDist && cZ <= goalCoords.z + chunkRenderDist)
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

							if (!chunk.ShouldBeActive)
							{
								chunk.ShouldBeActive = true;
								chunkQueue.AddLast((currentSpiralCoords, lod, chunk));
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
						{ lod = NumLods - 1; break; }

						yield return null;
					}

					yield return null;
				}

				yield return null;
			}
		}

		private IEnumerator UpdateChunkQueue()
		{
			while (true)
			{
				if (chunkQueue.Count > 0)
				{
					var node = chunkQueue.First;
					chunkQueue.RemoveFirst();

					bool shouldRenderMesh;
					if (node.Value.lod == 0)
					{
						ChunkCoords playerCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, node.Value.lod);

						long coordX = node.Value.coords.x;
						long coordZ = node.Value.coords.z;
						long pcoordX = playerCoords.x;
						long pcoordZ = playerCoords.z;

						long xDiff = pcoordX - coordX; if (xDiff < 0) { xDiff = -xDiff; }
						long zDiff = pcoordZ - coordZ; if (zDiff < 0) { zDiff = -zDiff; }
						long dist = Math.Max(xDiff, zDiff);

						shouldRenderMesh = dist <= RenderDist && dist > RenderDist / 2;
					}
					else if (node.Value.lod == NumLods - 1)
					{
						ChunkCoords playerCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, node.Value.lod);

						long coordX = node.Value.coords.x;
						long coordZ = node.Value.coords.z;
						long pcoordX = playerCoords.x;
						long pcoordZ = playerCoords.z;

						long coordXr; if (coordX >= 0) coordXr = (coordX / 2) * 2; else coordXr = ((coordX - 1) / 2) * 2;
						long coordZr; if (coordZ >= 0) coordZr = (coordZ / 2) * 2; else coordZr = ((coordZ - 1) / 2) * 2;
						long pcoordXr; if (pcoordX >= 0) pcoordXr = (pcoordX / 2) * 2; else pcoordXr = ((pcoordX - 1) / 2) * 2;
						long pcoordZr; if (pcoordZ >= 0) pcoordZr = (pcoordZ / 2) * 2; else pcoordZr = ((pcoordZ - 1) / 2) * 2;

						long xDiffR = pcoordXr - coordXr; if (xDiffR < 0) { xDiffR = -xDiffR; }
						long zDiffR = pcoordZr - coordZr; if (zDiffR < 0) { zDiffR = -zDiffR; }
						long distR = Math.Max(xDiffR, zDiffR);

						shouldRenderMesh = distR <= RenderDist;
					}
					else
					{
						ChunkCoords playerCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, node.Value.lod);

						long coordX = node.Value.coords.x;
						long coordZ = node.Value.coords.z;
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

						shouldRenderMesh = distR <= RenderDist && dist > RenderDist / 2;
					}
					
					if (shouldRenderMesh)
					{
						if (!node.Value.chunk.HasMesh)
						{
							node.Value.chunk.GenerateMesh(this, ChunkScales[node.Value.lod], (int)node.Value.coords.x, (int)node.Value.coords.z);
						}

						node.Value.chunk.SetObjActive(true);
						chunkQueue.AddLast(node);
					}
					else
					{
						node.Value.chunk.ShouldBeActive = false;
						node.Value.chunk.SetObjActive(false);
					}
				}

				yield return null;
			}
		}

		private sealed class Chunk
		{
			public bool ShouldBeActive { get; set; }
			public bool HasMesh { get; private set; }
			public LodTransitions CurrentLodTrans { get; private set; }

			private readonly GameObject objRef;

			public Chunk(TerrainManager terrainManager)
			{
				objRef = new GameObject("ScriptGeneratedChunk");
				ShouldBeActive = false;
				HasMesh = false;

				objRef.transform.SetParent(terrainManager.originTran);
				objRef.transform.localPosition = Vector3.zero;
				objRef.AddComponent<MeshFilter>();
				objRef.AddComponent<MeshRenderer>();
				objRef.AddComponent<MeshCollider>();
				objRef.GetComponent<Renderer>().material = terrainManager.terrainMat;
				objRef.SetActive(false);
			}

			public void GenerateMesh(TerrainManager terrainManager, float chunkScale, int offX, int offZ, LodTransitions lodTransitions = LodTransitions.None)
			{
				Mesh mesh = ChunkMeshGenerator.MakeMesh(terrainManager.terrainGene, terrainManager.ChunkMeshSize, chunkScale / terrainManager.ChunkMeshSize, offX, offZ, lodTransitions);

				objRef.GetComponent<MeshFilter>().mesh = mesh;
				objRef.GetComponent<MeshCollider>().sharedMesh = mesh;

				HasMesh = true;
				CurrentLodTrans = lodTransitions;
			}

			public void SetObjActive(bool active)
			{
				if (active && !HasMesh)
				{ Debug.LogWarning("Setting chunk active while it has no mesh."); }

				objRef.SetActive(active);
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
				float pre = x / ChunkScales[lod];
				if (pre < 0.0) { outX = (long)pre - 1; }
				else { outX = (long)pre; }
			}
			long outZ;
			{
				float pre = z / ChunkScales[lod];
				if (pre < 0.0) { outZ = (long)pre - 1; }
				else { outZ = (long)pre; }
			}

			return new ChunkCoords(outX, outZ);
		}

		private (float x, float z) ToRealCoords(ChunkCoords coords, int lod)
		{
			float outX = coords.x * ChunkScales[lod];
			float outZ = coords.z * ChunkScales[lod];

			return (outX, outZ);
		}
	}
}
