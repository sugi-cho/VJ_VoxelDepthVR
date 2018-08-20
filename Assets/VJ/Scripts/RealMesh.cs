﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Intel.RealSense;
using UnityEngine.Rendering;
using UnityEngine.Assertions;
using System.Runtime.InteropServices;
using System.Threading;

using sugi.cc;

//this class is copy of RealSensePointCloudGenerator class
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RealMesh : RendererBehaviour
{
    public Stream stream = Stream.Color;
    Mesh mesh;

    PointCloud pc;

    private GCHandle handle;
    private IntPtr verticesPtr;
    private IntPtr preVerticesPtr;

    public bool pause;

    Intrinsics intrinsics;
    readonly AutoResetEvent e = new AutoResetEvent(false);

    public ComputeShader compute;
    Vector3[] vertices;
    Vector3[] preVertices;
    ComputeBuffer particleBuffer;
    ComputeBuffer vertexBuffer;
    ComputeBuffer prevBuffer;
    ComputeBuffer indicesBuffer;

    SpatialFilter spatial;
    TemporalFilter temporal;
    HoleFillingFilter holeFilling;

    int numParticles;

    void Start()
    {
        RealSenseDevice.Instance.OnStart += OnStartStreaming;
        RealSenseDevice.Instance.OnStop += OnStopStreaming;
    }

    private void OnStartStreaming(PipelineProfile activeProfile)
    {
        pc = new PointCloud();
        spatial = new SpatialFilter();
        temporal = new TemporalFilter();
        holeFilling = new HoleFillingFilter();

        using (var profile = activeProfile.GetStream(stream))
        {
            if (profile == null)
            {
                Debug.LogWarningFormat("Stream {0} not in active profile", stream);
            }
        }

        using (var profile = activeProfile.GetStream(Stream.Depth) as VideoStreamProfile)
        {
            intrinsics = profile.GetIntrinsics();

            Assert.IsTrue(SystemInfo.SupportsTextureFormat(TextureFormat.RGFloat));


            numParticles = (profile.Width - 1) * (profile.Height - 1) * 2;

            vertices = new Vector3[profile.Width * profile.Height];
            preVertices = new Vector3[profile.Width * profile.Height];
            handle = GCHandle.Alloc(vertices, GCHandleType.Pinned);
            verticesPtr = handle.AddrOfPinnedObject();
            handle = GCHandle.Alloc(preVertices, GCHandleType.Pinned);
            preVerticesPtr = handle.AddrOfPinnedObject();

            var indices = new int[(profile.Width - 1) * (profile.Height - 1) * 6];

            var iIdx = 0;
            for (int j = 0; j < profile.Height; j++)
            {
                for (int i = 0; i < profile.Width; i++)
                {
                    if (i < profile.Width - 1 && j < profile.Height - 1)
                    {
                        var idx = i + j * profile.Width;
                        var y = profile.Width;
                        indices[iIdx++] = idx + 0;
                        indices[iIdx++] = idx + y;
                        indices[iIdx++] = idx + 1;

                        indices[iIdx++] = idx + 1;
                        indices[iIdx++] = idx + y;
                        indices[iIdx++] = idx + y + 1;
                    }
                }
            }

            particleBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(VoxelParticle)));
            vertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            prevBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            indicesBuffer = new ComputeBuffer(indices.Length, sizeof(int));

            vertexBuffer.SetData(vertices);
            indicesBuffer.SetData(indices);
            renderer.SetBuffer("_VoxelBuffer", particleBuffer);

            if (mesh != null)
                Destroy(mesh);

            mesh = new Mesh()
            {
                indexFormat = IndexFormat.UInt32,
            };
            mesh.MarkDynamic();

            mesh.vertices = new Vector3[numParticles];
            var newIdices = Enumerable.Range(0, numParticles).ToArray();

            mesh.SetIndices(newIdices, MeshTopology.Points, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10f);

            GetComponent<MeshFilter>().sharedMesh = mesh;
        }

        RealSenseDevice.Instance.onNewSampleSet += OnFrames;
    }

    void OnDestroy()
    {
        OnStopStreaming();
    }


    private void OnStopStreaming()
    {
        // RealSenseDevice.Instance.onNewSampleSet -= OnFrames;

        e.Reset();

        if (handle.IsAllocated)
            handle.Free();

        if (pc != null)
        {
            pc.Dispose();
            pc = null;
        }

        new List<ComputeBuffer>() { particleBuffer, prevBuffer, vertexBuffer }.ForEach((b) =>
          {
              if (b != null)
                  b.Release();
              b = null;
          });
    }

    private void OnFrames(FrameSet frames)
    {
        using (var depthFrame =
                holeFilling.ApplyFilter(
                temporal.ApplyFilter(
                spatial.ApplyFilter(
                frames.DepthFrame))))
        using (var points = pc.Calculate(depthFrame))
        using (var f = frames.FirstOrDefault<VideoFrame>(stream))
        {
            pc.MapTexture(f);
            memcpy(preVerticesPtr, verticesPtr, points.Count * 3 * sizeof(float));
            memcpy(verticesPtr, points.VertexData, points.Count * 3 * sizeof(float));

            e.Set();
        }
    }

    void Update()
    {
        if (pause)
            return;
        if (e.WaitOne(0))
        {
            vertexBuffer.SetData(vertices);
            prevBuffer.SetData(preVertices);

            var kernel = compute.FindKernel("build");
            compute.SetBuffer(kernel, "_ParticleBuffer", particleBuffer);
            compute.SetBuffer(kernel, "_PrevBuffer", prevBuffer);
            compute.SetBuffer(kernel, "_VertBuffer", vertexBuffer);
            compute.SetBuffer(kernel, "_IndicesBuffer", indicesBuffer);
            compute.Dispatch(kernel, numParticles / 8 + 1, 1, 1);
        }
    }

    public struct VoxelParticle
    {
        public Vector3 pos;
        public Vector3 vel;
        public Vector3 normal;
        public float prop;
        public float t;
        public float size;
    }

    [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    internal static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);
}