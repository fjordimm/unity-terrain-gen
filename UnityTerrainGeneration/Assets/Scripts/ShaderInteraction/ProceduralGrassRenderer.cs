
// Most code copied directly from https://www.youtube.com/watch?v=DeATXF4Szqo

using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityTerrainGeneration.Controllers;
using UnityTerrainGeneration.TerrainGeneration;

public class ProceduralGrassRenderer : MonoBehaviour
{
	[Serializable]
	public class GrassSettings
	{
		public float maxBendAngle = 0;
		public float bladeHeight = 1;
		public float bladeHeightVariance = 0.1f;
		public float bladeWidth = 1;
		public float bladeWidthVariance = 0.1f;
	}

	private GrassSettings grassSettings = null;
	private ComputeShader grassComputeShader = null;
	private Material material = null;
	private Mesh sourceMesh = null;

	public void SetGrassSettings(GrassSettings grassSettings)
	{ this.grassSettings = grassSettings; }

	public void SetComputeShader(ComputeShader grassComputeShader)
	{ this.grassComputeShader = grassComputeShader; }
	
	public void SetMaterial(Material material)
	{ this.material = material; }

	public void SetSourceMeshAndRender(Mesh sourceMesh)
	{
		this.sourceMesh = sourceMesh;
		OnMeshReady();
	}

	private bool initialized;
	private ComputeBuffer sourceVertBuffer;
	private ComputeBuffer sourceTriBuffer;
	private ComputeBuffer drawBuffer;
	private ComputeBuffer argsBuffer;
	private int idGrassKernel;
	private int dispatchSize;
	private Bounds localBounds;

	private const int SOURCE_VERT_STRIDE = sizeof(float) * 3;
	private const int SOURCE_TRI_STRIDE = sizeof(int);
	private const int DRAW_STRIDE = sizeof(float) * (3 + (3 + 1) * 3);
	private const int INDIRECT_ARGS_STRIDE = sizeof(int) * 4;

	private int[] argsBufferReset = new int[] {0, 1, 0, 0};

	public void OnMeshReady()
	{
		Debug.Assert(grassComputeShader != null, "The grass compute shader is null", gameObject);
		Debug.Assert(material != null, "The material is null", gameObject);

		if (initialized)
		{ OnDisable(); }
		initialized = true;

		Vector3[] positions = sourceMesh.vertices;
		int[] tris = sourceMesh.triangles;

		SourceVertex[] vertices = new SourceVertex[positions.Length];
		for (int i = 0; i < vertices.Length; i++)
		{ vertices[i] = new SourceVertex() { position = positions[i] }; }
		int numSourceTriangles = tris.Length / 3;

		sourceVertBuffer = new ComputeBuffer(vertices.Length, SOURCE_VERT_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
		sourceVertBuffer.SetData(vertices);
		sourceTriBuffer = new ComputeBuffer(tris.Length, SOURCE_TRI_STRIDE, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
		sourceTriBuffer.SetData(tris);
		drawBuffer = new ComputeBuffer(numSourceTriangles, DRAW_STRIDE, ComputeBufferType.Append);
		drawBuffer.SetCounterValue(0);
		argsBuffer = new ComputeBuffer(1, INDIRECT_ARGS_STRIDE, ComputeBufferType.IndirectArguments);

		idGrassKernel = grassComputeShader.FindKernel("Main");

		grassComputeShader.SetBuffer(idGrassKernel, "_SourceVertices", sourceVertBuffer);
		grassComputeShader.SetBuffer(idGrassKernel, "_SourceTriangles", sourceTriBuffer);
		grassComputeShader.SetBuffer(idGrassKernel, "_DrawTriangles", drawBuffer);
		grassComputeShader.SetBuffer(idGrassKernel, "_IndirectArgsBuffer", argsBuffer);
		grassComputeShader.SetInt("_NumSourceTriangles", numSourceTriangles);

		grassComputeShader.SetFloat("_MaxBendAngle", grassSettings.maxBendAngle);
		grassComputeShader.SetFloat("_BladeHeight", grassSettings.bladeHeight);
		grassComputeShader.SetFloat("_BladeHeightVariance", grassSettings.bladeHeightVariance);
		grassComputeShader.SetFloat("_BladeWidth", grassSettings.bladeWidth);
		grassComputeShader.SetFloat("_BladeWidthVariance", grassSettings.bladeWidthVariance);

		material.SetBuffer("_DrawTriangles", drawBuffer);

		grassComputeShader.GetKernelThreadGroupSizes(idGrassKernel, out uint threadGroupSize, out _, out _);
		dispatchSize = Mathf.CeilToInt((float)numSourceTriangles / threadGroupSize);

		localBounds = sourceMesh.bounds;
		localBounds.Expand(Mathf.Max(grassSettings.bladeHeight + grassSettings.bladeHeightVariance, grassSettings.bladeWidth + grassSettings.bladeWidthVariance));
	}

	private void OnDisable()
	{
		if (sourceMesh == null)
		{ return; }

		if (initialized)
		{
			sourceVertBuffer.Release();
			sourceTriBuffer.Release();
			drawBuffer.Release();
			argsBuffer.Release();
		}
		initialized = false;
	}

	private void LateUpdate()
	{
		if (sourceMesh == null)
		{ return; }

		if (Application.isPlaying == false)
		{
			OnDisable();
			OnMeshReady();
		}

		drawBuffer.SetCounterValue(0);
		argsBuffer.SetData(argsBufferReset);

		Bounds bounds = TransformBounds(localBounds);

		grassComputeShader.SetMatrix("_LocalToWorld", transform.localToWorldMatrix);

		grassComputeShader.Dispatch(idGrassKernel, dispatchSize, 1, 1);

		Graphics.DrawProceduralIndirect(material, bounds, MeshTopology.Triangles, argsBuffer, 0, null, null, ShadowCastingMode.Off, true, gameObject.layer);
	}

	public Bounds TransformBounds(Bounds boundsOS)
	{
		var center = transform.TransformPoint(boundsOS.center);

		var extents = boundsOS.extents;
		var axisX = transform.TransformVector(extents.x, 0, 0);
		var axisY = transform.TransformVector(0, extents.y, 0);
		var axisZ = transform.TransformVector(0, 0, extents.z);

		extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
		extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
		extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);

		return new Bounds { center = center, extents = extents };
	}

	[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
	private struct SourceVertex
	{
		public Vector3 position;
	}
}
