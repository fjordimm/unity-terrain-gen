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
			new Vector3(1f, 0f, 0f),

			new Vector3(1f, 0f, 0f),
			new Vector3(0, 1f, 0f),
			new Vector3(1f, 1f, 0f),

			new Vector3(1f, 0f, 0f),
			new Vector3(1f, 1f, 0f),
			new Vector3(2f, 0f, 0f),

			new Vector3(2f, 0f, 0f),
			new Vector3(1, 1f, 0f),
			new Vector3(2f, 1f, 0f)
		};

		int[] triangles =
		{
			0, 1, 2,
			3, 4, 5,
			6, 7, 8,
			9, 10, 11
		};

		Vector2[] uvs = new Vector2[vertices.Length];
		for (int i = 0; i < vertices.Length; i++)
		{
			uvs[i] = new Vector2(vertices[i].x, vertices[i].y);
		}

		Mesh mesh = new()
		{
			name = "obamameshbitch",
			vertices = vertices,
			triangles = triangles,
			uv = uvs
		};
		mesh.Optimize();

		this.AddComponent<MeshFilter>();
		this.AddComponent<MeshRenderer>();
		this.AddComponent<MeshCollider>();
		this.GetComponent<MeshRenderer>().material = TestBlobMat;
		// this.GetComponent<Renderer>().material = TestBlobMat;

		this.GetComponent<MeshFilter>().mesh = mesh;
		this.GetComponent<MeshCollider>().sharedMesh = mesh;

		///////
		
		Texture2D texture = new Texture2D(10, 10);

		Color[] colors = new Color[10 * 10];
		for (int i = 0; i < 10; i++)
		{
			for (int j = 0; j < 10; j++)
			{
				colors[i * 10 + j] = Color.Lerp(Color.black, Color.white, Random.Range(0f, 1f));
			}
		}

		texture.SetPixels(colors);
		texture.Apply();

		this.GetComponent<MeshRenderer>().material.mainTexture = texture;
	}
}
