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
		private readonly int NumLods = 5;
		private readonly float Lod0PartialChunkScale = 0.25f; // The width of one triangle for the lowest LOD chunk
		private readonly float[] PartialChunkScales; // The width of one triangle for each LOD
		private readonly float[] ChunkScales; // The scale of a chunk for each LOD

		private readonly long RenderDist = 5;

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
			PartialChunkScales = new float[NumLods];
			ChunkScales = new float[NumLods];
			for (int i = 0; i < NumLods; i++)
			{
				PartialChunkScales[i] = Lod0PartialChunkScale * (float)(1 << i);
				ChunkScales[i] = PartialChunkScales[i] * ChunkMeshSize;
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
				for (int lod = 0; lod < NumLods; lod++)
				{
					ChunkCoords goalCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, lod);

					long chunkRenderDist = RenderDist + 1;

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
						{ lod = 0; break; }

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

					bool withinRenderDist;
					bool withinLodGap;
					if (node.Value.lod == NumLods - 1)
					{
						ChunkCoords playerCoords = ToChunkCoords(playerTran.position.x, playerTran.position.z, node.Value.lod);

						long coordX = node.Value.coords.x;
						long coordZ = node.Value.coords.z;
						long pcoordX = playerCoords.x;
						long pcoordZ = playerCoords.z;

						long xDiff = pcoordX - coordX; if (xDiff < 0) { xDiff = -xDiff; }
						long zDiff = pcoordZ - coordZ; if (zDiff < 0) { zDiff = -zDiff; }
						long dist = Math.Max(xDiff, zDiff);

						withinRenderDist = dist <= RenderDist;
						withinLodGap = dist <= RenderDist / 2;
					}
					else if (node.Value.lod == 0)
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

						withinRenderDist = distR <= RenderDist;
						withinLodGap = false;
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

						withinRenderDist = distR <= RenderDist;
						withinLodGap = dist <= RenderDist / 2;
					}

					if (withinRenderDist && !node.Value.chunk.HasMesh)
					{
						node.Value.chunk.GenerateMesh(this, PartialChunkScales[node.Value.lod], (int)node.Value.coords.x, (int)node.Value.coords.z);
					}

					if (withinRenderDist && !withinLodGap)
					{
						node.Value.chunk.SetObjActive(true);
						chunkQueue.AddLast(node);
					}
					else if (withinRenderDist && withinLodGap && node.Value.lod != 0)
					{
						ChunkCoords mini1Coords = new ChunkCoords(node.Value.coords.x * 2, node.Value.coords.z * 2);
						ChunkCoords mini2Coords = new ChunkCoords(node.Value.coords.x * 2 + 1, node.Value.coords.z * 2);
						ChunkCoords mini3Coords = new ChunkCoords(node.Value.coords.x * 2, node.Value.coords.z * 2 + 1);
						ChunkCoords mini4Coords = new ChunkCoords(node.Value.coords.x * 2 + 1, node.Value.coords.z * 2 + 1);

						bool gotMini1 = chunkDicts[node.Value.lod - 1].TryGetValue(mini1Coords, out Chunk mini1);
						bool gotMini2 = chunkDicts[node.Value.lod - 1].TryGetValue(mini2Coords, out Chunk mini2);
						bool gotMini3 = chunkDicts[node.Value.lod - 1].TryGetValue(mini3Coords, out Chunk mini3);
						bool gotMini4 = chunkDicts[node.Value.lod - 1].TryGetValue(mini4Coords, out Chunk mini4);

						if (gotMini1 && mini1.HasMesh && gotMini2 && mini2.HasMesh && gotMini3 && mini3.HasMesh && gotMini4 && mini4.HasMesh)
						{
							node.Value.chunk.ShouldBeActive = false;
							node.Value.chunk.SetObjActive(false);
						}
						else
						{
							node.Value.chunk.SetObjActive(true);
							chunkQueue.AddLast(node);

							// node.Value.chunk.objRef.transform.transform.position += new Vector3(0f, 30f, 0f);

							/*
							// DEBUG:
							if (gotMini1) mini1.objRef.transform.transform.position += new Vector3(0f, -10f, 0f);
							if (gotMini2) mini2.objRef.transform.transform.position += new Vector3(0f, -10f, 0f);
							if (gotMini3) mini3.objRef.transform.transform.position += new Vector3(0f, -10f, 0f);
							if (gotMini4) mini4.objRef.transform.transform.position += new Vector3(0f, -10f, 0f);
							*/
						}
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

			public readonly GameObject objRef;

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
				Mesh mesh = ChunkMeshGenerator.MakeMesh(terrainManager.terrainGene, terrainManager.ChunkMeshSize, chunkScale, offX, offZ, lodTransitions);

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
