
using UnityEngine;

namespace UnityTerrainGeneration.TerrainGeneration
{
	public class TerrainOriginController : MonoBehaviour
	{
		[SerializeField]
		private Transform PlayerTran;

		[SerializeField]
		private Material TerrainMat;

		private const ulong Seed = 0;

		private TerrainManager terrainManager;

		void Start()
		{
			terrainManager = new TerrainManager(this.transform, PlayerTran, TerrainMat, Seed);
			terrainManager.Begin();
		}
	}
}
