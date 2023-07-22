using UnityEngine;
using UnityTerrainGeneration.TerrainGeneration;

namespace UnityTerrainGeneration.Controllers
{
	public class TerrainOriginController : MonoBehaviour
	{
		[SerializeField]
		private Transform PlayerTran;

		[SerializeField]
		private Material TerrainMat;

		private const int SEED = 0;

		private TerrainManager terrainManager;

		void Start()
		{
			terrainManager = new TerrainManager(this, this.transform, PlayerTran, TerrainMat, SEED);
			terrainManager.BeginGeneration();
		}
	}
}
