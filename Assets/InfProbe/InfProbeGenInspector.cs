using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CustomEditor(typeof(InfProbeGen))]
public class InfProbeGenInspector : Editor
{
    private const float RAY_COMP_EPSILON = 0.000001f;
    private const int PROBE_RENDER_SIZE = 64;


    private InfProbeGen probeGen;
    private List<MeshFilter> meshList = new List<MeshFilter>();

    private ComputeBuffer bufTmpBuffer;


    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool TGBuildTets(float[] vertices, uint numVert);
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint TGGetTetIndexCount();
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TGGetTetIndices(uint[] vOut);


    private void _findAllMeshes()
    {
        meshList.Clear();

        foreach (var gObj in Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[])
        {
            if (!gObj.activeSelf)
                continue;

            if (!EditorUtility.IsPersistent(gObj.transform.root.gameObject) && !(gObj.hideFlags == HideFlags.NotEditable || gObj.hideFlags == HideFlags.HideAndDontSave))
            {
                //foreach(var mesh in gObj.GetComponents<MeshFilter>())
                //{
                //    if ((mesh.GetComponents<MeshRenderer>().Length + mesh.GetComponentsInChildren<MeshRenderer>().Length) <= 0)
                //        continue;

                //    meshList.Add(mesh);
                //}

                foreach (var mesh in gObj.GetComponentsInChildren<MeshFilter>(false))
                {
                    if ((mesh.GetComponents<MeshRenderer>().Length + mesh.GetComponentsInChildren<MeshRenderer>().Length) <= 0)
                        continue;

                    meshList.Add(mesh);
                }
            }
        }
    }

    private void _generateTets()
    {
        var vAABBMin = probeGen.transform.position - probeGen.vAABBExtents;
        var vAABBMax = probeGen.transform.position + probeGen.vAABBExtents;

        int iLen = 0;
        for (float fZ = vAABBMin.z; fZ <= vAABBMax.z; fZ += probeGen.vProbeSpacing.z)
            for (float fY = vAABBMin.y; fY <= vAABBMax.y; fY += probeGen.vProbeSpacing.y)
                for (float fX = vAABBMin.x; fX <= vAABBMax.x; fX += probeGen.vProbeSpacing.x)
                    ++iLen;
        probeGen.vProbes = new Vector3[iLen];
        float[] fPassProbes = new float[iLen * 3];

        iLen = 0;
        for (float fZ = vAABBMin.z; fZ <= vAABBMax.z; fZ += probeGen.vProbeSpacing.z)
        {
            for (float fY = vAABBMin.y; fY <= vAABBMax.y; fY += probeGen.vProbeSpacing.y)
            {
                for (float fX = vAABBMin.x; fX <= vAABBMax.x; fX += probeGen.vProbeSpacing.x)
                {
                    fPassProbes[iLen * 3 + 0] = fX;
                    fPassProbes[iLen * 3 + 1] = fY;
                    fPassProbes[iLen * 3 + 2] = fZ;
                    probeGen.vProbes[iLen++] = new Vector3(fX, fY, fZ);
                }
            }
        }

        {
            TGBuildTets(fPassProbes, (uint)(fPassProbes.Length));

            iLen = (int)TGGetTetIndexCount();
            uint[] uRawTetIndices = new uint[iLen];
            TGGetTetIndices(uRawTetIndices);

            iLen >>= 2;
            probeGen.vTetIndices = new TetIndex[iLen];
            probeGen.vTetDepthMap = new TetDepthMap[iLen];

            for (int i = 0; i < iLen; ++i)
            {
                probeGen.vTetIndices[i]._0 = (int)uRawTetIndices[i * 4 + 0];
                probeGen.vTetIndices[i]._1 = (int)uRawTetIndices[i * 4 + 1];
                probeGen.vTetIndices[i]._2 = (int)uRawTetIndices[i * 4 + 2];
                probeGen.vTetIndices[i]._3 = (int)uRawTetIndices[i * 4 + 3];
            }
        }
    }

