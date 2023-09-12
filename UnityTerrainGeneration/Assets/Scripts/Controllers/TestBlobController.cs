using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.TerrainUtils;

public class TestBlobController : MonoBehaviour
{
	[SerializeField]
	private Material TestBlobMat;

	private void Start()
    {
        Vector3[] vertices =
		{
			new Vector3(0f, 0f, 0f),
			new Vector3(0f, 1f, 0f),
			new Vector3(1f, 0f, 0f)
		};

		int[] triangles =
		{
			0, 1, 2
		};

		Color[] colors =
		{
			new Color(1f, 0f, 0f),
			new Color(0f, 1f, 0f),
			new Color(0f, 0f, 1f)
		};

		Mesh mesh = new()
		{
			name = "obamameshbitch",
			vertices = vertices,
			triangles = triangles,
			colors = colors,
		};
		mesh.Optimize();

		this.AddComponent<MeshFilter>();
		this.AddComponent<MeshRenderer>();
		this.AddComponent<MeshCollider>();
		this.GetComponent<Renderer>().material = TestBlobMat;

		this.GetComponent<MeshFilter>().mesh = mesh;
		this.GetComponent<MeshCollider>().sharedMesh = mesh;
	}
}
