
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
		private readonly MonoBehaviour controller;
		private readonly Transform originTran;
		private readonly Transform playerTran;
		private readonly Material terrainMat;
		private readonly int seed;

		//private static readonly ProceduralGrassRenderer.GrassSettings GRASS_SETTINGS = new()
		//{ maxBendAngle = 0.1f, bladeHeight = 3f, bladeHeightVariance = 0.9f, bladeWidth = 1f, bladeWidthVariance = 0.1f };

		private static ProceduralGrassRenderer.GrassSettings GrassSettings(float chunkScale, float grassEndSteepness)
		{
			return new ProceduralGrassRenderer.GrassSettings()
			{
				bladeMultipleSpread = chunkScale,
				bladeMultipleAmount = (uint)Mathf.CeilToInt(chunkScale),
				maxBendAngle = 0.1f,
				bladeHeight = 3f,
				bladeHeightVariance = 0.9f,
				bladeWidth = 0.3f,
				bladeWidthVariance = 0.01f,
				grassEndSteepness = grassEndSteepness
			};
		}

		private readonly ComputeShader grassComputeShader;
		private readonly Material grassMaterial;
		
		private readonly System.Random rand;
		private readonly TerrainGene terrainGene;

		public TerrainManager(MonoBehaviour _controller, Transform _originTran, Transform _playerTran, Material _terrainMat, int _seed, ComputeShader _grassComputeShader, Material _grassMaterial)
		{
			controller = _controller;
			originTran = _originTran;
			playerTran = _playerTran;
			terrainMat = _terrainMat;
			seed = _seed;

			grassComputeShader = _grassComputeShader;
			grassMaterial = _grassMaterial;

			rand = new System.Random(seed);
			terrainGene = new TerrainGene(rand);
		}

		private const int TEMP_CHUNK_SIZE = 64;
		private const float TEMP_CHUNK_SCALE = 3f;
		public async void BeginGeneration()
		{
			GameObject chonk = new GameObject("HahaImChonk");

			chonk.transform.SetParent(this.originTran);
			chonk.transform.localPosition = Vector3.zero;
			chonk.layer = this.originTran.gameObject.layer;

			chonk.AddComponent<MeshFilter>();
			chonk.AddComponent<MeshRenderer>();
			chonk.AddComponent<MeshCollider>();

			MeshRenderer chonkRenderer = chonk.GetComponent<MeshRenderer>();
			chonkRenderer.material = this.terrainMat;

			Mesh mosh = ChunkMeshGenerator.MakeMesh(this.terrainGene, TEMP_CHUNK_SIZE, TEMP_CHUNK_SCALE, 0, 0);
			chonk.GetComponent<MeshFilter>().sharedMesh = mosh;
			chonk.GetComponent<MeshCollider>().sharedMesh = mosh;
			
			(Texture mainTexture, Texture bumpMap) = await ChunkMeshGenerator.MakeTexture(this.terrainGene, TEMP_CHUNK_SIZE, TEMP_CHUNK_SCALE, 0, 0, 1);
			chonkRenderer.material.mainTexture = mainTexture;
			chonkRenderer.material.EnableKeyword("_NORMALMAP");
			chonkRenderer.material.SetTexture("_BumpMap", bumpMap);

			chonk.AddComponent<ProceduralGrassRenderer>();
			ProceduralGrassRenderer proceduralGrassRenderer = chonk.GetComponent<ProceduralGrassRenderer>();
			proceduralGrassRenderer.SetGrassSettings(GrassSettings(TEMP_CHUNK_SCALE, terrainGene.GrassEndSteepness()));
			proceduralGrassRenderer.SetComputeShader(grassComputeShader);
			proceduralGrassRenderer.SetMaterial(grassMaterial);
			proceduralGrassRenderer.SetSourceMeshAndRender(mosh);
		}
	}
}
