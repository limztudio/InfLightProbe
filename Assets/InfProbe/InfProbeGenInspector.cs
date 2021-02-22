using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;


struct TGVector4x3
{
    public Vector3[] vectors;
};
struct TGVector6x3
{
    public Vector3[] vectors;
};
struct TGUint4
{
    public uint[] ind;
};


[CustomEditor(typeof(InfProbeGen))]
public class InfProbeGenInspector : Editor
{
    private const float RAY_COMP_EPSILON = 0.000001f;
    private const int PROBE_RENDER_SIZE = 64;


    private InfProbeGen probeGen;
    private List<MeshFilter> meshList = new List<MeshFilter>();

    private Dictionary<Vector3, SHColor> cachedSHList = new Dictionary<Vector3, SHColor>();
    private HashSet<Vector3> cachedSubPositions = new HashSet<Vector3>();

    private TGVector4x3[] renderTets;

    private ComputeBuffer[] bufTmpBuffers = new ComputeBuffer[2];


    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool TGBuildTets(float[] vInputTets, uint numVert);
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint TGGetTetCount();
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TGGetTetVertices(float[] vOut);
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TGGetTetIntraIndices(uint[] vOut);
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TGGetTetAjacentIndices(uint[] vOut);
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TGGetTetBaryMatrices(float[] vOut);
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TGSubdivideTet(float[] vInputTet, float[] vOutTets, float[] vOutOct);
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void TGSubdivideOct(float[] vInputOct, float[] vOutTets, float[] vOutOcts);
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern float TGGetAverageTetSpace(float[] vInputTet);
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern float TGGetAverageOctSpace(float[] vInputOct);


    private static float[] _vector3sToFloats(Vector3[] vAry)
    {
        float[] fOut = new float[vAry.Length * 3];
        for(int i = 0; i < vAry.Length; ++i)
        {
            fOut[(i * 3) + 0] = vAry[i].x;
            fOut[(i * 3) + 1] = vAry[i].y;
            fOut[(i * 3) + 2] = vAry[i].z;
        }
        return fOut;
    }
    private static Vector3[] _floatsToVector3s(float[] fAry)
    {
        Vector3[] vOut = new Vector3[fAry.Length / 3];
        for (int i = 0; i < vOut.Length; ++i)
        {
            vOut[i] = new Vector3(
                fAry[(i * 3) + 0],
                fAry[(i * 3) + 1],
                fAry[(i * 3) + 2]
                );
        }
        return vOut;
    }

    private static Vector3[] _vector43sToVector3s(TGVector4x3[] vAry)
    {
        Vector3[] vOut = new Vector3[vAry.Length << 2];
        for (int i = 0; i < vAry.Length; ++i)
        {
            vOut[(i << 2) + 0] = vAry[i].vectors[0];
            vOut[(i << 2) + 1] = vAry[i].vectors[1];
            vOut[(i << 2) + 2] = vAry[i].vectors[2];
            vOut[(i << 2) + 3] = vAry[i].vectors[3];
        }
        return vOut;
    }
    private static TGVector4x3[] _vector3sToVector43s(Vector3[] vAry)
    {
        TGVector4x3[] vOut = new TGVector4x3[vAry.Length >> 2];
        for (int i = 0; i < vOut.Length; ++i)
        {
            vOut[i].vectors = new Vector3[4];
            vOut[i].vectors[0] = vAry[(i << 2) + 0];
            vOut[i].vectors[1] = vAry[(i << 2) + 1];
            vOut[i].vectors[2] = vAry[(i << 2) + 2];
            vOut[i].vectors[3] = vAry[(i << 2) + 3];
        }
        return vOut;
    }
    private static Vector3[] _vector63sToVector3s(TGVector6x3[] vAry)
    {
        Vector3[] vOut = new Vector3[vAry.Length * 6];
        for (int i = 0; i < vAry.Length; ++i)
        {
            vOut[(i * 6) + 0] = vAry[i].vectors[0];
            vOut[(i * 6) + 1] = vAry[i].vectors[1];
            vOut[(i * 6) + 2] = vAry[i].vectors[2];
            vOut[(i * 6) + 3] = vAry[i].vectors[3];
            vOut[(i * 6) + 4] = vAry[i].vectors[4];
            vOut[(i * 6) + 5] = vAry[i].vectors[5];
        }
        return vOut;
    }
    private static TGVector6x3[] _vector3sToVector63s(Vector3[] vAry)
    {
        TGVector6x3[] vOut = new TGVector6x3[vAry.Length / 6];
        for (int i = 0; i < vOut.Length; ++i)
        {
            vOut[i].vectors = new Vector3[6];
            vOut[i].vectors[0] = vAry[(i / 6) + 0];
            vOut[i].vectors[1] = vAry[(i / 6) + 1];
            vOut[i].vectors[2] = vAry[(i / 6) + 2];
            vOut[i].vectors[3] = vAry[(i / 6) + 3];
            vOut[i].vectors[4] = vAry[(i / 6) + 4];
            vOut[i].vectors[5] = vAry[(i / 6) + 5];
        }
        return vOut;
    }

