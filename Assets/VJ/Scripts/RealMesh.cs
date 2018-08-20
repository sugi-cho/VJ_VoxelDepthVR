using System;
using System.Collections;
using System.Collections.Generic;
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
    Texture2D uvmap;

    PointCloud pc;

    Vector3[] vertices;
    private GCHandle handle;
    private IntPtr verticesPtr;
    int frameSize;
    private IntPtr frameData;

    Intrinsics intrinsics;
    readonly AutoResetEvent e = new AutoResetEvent(false);

    ComputeBuffer vertexBuffer;

    void Start()
    {
        RealSenseDevice.Instance.OnStart += OnStartStreaming;
        RealSenseDevice.Instance.OnStop += OnStopStreaming;
    }

    private void OnStartStreaming(PipelineProfile activeProfile)
    {
        pc = new PointCloud();

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
            uvmap = new Texture2D(profile.Width, profile.Height, TextureFormat.RGFloat, false, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
            };
            GetComponent<MeshRenderer>().sharedMaterial.SetTexture("_UVMap", uvmap);

            if (mesh != null)
                Destroy(mesh);

            mesh = new Mesh()
            {
                indexFormat = IndexFormat.UInt32,
            };

            vertices = new Vector3[profile.Width * profile.Height];
            handle = GCHandle.Alloc(vertices, GCHandleType.Pinned);
            verticesPtr = handle.AddrOfPinnedObject();

            vertexBuffer = new ComputeBuffer(vertices.Length, sizeof(float) * 3);
            vertexBuffer.SetData(vertices);
            renderer.SetBuffer("_VertBuffer", vertexBuffer);

            var indices = new int[(profile.Width - 1) * (profile.Height - 1) * 6];

            mesh.MarkDynamic();
            mesh.vertices = vertices;

            var uvs = new Vector2[vertices.Length];
            Array.Clear(uvs, 0, uvs.Length);
            var invSize = new Vector2(1f / profile.Width, 1f / profile.Height);

            var iIdx = 0;
            for (int j = 0; j < profile.Height; j++)
            {
                for (int i = 0; i < profile.Width; i++)
                {
                    uvs[i + j * profile.Width].x = i * invSize.x;
                    uvs[i + j * profile.Width].y = j * invSize.y;

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

            mesh.uv = uvs;

            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
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

        if (frameData != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(frameData);
            frameData = IntPtr.Zero;
        }

        if (pc != null)
        {
            pc.Dispose();
            pc = null;
        }

        new List<ComputeBuffer>() { vertexBuffer }.ForEach((b) => {
            if (b != null)
                b.Release();
            b = null;
        });
    }

    private void OnFrames(FrameSet frames)
    {
        using (var depthFrame = frames.DepthFrame)
        using (var points = pc.Calculate(depthFrame))
        using (var f = frames.FirstOrDefault<VideoFrame>(stream))
        {
            pc.MapTexture(f);

            memcpy(verticesPtr, points.VertexData, points.Count * 3 * sizeof(float));

            frameSize = depthFrame.Width * depthFrame.Height * 2 * sizeof(float);
            if (frameData == IntPtr.Zero)
                frameData = Marshal.AllocHGlobal(frameSize);
            memcpy(frameData, points.TextureData, frameSize);

            e.Set();
        }
    }

    void Update()
    {
        if (e.WaitOne(0))
        {
            uvmap.LoadRawTextureData(frameData, frameSize);
            uvmap.Apply();

            //mesh.vertices = vertices;
            //mesh.UploadMeshData(false);
            vertexBuffer.SetData(vertices);
            
        }
    }


    [DllImport("msvcrt.dll", EntryPoint = "memcpy", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
    internal static extern IntPtr memcpy(IntPtr dest, IntPtr src, int count);
}
