using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;

namespace UnityTerrainGeneration.TerrainGeneration
{
	internal class TerrainManager
	{
		private const int thingy = 65535;

		// private const int LODSIZE = 7; // Do not make this greater than 7
		// private const float CHUNKSCALE = 0.5f;

		private readonly Transform originTran;
		private readonly Transform playerTran;
		private readonly Material terrainMat;
		private readonly ulong seed;

		private readonly System.Random rand;
		private readonly TerrainGene terrainGene;

		public TerrainManager(Transform _originTran, Transform _playerTran, Material _terrainMat, ulong _seed)
		{
			originTran = _originTran;
			playerTran = _playerTran;
			terrainMat = _terrainMat;
			seed = _seed;

			rand = new System.Random((int)seed);
			terrainGene = new TerrainGene(rand);
		}

		public void BeginGeneration()
		{
			{
				Chunk conk = new Chunk(this);
				conk.GenerateMesh(this, 1f, -1, 0);
				conk.SetObjActive(true);
			}

			{
				Chunk conk = new Chunk(this);
				conk.GenerateMesh(this, 1f, -2, -1);
				conk.SetObjActive(true);
			}

			{
				Chunk conk = new Chunk(this);
				conk.GenerateMesh(this, 0.5f, -3, 0, LodTransitions.Right | LodTransitions.Bottom);
				conk.SetObjActive(true);
			}

			/*
			{
				GameObject chunkGameObj = new("ScriptGeneratedChunk");
				chunkGameObj.transform.SetParent(originTran);
				chunkGameObj.transform.localPosition = Vector3.zero;
				chunkGameObj.AddComponent<MeshFilter>();
				chunkGameObj.AddComponent<MeshRenderer>();
				chunkGameObj.AddComponent<MeshCollider>();
				chunkGameObj.GetComponent<Renderer>().material = terrainMat;

				Mesh mesh = ChunkMeshGenerator.MakeMesh(terrainGene, 16, 1f, -1, 0, LodTransitions.None);

				chunkGameObj.GetComponent<MeshFilter>().mesh = mesh;
				chunkGameObj.GetComponent<MeshCollider>().sharedMesh = mesh;
			}

			{
				GameObject chunkGameObj = new("ScriptGeneratedChunk");
				chunkGameObj.transform.SetParent(originTran);
				chunkGameObj.transform.localPosition = Vector3.zero;
				chunkGameObj.AddComponent<MeshFilter>();
				chunkGameObj.AddComponent<MeshRenderer>();
				chunkGameObj.AddComponent<MeshCollider>();
				chunkGameObj.GetComponent<Renderer>().material = terrainMat;

				Mesh mesh = ChunkMeshGenerator.MakeMesh(terrainGene, 16, 1f, -2, -1, LodTransitions.None);

				chunkGameObj.GetComponent<MeshFilter>().mesh = mesh;
				chunkGameObj.GetComponent<MeshCollider>().sharedMesh = mesh;
			}

			{
				GameObject chunkGameObj = new("ScriptGeneratedChunk");
				chunkGameObj.transform.SetParent(originTran);
				chunkGameObj.transform.localPosition = Vector3.zero;
				chunkGameObj.AddComponent<MeshFilter>();
				chunkGameObj.AddComponent<MeshRenderer>();
				chunkGameObj.AddComponent<MeshCollider>();
				chunkGameObj.GetComponent<Renderer>().material = terrainMat;

				Mesh mesh = ChunkMeshGenerator.MakeMesh(terrainGene, 16, 0.5f, -3, 0, LodTransitions.Right | LodTransitions.Bottom);

				chunkGameObj.GetComponent<MeshFilter>().mesh = mesh;
				chunkGameObj.GetComponent<MeshCollider>().sharedMesh = mesh;
			}
			*/
		}

		private const int CHUNK_SUBDIVS = 4;
		private const int CHUNK_MESH_SIZE = 32;
		private const float CHUNK_0LOD_SCALE = 8.0f;

		private sealed class Chunk
		{
			public bool ShouldBeActive { get; set; }
			public bool HasMesh { get; private set; }

			private Chunk[,] _subChunks;
			public Chunk[,] SubChunks { get => _subChunks; }

			private readonly GameObject _objRef;

			public Chunk(TerrainManager terrainManager)
			{
				_objRef = new GameObject("ScriptGeneratedChunk");
				ShouldBeActive = false;
				HasMesh = false;
				_subChunks = null;

				_objRef.transform.SetParent(terrainManager.originTran);
				_objRef.transform.localPosition = Vector3.zero;
				_objRef.AddComponent<MeshFilter>();
				_objRef.AddComponent<MeshRenderer>();
				_objRef.AddComponent<MeshCollider>();
				_objRef.GetComponent<Renderer>().material = terrainManager.terrainMat;
				_objRef.SetActive(false);
			}

			public void GenerateMesh(TerrainManager terrainManager, float chunkScale, int offX, int offZ, LodTransitions lodTransitions = LodTransitions.None)
			{
				Mesh mesh = ChunkMeshGenerator.MakeMesh(terrainManager.terrainGene, CHUNK_MESH_SIZE, chunkScale, offX, offZ, lodTransitions);

				_objRef.GetComponent<MeshFilter>().mesh = mesh;
				_objRef.GetComponent<MeshCollider>().sharedMesh = mesh;

				HasMesh = true;
			}

			public void MakeSubChunks()
			{
				if (_subChunks == null)
				{ Debug.LogException(new System.Exception("Tryed to call MakeSubChunks after it already had sub chunks.")); }
				else
				{
					_subChunks = new Chunk[CHUNK_SUBDIVS, CHUNK_SUBDIVS];
				}
			}

			public void SetObjActive(bool active)
			{
				if (active && !HasMesh)
				{ Debug.LogWarning("Setting chunk active while it has no mesh."); }

				_objRef.SetActive(active);
			}
		}

		private class ChunkDict
		{
			private readonly Dictionary<uint, Chunk> dict;

			public ChunkDict()
			{
				dict = new Dictionary<uint, Chunk>();
			}
		}
	}
}