    private static TGUint4[] _uintsToUint4s(uint[] uAry)
    {
        TGUint4[] uOut = new TGUint4[uAry.Length >> 2];
        for (int i = 0; i < uOut.Length; ++i)
        {
            uOut[i].ind = new uint[4];
            uOut[i].ind[0] = uAry[(i << 2) + 0];
            uOut[i].ind[1] = uAry[(i << 2) + 1];
            uOut[i].ind[2] = uAry[(i << 2) + 2];
            uOut[i].ind[3] = uAry[(i << 2) + 3];
        }
        return uOut;
    }

    private static Vector3 _averageVector3s(Vector3[] vAry)
    {
        Vector3 vOut = Vector3.zero;
        foreach (var v in vAry)
            vOut += v;
        vOut /= (float)vAry.Length;
        return vOut;
    }


    private void _findAllMeshes()
    {
        meshList.Clear();

        foreach (var gObj in Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[])
        {
            if (!gObj.activeSelf)
                continue;

            if (!EditorUtility.IsPersistent(gObj.transform.root.gameObject) && !(gObj.hideFlags == HideFlags.NotEditable || gObj.hideFlags == HideFlags.HideAndDontSave))
            {
                foreach (var mesh in gObj.GetComponentsInChildren<MeshFilter>(false))
                {
                    if ((mesh.GetComponents<MeshRenderer>().Length + mesh.GetComponentsInChildren<MeshRenderer>().Length) <= 0)
                        continue;

                    meshList.Add(mesh);
                }
            }
        }
    }

    private static bool _compareSH(ref SHColor vLHS, ref SHColor vRHS, float fDIFF)
    {
        for(int i = 0; i < 9; ++i)
        {
            var v = Mathf.Abs(vLHS.SH[i] - vRHS.SH[i]);
            if (v > fDIFF)
                return false;
        }
        return true;
    }
    private static SHColor _averageSH(ref SHColor[] vSHs)
    {
        SHColor vSH = new SHColor();
        vSH.SH = new float[9];
        for (int i = 0; i < 9; ++i)
            vSH.SH[i] = 0.0f;
        foreach (var j in vSHs)
            for (int i = 0; i < 9; ++i)
                vSH.SH[i] += j.SH[i];
        for (int i = 0; i < 9; ++i)
            vSH.SH[i] /= (float)vSHs.Length;
        return vSH;
    }
    private void _rebuildSH(
        ref Camera tmpCamera,
        ref Cubemap tmpTexture,
        float fTotalWeight,
        Vector3 vPos,
        ref SHColor vSH
        )
    {
        if (cachedSHList.ContainsKey(vPos))
        {
            vSH = cachedSHList[vPos];
        }
        else
        {
            tmpCamera.transform.position = vPos;
            tmpCamera.transform.rotation = Quaternion.identity;
            tmpCamera.RenderToCubemap(tmpTexture);

            var iKernel = probeGen.shdSHIntegrator.FindKernel("CSMain");
            probeGen.shdSHIntegrator.SetTexture(iKernel, "TexEnv", tmpTexture);
            probeGen.shdSHIntegrator.SetBuffer(iKernel, "BufCoeff", bufTmpBuffers[1]);
            probeGen.shdSHIntegrator.Dispatch(iKernel, 1, PROBE_RENDER_SIZE, 6);

            iKernel = probeGen.shdSHReductor.FindKernel("CSMain");
            probeGen.shdSHReductor.SetFloat("FltTotalWeight", fTotalWeight);
            probeGen.shdSHReductor.SetBuffer(iKernel, "BufCoeff", bufTmpBuffers[1]);
            probeGen.shdSHReductor.SetBuffer(iKernel, "BufCoeffAcc", bufTmpBuffers[0]);
            probeGen.shdSHReductor.Dispatch(iKernel, 1, 1, 1);

            vSH.SH = new float[9];
            bufTmpBuffers[0].GetData(vSH.SH);

            cachedSHList.Add(vPos, vSH);
        }
    }