    private void _rebuildSH(ref Camera camera, ref Cubemap texture, Vector3 vPos)
    {
        camera.transform.position = vPos;
        camera.transform.rotation = Quaternion.identity;
        camera.RenderToCubemap(texture);

        var iKernel = probeGen.shdSHIntegrator.FindKernel("CSMain");

        probeGen.shdSHIntegrator.SetTexture(iKernel, "texEnv", texture);
        probeGen.shdSHIntegrator.SetBuffer(iKernel, "bufCoeff", bufTmpBuffer);

        probeGen.shdSHIntegrator.Dispatch(iKernel, PROBE_RENDER_SIZE >> 1, PROBE_RENDER_SIZE >> 1, 6);

        //bufTmpBuffer.GetData();

    }
    private void _rebuildSHs()
    {
        var gObj = new GameObject("ProbeCamera");
        var camera = gObj.AddComponent<Camera>();
        var texture = new Cubemap(PROBE_RENDER_SIZE, TextureFormat.RGB24, false);

        camera.allowHDR = false;
        camera.allowMSAA = false;
        camera.backgroundColor = Color.black;
        camera.aspect = 1.0f;

        for (int i = 0; i < probeGen.vProbes.Length; ++i)
        {
            var vPos = probeGen.vProbes[i];

            _rebuildSH(ref camera, ref texture, vPos);
        }

        DestroyImmediate(gObj);
    }

    private static ref byte _depthToIndex(ref TetDepth tetDepth, int iID)
    {
        switch (iID)
        {
            case 0:
                return ref tetDepth._00;
            case 1:
                return ref tetDepth._01;
            case 2:
                return ref tetDepth._02;
            case 3:
                return ref tetDepth._03;
            case 4:
                return ref tetDepth._04;
            case 5:
                return ref tetDepth._05;
            case 6:
                return ref tetDepth._06;
            case 7:
                return ref tetDepth._07;
            case 8:
                return ref tetDepth._08;
            case 9:
                return ref tetDepth._09;
            case 10:
                return ref tetDepth._10;
            case 11:
                return ref tetDepth._11;
            case 12:
                return ref tetDepth._12;
            case 13:
                return ref tetDepth._13;
            case 14:
                return ref tetDepth._14;
        }
        return ref tetDepth._14;
    }
    private static bool _rayTriIntersect(Ray ray, Vector3 v0, Vector3 v1, Vector3 v2, ref float t)
    {
        Vector3 e1 = v1 - v0;
        Vector3 e2 = v2 - v0;

        Vector3 h = Vector3.Cross(ray.direction, e2);
        float a = Vector3.Dot(e1, h);
        if ((a > -RAY_COMP_EPSILON) && (a < RAY_COMP_EPSILON))
            return false;

        float f = 1.0f / a;

        Vector3 s = ray.origin - v0;
        float u = f * Vector3.Dot(s, h);
        if ((u < 0.0f) || (u > 1.0f))
            return false;

        Vector3 q = Vector3.Cross(s, e1);
        float v = f * Vector3.Dot(ray.direction, q);
        if ((v < 0.0f) || (u + v > 1.0f))
            return false;

        float _t = f * Vector3.Dot(e2, q);
        if (_t > RAY_COMP_EPSILON)
        {
            t = _t;
            return true;
        }
        else
            return false;

        return false;
    }
    private void _fillTriInfo(ref TetDepth tetDepth, Vector3 vOrigin, Vector3 v0, Vector3 v1, Vector3 v2)
    {
        int iID = 0;

        for (int i = 0; i <= 4; ++i)
        {
            var vP = Vector3.Lerp(v0, v1, i * 0.25f);
            var vQ = Vector3.Lerp(v0, v2, i * 0.25f);

            for (int j = 0; j <= i; ++j)
            {
                var vTarget = Vector3.Lerp(vP, vQ, j * 0.25f);

                var vDiff = vTarget - vOrigin;
                var fLongest = vDiff.magnitude;
                if (fLongest <= 0.0001f)
                {
                    _depthToIndex(ref tetDepth, iID++) = 0xff;
                }
                else {
                    var vToward = vDiff / fLongest;
                    var rToward = new Ray(vOrigin, vToward);

                    var fShortest = fLongest;
                    foreach (var filter in meshList)
                    {
                        var bAABB = filter.sharedMesh.bounds;
                        var vAABBMin = filter.transform.TransformVector(bAABB.min);
                        var vAABBMax = filter.transform.TransformVector(bAABB.max);
                        bAABB.SetMinMax(vAABBMin, vAABBMax);
                        if (!bAABB.IntersectRay(rToward))
                            continue;
            
                        for (int iSubMesh = 0; iSubMesh < filter.sharedMesh.subMeshCount; ++iSubMesh)
                        {
                            var indices = filter.sharedMesh.GetIndices(iSubMesh);
                            for (int iIndex = 0; iIndex < indices.Length; iIndex += 3)
                            {
                                var fDist = fLongest;
                                if(_rayTriIntersect(
                                    rToward,
                                    filter.transform.TransformVector(filter.sharedMesh.vertices[indices[iIndex + 0]]),
                                    filter.transform.TransformVector(filter.sharedMesh.vertices[indices[iIndex + 1]]),
                                    filter.transform.TransformVector(filter.sharedMesh.vertices[indices[iIndex + 2]]),
                                    ref fDist
                                    ))
                                {
                                    fShortest = Mathf.Min(fShortest, fDist);
                                }
                            }
                        }
                    }

                    fShortest /= fLongest;
                    fShortest *= 255.0f;
                    fShortest = Mathf.Clamp(fShortest, 0.0f, 255.0f);
                    _depthToIndex(ref tetDepth, iID++) = (byte)fShortest;
                }
            }
        }
    }
    private void _fillDepthInfo()
    {
        for (int i = 0, e = probeGen.vTetIndices.Length; i < e; ++i)
        {
            var iTet = probeGen.vTetIndices[i];
            ref var iTetDepth = ref probeGen.vTetDepthMap[i];

            { // 0 -> 1 2 3
                _fillTriInfo(ref iTetDepth._0, probeGen.vProbes[iTet._0], probeGen.vProbes[iTet._1], probeGen.vProbes[iTet._2], probeGen.vProbes[iTet._3]);
            }
            { // 1 -> 0 2 3
                _fillTriInfo(ref iTetDepth._1, probeGen.vProbes[iTet._1], probeGen.vProbes[iTet._0], probeGen.vProbes[iTet._2], probeGen.vProbes[iTet._3]);
            }
            { // 2 -> 0 1 3
                _fillTriInfo(ref iTetDepth._2, probeGen.vProbes[iTet._2], probeGen.vProbes[iTet._0], probeGen.vProbes[iTet._1], probeGen.vProbes[iTet._3]);
            }
            { // 3 -> 0 1 2
                _fillTriInfo(ref iTetDepth._3, probeGen.vProbes[iTet._3], probeGen.vProbes[iTet._0], probeGen.vProbes[iTet._1], probeGen.vProbes[iTet._2]);
            }
        }
    }

