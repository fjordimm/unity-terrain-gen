using UnityEngine;
using UnityTerrainGeneration.TerrainGeneration;

namespace UnityTerrainGeneration.Controllers
{
	public class TerrainOriginController : MonoBehaviour
	{
		[SerializeField] private Transform PlayerTran;
		[SerializeField] private Material TerrainMat;

		[SerializeField] private ComputeShader GrassComputeShader;
		[SerializeField] private Material GrassMaterial;

		private const int SEED = 0;

		private TerrainManager terrainManager;

		private void Start()
		{
			terrainManager = new TerrainManager(this, this.transform, PlayerTran, TerrainMat, SEED, GrassComputeShader, GrassMaterial);
			terrainManager.BeginGeneration();


			// proceduralGrassRenderer.OnSourceMeshReady();
		}

		/*
		public async void BeginGenerationForTerrainManager()
		{
			terrainManager = new TerrainManager(this, this.transform, PlayerTran, TerrainMat, SEED);
			terrainManager.BeginGeneration();
		}
		*/
	}
}