    private void _subdivideTet(
        ref Camera tmpCamera,
        ref Cubemap tmpTexture,
        float fTotalWeight,
        TGVector4x3 vParentTet,
        SHColor shParentAverage
        )
    {
        TGVector4x3[] vSubTets;
        TGVector6x3[] vSubOcts;
        {
            TGVector4x3[] vInputTets = new TGVector4x3[1];
            vInputTets[0] = vParentTet;

            float[] fSubTetVertices = new float[4 * 4 * 3];
            float[] fSubOctVertices = new float[1 * 6 * 3];

            TGSubdivideTet(
                _vector3sToFloats(_vector43sToVector3s(vInputTets)),
                fSubTetVertices,
                fSubOctVertices
                );

            vSubTets = _vector3sToVector43s(_floatsToVector3s(fSubTetVertices));
            vSubOcts = _vector3sToVector63s(_floatsToVector3s(fSubOctVertices));
        }

        foreach (var vSubTet in vSubTets)
        {
            TGVector4x3[] vInputTets = new TGVector4x3[1];
            vInputTets[0] = vSubTet;
            var fComp = TGGetAverageTetSpace(_vector3sToFloats(_vector43sToVector3s(vInputTets)));
            if (fComp <= probeGen.fMinDist)
            {
                foreach (var v in vParentTet.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            SHColor[] sHColors = new SHColor[4];
            for (int j = 0; j < 4; ++j)
            {
                sHColors[j] = new SHColor();
                _rebuildSH(
                    ref tmpCamera,
                    ref tmpTexture,
                    fTotalWeight,
                    vSubTet.vectors[j],
                    ref sHColors[j]
                    );
            }
            var shAverage = _averageSH(ref sHColors);
            if (_compareSH(ref shAverage, ref shParentAverage, probeGen.fSHDiff))
            {
                foreach (var v in vParentTet.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            _subdivideTet(
                ref tmpCamera,
                ref tmpTexture,
                fTotalWeight,
                vSubTet,
                shAverage
                );
        }
        foreach (var vSubOct in vSubOcts)
        {
            TGVector6x3[] vInputOcts = new TGVector6x3[1];
            vInputOcts[0] = vSubOct;
            var fComp = TGGetAverageOctSpace(_vector3sToFloats(_vector63sToVector3s(vInputOcts)));
            if (fComp <= probeGen.fMinDist)
            {
                foreach (var v in vParentTet.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            SHColor[] sHColors = new SHColor[6];
            for (int j = 0; j < 6; ++j)
            {
                sHColors[j] = new SHColor();
                _rebuildSH(
                    ref tmpCamera,
                    ref tmpTexture,
                    fTotalWeight,
                    vSubOct.vectors[j],
                    ref sHColors[j]
                    );
            }
            var shAverage = _averageSH(ref sHColors);
            if (_compareSH(ref shAverage, ref shParentAverage, probeGen.fSHDiff))
            {
                foreach (var v in vParentTet.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            _subdivideOct(
                ref tmpCamera,
                ref tmpTexture,
                fTotalWeight,
                vSubOct,
                shAverage
                );
        }
    }
    private void _subdivideOct(
        ref Camera tmpCamera,
        ref Cubemap tmpTexture,
        float fTotalWeight,
        TGVector6x3 vParentOct,
        SHColor shParentAverage
        )
    {
        TGVector4x3[] vSubTets;
        TGVector6x3[] vSubOcts;
        {
            TGVector6x3[] vInputOcts = new TGVector6x3[1];
            vInputOcts[0] = vParentOct;

            float[] fSubTetVertices = new float[8 * 4 * 3];
            float[] fSubOctVertices = new float[6 * 6 * 3];

            TGSubdivideOct(
                _vector3sToFloats(_vector63sToVector3s(vInputOcts)),
                fSubTetVertices,
                fSubOctVertices
                );

            vSubTets = _vector3sToVector43s(_floatsToVector3s(fSubTetVertices));
            vSubOcts = _vector3sToVector63s(_floatsToVector3s(fSubOctVertices));
        }

        foreach (var vSubTet in vSubTets)
        {
            TGVector4x3[] vInputTets = new TGVector4x3[1];
            vInputTets[0] = vSubTet;
            var fComp = TGGetAverageTetSpace(_vector3sToFloats(_vector43sToVector3s(vInputTets)));
            if (fComp <= probeGen.fMinDist)
            {
                foreach (var v in vParentOct.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            SHColor[] sHColors = new SHColor[4];
            for (int j = 0; j < 4; ++j)
            {
                sHColors[j] = new SHColor();
                _rebuildSH(
                    ref tmpCamera,
                    ref tmpTexture,
                    fTotalWeight,
                    vSubTet.vectors[j],
                    ref sHColors[j]
                    );
            }
            var shAverage = _averageSH(ref sHColors);
            if (_compareSH(ref shAverage, ref shParentAverage, probeGen.fSHDiff))
            {
                foreach (var v in vParentOct.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            _subdivideTet(
                ref tmpCamera,
                ref tmpTexture,
                fTotalWeight,
                vSubTet,
                shAverage
                );
        }
        foreach (var vSubOct in vSubOcts)
        {
            TGVector6x3[] vInputOcts = new TGVector6x3[1];
            vInputOcts[0] = vSubOct;
            var fComp = TGGetAverageOctSpace(_vector3sToFloats(_vector63sToVector3s(vInputOcts)));
            if (fComp <= probeGen.fMinDist)
            {
                foreach (var v in vParentOct.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            SHColor[] sHColors = new SHColor[6];
            for (int j = 0; j < 6; ++j)
            {
                sHColors[j] = new SHColor();
                _rebuildSH(
                    ref tmpCamera,
                    ref tmpTexture,
                    fTotalWeight,
                    vSubOct.vectors[j],
                    ref sHColors[j]
                    );
            }
            var shAverage = _averageSH(ref sHColors);
            if (_compareSH(ref shAverage, ref shParentAverage, probeGen.fSHDiff))
            {
                foreach (var v in vParentOct.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            _subdivideOct(
                ref tmpCamera,
                ref tmpTexture,
                fTotalWeight,
                vSubOct,
                shAverage
                );
        }
    }
    private void _generateTets()
    {
        cachedSHList.Clear();
        cachedSubPositions.Clear();

        var tmpObject = new GameObject("ProbeCamera");
        var tmpCamera = tmpObject.AddComponent<Camera>();
        var tmpTexture = new Cubemap(PROBE_RENDER_SIZE, TextureFormat.RGB24, false);

        tmpCamera.allowHDR = false;
        tmpCamera.allowMSAA = false;
        tmpCamera.backgroundColor = Color.black;
        tmpCamera.aspect = 1.0f;
        //tmpCamera.clearFlags = CameraClearFlags.SolidColor;
        tmpCamera.clearFlags = CameraClearFlags.Skybox;

        float fTotalWeight = 0.0f;
        for (int y = 0; y < PROBE_RENDER_SIZE; ++y)
        {
            for (int x = 0; x < PROBE_RENDER_SIZE; ++x)
            {
                float u = ((float)x / (float)PROBE_RENDER_SIZE) * 2.0f - 1.0f;
                float v = ((float)y / (float)PROBE_RENDER_SIZE) * 2.0f - 1.0f;

                float temp = 1.0f + u * u + v * v;
                float weight = 4.0f / (Mathf.Sqrt(temp) * temp);

                fTotalWeight += weight;
            }
        }
        fTotalWeight *= 6.0f;
        fTotalWeight = (4.0f * 3.14159f) / fTotalWeight;

        {
            Vector3[] vRootVertices = new Vector3[8];
            {
                var vAABBMin = probeGen.transform.position - probeGen.vAABBExtents;
                var vAABBMax = probeGen.transform.position + probeGen.vAABBExtents;

                vRootVertices[0] = new Vector3(vAABBMin.x, vAABBMin.y, vAABBMin.z);
                vRootVertices[1] = new Vector3(vAABBMax.x, vAABBMin.y, vAABBMin.z);
                vRootVertices[2] = new Vector3(vAABBMin.x, vAABBMax.y, vAABBMin.z);
                vRootVertices[3] = new Vector3(vAABBMin.x, vAABBMin.y, vAABBMax.z);
                vRootVertices[4] = new Vector3(vAABBMin.x, vAABBMax.y, vAABBMax.z);
                vRootVertices[5] = new Vector3(vAABBMax.x, vAABBMin.y, vAABBMax.z);
                vRootVertices[6] = new Vector3(vAABBMax.x, vAABBMax.y, vAABBMin.z);
                vRootVertices[7] = new Vector3(vAABBMax.x, vAABBMax.y, vAABBMax.z);
            }
            TGBuildTets(_vector3sToFloats(vRootVertices), (uint)(vRootVertices.Length * 3));

            var iTetCount = (int)TGGetTetCount();

            float[] fRawRootVertices = new float[iTetCount * 4 * 3];
            TGGetTetVertices(fRawRootVertices);
            TGVector4x3[] vRootTetVertices = _vector3sToVector43s(_floatsToVector3s(fRawRootVertices));

            for (int i = 0; i < iTetCount; ++i)
            {
                SHColor[] sHColors = new SHColor[4];
                for (int j = 0; j < 4; ++j)
                {
                    sHColors[j] = new SHColor();
                    _rebuildSH(
                        ref tmpCamera,
                        ref tmpTexture,
                        fTotalWeight,
                        vRootTetVertices[i].vectors[j],
                        ref sHColors[j]
                        );
                }
                var shAverage = _averageSH(ref sHColors);

                _subdivideTet(
                    ref tmpCamera,
                    ref tmpTexture,
                    fTotalWeight,
                    vRootTetVertices[i],
                    shAverage
                    );
            }
        }

        {
            Vector3[] vSubVertices = new Vector3[cachedSubPositions.Count];
            int i = 0;
            foreach (var v in cachedSubPositions)
                vSubVertices[i++] = v;

            TGBuildTets(_vector3sToFloats(vSubVertices), (uint)(vSubVertices.Length * 3));

            var iTetCount = (int)TGGetTetCount();

            float[] fRawSubVertices = new float[iTetCount * 4 * 3];
            TGGetTetVertices(fRawSubVertices);
            renderTets = _vector3sToVector43s(_floatsToVector3s(fRawSubVertices));
        }

        DestroyImmediate(tmpObject);
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
        //_findAllMeshes();
        //_fillDepthInfo();
    }


    private void OnEnable()
    {
        probeGen = (InfProbeGen)target;

        bufTmpBuffers[0] = new ComputeBuffer(9, sizeof(float) * 3);
        bufTmpBuffers[1] = new ComputeBuffer(PROBE_RENDER_SIZE * 9, sizeof(float) * 3);
    }
    private void OnDisable()
    {
        bufTmpBuffers[0].Release();
        bufTmpBuffers[1].Release();
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

        //if (renderTets != null)
        //{ // render Tets
        //    Handles.color = Color.gray;
        //    foreach (var vTet in renderTets)
        //    {
        //        Handles.DrawLine(vTet.vectors[0], vTet.vectors[1]);
        //        Handles.DrawLine(vTet.vectors[0], vTet.vectors[2]);
        //        Handles.DrawLine(vTet.vectors[0], vTet.vectors[3]);

        //        Handles.DrawLine(vTet.vectors[1], vTet.vectors[2]);
        //        Handles.DrawLine(vTet.vectors[1], vTet.vectors[3]);

        //        Handles.DrawLine(vTet.vectors[2], vTet.vectors[3]);
        //    }
        //}

        { // render Probes
            var vSize = new Vector3(0.2f, 0.2f, 0.2f);

            Handles.color = Color.magenta;
            foreach (var vProbe in cachedSubPositions)
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