    private void _rebuildProbes()
    {
        _generateTets();
        _rebuildSHs();
        _findAllMeshes();
        _fillDepthInfo();
    }


    private void OnEnable()
    {
        probeGen = (InfProbeGen)target;

        bufTmpBuffer = new ComputeBuffer(9, sizeof(float));
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if(GUILayout.Button("Rebuild Probes"))
            _rebuildProbes();
    }

    private void OnSceneGUI()
    {
        Handles.zTest = CompareFunction.Less;

        { // render Tets
            Handles.color = Color.gray;
            foreach (var vTetIndex in probeGen.vTetIndices)
            {
                Handles.DrawLine(probeGen.vProbes[vTetIndex._0], probeGen.vProbes[vTetIndex._1]);
                Handles.DrawLine(probeGen.vProbes[vTetIndex._0], probeGen.vProbes[vTetIndex._2]);
                Handles.DrawLine(probeGen.vProbes[vTetIndex._0], probeGen.vProbes[vTetIndex._3]);

                Handles.DrawLine(probeGen.vProbes[vTetIndex._1], probeGen.vProbes[vTetIndex._2]);
                Handles.DrawLine(probeGen.vProbes[vTetIndex._1], probeGen.vProbes[vTetIndex._3]);

                Handles.DrawLine(probeGen.vProbes[vTetIndex._2], probeGen.vProbes[vTetIndex._3]);
            }
        }

        { // render Probes
            var vSize = new Vector3(0.2f, 0.2f, 0.2f);

            Handles.color = Color.magenta;
            foreach (var vProbe in probeGen.vProbes)
                Handles.DrawWireCube(vProbe, vSize);
        }

        { // render AABB
            var vDisplayExtents = Handles.DoPositionHandle(probeGen.vAABBExtents + probeGen.transform.position, Quaternion.identity);
            probeGen.vAABBExtents = vDisplayExtents - probeGen.transform.position;
            probeGen.vAABBExtents.x = Mathf.Max(0, probeGen.vAABBExtents.x);
            probeGen.vAABBExtents.y = Mathf.Max(0, probeGen.vAABBExtents.y);
            probeGen.vAABBExtents.z = Mathf.Max(0, probeGen.vAABBExtents.z);

            Handles.color = Color.yellow;
            Handles.DrawWireCube(probeGen.transform.position, probeGen.vAABBExtents * 2);
        }
    }
}