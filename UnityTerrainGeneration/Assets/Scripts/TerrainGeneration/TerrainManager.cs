using Unity.VisualScripting;
using UnityEngine;
using System.Collections.Generic;

namespace UnityTerrainGeneration.TerrainGeneration
{
	internal class TerrainManager
	{
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
			GameObject chunkGameObj = new("ScriptGeneratedChunk");
			chunkGameObj.transform.SetParent(originTran);
			chunkGameObj.transform.localPosition = Vector3.zero;
			chunkGameObj.AddComponent<MeshFilter>();
			chunkGameObj.AddComponent<MeshRenderer>();
			chunkGameObj.AddComponent<MeshCollider>();
			chunkGameObj.GetComponent<Renderer>().material = terrainMat;

			Mesh mesh = ChunkMeshGenerator.MakeMesh(terrainGene, 10, 1f, 0, 0);

			chunkGameObj.GetComponent<MeshFilter>().mesh = mesh;
			chunkGameObj.GetComponent<MeshCollider>().sharedMesh = mesh;
		}

		private const int CHUNK_SUBDIVS = 4;

		private class Chunk
		{
			public GameObject ObjRef { get; }
			public bool ShouldBeActive { get; set; }
			public bool HasMesh { get; set; }

			private Chunk[,] _subChunks;
			public Chunk[,] SubChunks { get => _subChunks; }

			private Chunk(GameObject objRef)
			{
				ObjRef = objRef;
				ShouldBeActive = false;
				HasMesh = false;
				_subChunks = null;
			}

			public Chunk(TerrainManager terrainManager) : this(new GameObject("ScriptGeneratedChunk"))
			{
				ObjRef.transform.SetParent(terrainManager.originTran);
				ObjRef.transform.localPosition = Vector3.zero;
				ObjRef.AddComponent<MeshFilter>();
				ObjRef.AddComponent<MeshRenderer>();
				ObjRef.AddComponent<MeshCollider>();
				ObjRef.GetComponent<Renderer>().material = terrainManager.terrainMat;
			}

			public void GenerateMesh(TerrainManager terrainManager)
			{
				Mesh mesh = ChunkMeshGenerator.MakeMesh(terrainManager.terrainGene, 10, 1f, 0, 0);

				ObjRef.GetComponent<MeshFilter>().mesh = mesh;
				ObjRef.GetComponent<MeshCollider>().sharedMesh = mesh;

				HasMesh = true;
			}

			public void MakeSubChunks()
			{
				if (_subChunks != null)
				{
					_subChunks = new Chunk[CHUNK_SUBDIVS, CHUNK_SUBDIVS];
				}
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
