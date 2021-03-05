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

class TGRenderLine
{
    public Vector3 v0, v1;

    public TGRenderLine(Vector3 _v0, Vector3 _v1)
    {
        v0 = _v0;
        v1 = _v1;
    }

    public static bool operator ==(TGRenderLine lhs, TGRenderLine rhs){
        if (lhs.v0 == rhs.v0 && lhs.v1 == rhs.v1)
            return true;
        if (lhs.v0 == rhs.v1 && lhs.v1 == rhs.v0)
            return true;
        return false;
    }
    public static bool operator !=(TGRenderLine lhs, TGRenderLine rhs)
    {
        return !(lhs == rhs);
    }

    public override bool Equals(object other)
    {
        return Equals(other as TGRenderLine);
    }
    public bool Equals(TGRenderLine other)
    {
        return this == other;
    }
    public override int GetHashCode()
    {
        return v0.GetHashCode() + v1.GetHashCode();
    }
}


[CustomEditor(typeof(InfProbeGen))]
public class InfProbeGenInspector : Editor
{
    private const float RAY_COMP_EPSILON = 0.000001f;
    private const int PROBE_RENDER_SIZE = 64;


    private InfProbeGen probeGen;

    private List<InfProbeFinder> probeFinderList = new List<InfProbeFinder>();
    private List<Renderer> probeFinderRendererList = new List<Renderer>();
    private List<MeshFilter> renderableMeshList = new List<MeshFilter>();

    private Dictionary<Vector3, SHColor> cachedSHList = new Dictionary<Vector3, SHColor>();
    private HashSet<Vector3> cachedSubPositions = new HashSet<Vector3>();

    private HashSet<TGRenderLine> renderLines = new HashSet<TGRenderLine>();

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
    private static extern float TGGetTetVolume(float[] vInputTet);
    [DllImport("tetgen_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern float TGGetOctVolume(float[] vInputOct);

    [DllImport("colldetect_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void CDReserveColliderTable(uint numColl);
    [DllImport("colldetect_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern bool CDAddCollider(float[] vertices, uint numVert);
    [DllImport("colldetect_x64.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern void CDFillDepthInfo(float[] rawVertices, byte[] rawDepthMap, uint numTet);


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

    private static TetInt4[] _prb_uintsToInt4s(uint[] uAry)
    {
        TetInt4[] iOut = new TetInt4[uAry.Length >> 2];
        for (int i = 0; i < iOut.Length; ++i)
        {
            iOut[i]._0 = (int)uAry[(i << 2) + 0];
            iOut[i]._1 = (int)uAry[(i << 2) + 1];
            iOut[i]._2 = (int)uAry[(i << 2) + 2];
            iOut[i]._3 = (int)uAry[(i << 2) + 3];
        }
        return iOut;
    }
    private static TetFloat43[] _prb_vector3sToVector43s(Vector3[] vAry)
    {
        TetFloat43[] vOut = new TetFloat43[vAry.Length >> 2];
        for (int i = 0; i < vOut.Length; ++i)
        {
            vOut[i]._0 = vAry[(i << 2) + 0];
            vOut[i]._1 = vAry[(i << 2) + 1];
            vOut[i]._2 = vAry[(i << 2) + 2];
            vOut[i]._3 = vAry[(i << 2) + 3];
        }
        return vOut;
    }

    private static ref Vector3 _prb_float43ToIndex(ref TetFloat43 tetFloat43, int iID)
    {
        switch (iID)
        {
            case 0:
                return ref tetFloat43._0;
            case 1:
                return ref tetFloat43._1;
            case 2:
                return ref tetFloat43._2;
            case 3:
                return ref tetFloat43._3;
        }
        return ref tetFloat43._3;
    }

    private static Vector3 _averageVector3s(Vector3[] vAry)
    {
        Vector3 vOut = Vector3.zero;
        foreach (var v in vAry)
            vOut += v;
        vOut /= (float)vAry.Length;
        return vOut;
    }

    private static bool _compareSH(ref SHColor vLhs, ref SHColor vRhs, float fDiff)
    {
        for (int i = 0; i < 9; ++i)
        {
            var v = Mathf.Abs(vLhs.SH[i].x - vRhs.SH[i].x);
            if (v > fDiff)
                return false;

            v = Mathf.Abs(vLhs.SH[i].y - vRhs.SH[i].y);
            if (v > fDiff)
                return false;

            v = Mathf.Abs(vLhs.SH[i].z - vRhs.SH[i].z);
            if (v > fDiff)
                return false;
        }
        return true;
    }
    private static bool _compareSHs(ref SHColor[] vSHs, float fDiff)
    {
        for (int i = 0; i < vSHs.Length; ++i)
        {
            ref var vLhs = ref vSHs[i];

            for (int j = i + 1; j < vSHs.Length; ++j)
            {
                ref var vRhs = ref vSHs[j];

                if (!_compareSH(ref vLhs, ref vRhs, fDiff))
                    return false;
            }
        }
        return true;
    }


    private void _pushRenderTet(TGVector4x3 vTet)
    {
        renderLines.Add(new TGRenderLine(vTet.vectors[0], vTet.vectors[1]));
        renderLines.Add(new TGRenderLine(vTet.vectors[0], vTet.vectors[2]));
        renderLines.Add(new TGRenderLine(vTet.vectors[0], vTet.vectors[3]));

        renderLines.Add(new TGRenderLine(vTet.vectors[1], vTet.vectors[2]));
        renderLines.Add(new TGRenderLine(vTet.vectors[1], vTet.vectors[3]));

        renderLines.Add(new TGRenderLine(vTet.vectors[2], vTet.vectors[3]));
    }
    private void _pushRenderOct(TGVector6x3 vOct)
    {
        renderLines.Add(new TGRenderLine(vOct.vectors[0], vOct.vectors[1]));
        renderLines.Add(new TGRenderLine(vOct.vectors[0], vOct.vectors[2]));
        renderLines.Add(new TGRenderLine(vOct.vectors[0], vOct.vectors[3]));
        renderLines.Add(new TGRenderLine(vOct.vectors[0], vOct.vectors[4]));

        renderLines.Add(new TGRenderLine(vOct.vectors[1], vOct.vectors[2]));
        renderLines.Add(new TGRenderLine(vOct.vectors[2], vOct.vectors[3]));
        renderLines.Add(new TGRenderLine(vOct.vectors[3], vOct.vectors[4]));
        renderLines.Add(new TGRenderLine(vOct.vectors[4], vOct.vectors[1]));

        renderLines.Add(new TGRenderLine(vOct.vectors[5], vOct.vectors[1]));
        renderLines.Add(new TGRenderLine(vOct.vectors[5], vOct.vectors[2]));
        renderLines.Add(new TGRenderLine(vOct.vectors[5], vOct.vectors[3]));
        renderLines.Add(new TGRenderLine(vOct.vectors[5], vOct.vectors[4]));
    }

    private void _collectObjects()
    {
        probeFinderList.Clear();
        probeFinderRendererList.Clear();
        renderableMeshList.Clear();

        foreach (var gObj in Resources.FindObjectsOfTypeAll(typeof(GameObject)) as GameObject[])
        {
            if (!gObj.activeSelf)
                continue;
            if (gObj.hideFlags == HideFlags.HideInHierarchy)
                continue;

            if (!EditorUtility.IsPersistent(gObj.transform.root.gameObject) && !(gObj.hideFlags == HideFlags.NotEditable || gObj.hideFlags == HideFlags.HideAndDontSave))
            {
                foreach (var probeFinder in gObj.GetComponentsInChildren<InfProbeFinder>(false))
                {
                    probeFinderList.Add(probeFinder);
                }

                foreach (var renderer in gObj.GetComponentsInChildren<Renderer>(false))
                {
                    if ((renderer.GetComponents<InfProbeFinder>().Length + renderer.GetComponentsInChildren<InfProbeFinder>().Length) <= 0)
                        continue;

                    probeFinderRendererList.Add(renderer);
                }

                foreach (var mesh in gObj.GetComponentsInChildren<MeshFilter>(false))
                {
                    if ((mesh.GetComponents<MeshRenderer>().Length + mesh.GetComponentsInChildren<MeshRenderer>().Length) <= 0)
                        continue;

                    if ((mesh.GetComponents<InfProbeFinder>().Length + mesh.GetComponentsInChildren<InfProbeFinder>().Length) > 0)
                        continue;

                    renderableMeshList.Add(mesh);
                }
            }
        }
    }

    private void _rebuildSH(
        ref Camera tmpCamera,
        ref Cubemap tmpTexture,
        float fTotalWeight,
        Vector3 vPos,
        out SHColor vSH
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

            iKernel = probeGen.shdSHReductor1.FindKernel("CSMain");
            probeGen.shdSHReductor1.SetBuffer(iKernel, "BufCoeff", bufTmpBuffers[1]);
            probeGen.shdSHReductor1.SetBuffer(iKernel, "BufCoeffAcc", bufTmpBuffers[0]);
            probeGen.shdSHReductor1.Dispatch(iKernel, 1, 6, 1);

            iKernel = probeGen.shdSHReductor1.FindKernel("CSMain");
            probeGen.shdSHReductor2.SetFloat("FltTotalWeight", fTotalWeight);
            probeGen.shdSHReductor2.SetBuffer(iKernel, "BufCoeff", bufTmpBuffers[0]);
            probeGen.shdSHReductor2.SetBuffer(iKernel, "BufCoeffAcc", bufTmpBuffers[1]);
            probeGen.shdSHReductor2.Dispatch(iKernel, 1, 1, 1);

            var vRawSH = new float[9 * 3];
            bufTmpBuffers[1].GetData(vRawSH);
            vSH.SH = _floatsToVector3s(vRawSH);

            cachedSHList.Add(vPos, vSH);
        }
    }

    private void _subdivideTet(
        ref Camera tmpCamera,
        ref Cubemap tmpTexture,
        float fTotalWeight,
        TGVector4x3 vParentTet
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
            var fComp = TGGetTetVolume(_vector3sToFloats(_vector43sToVector3s(vInputTets)));
            if (fComp <= probeGen.fMinVolume)
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
                    out sHColors[j]
                    );
            }
            if (_compareSHs(ref sHColors, probeGen.fSHDiff))
            {
                foreach (var v in vParentTet.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            _subdivideTet(
                ref tmpCamera,
                ref tmpTexture,
                fTotalWeight,
                vSubTet
                );
        }
        foreach (var vSubOct in vSubOcts)
        {
            TGVector6x3[] vInputOcts = new TGVector6x3[1];
            vInputOcts[0] = vSubOct;
            var fComp = TGGetOctVolume(_vector3sToFloats(_vector63sToVector3s(vInputOcts)));
            if (fComp <= probeGen.fMinVolume)
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
                    out sHColors[j]
                    );
            }
            if (_compareSHs(ref sHColors, probeGen.fSHDiff))
            {
                foreach (var v in vParentTet.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            _subdivideOct(
                ref tmpCamera,
                ref tmpTexture,
                fTotalWeight,
                vSubOct
                );
        }
    }
    private void _subdivideOct(
        ref Camera tmpCamera,
        ref Cubemap tmpTexture,
        float fTotalWeight,
        TGVector6x3 vParentOct
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
            var fComp = TGGetTetVolume(_vector3sToFloats(_vector43sToVector3s(vInputTets)));
            if (fComp <= probeGen.fMinVolume)
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
                    out sHColors[j]
                    );
            }
            if (_compareSHs(ref sHColors, probeGen.fSHDiff))
            {
                foreach (var v in vParentOct.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            _subdivideTet(
                ref tmpCamera,
                ref tmpTexture,
                fTotalWeight,
                vSubTet
                );
        }
        foreach (var vSubOct in vSubOcts)
        {
            TGVector6x3[] vInputOcts = new TGVector6x3[1];
            vInputOcts[0] = vSubOct;
            var fComp = TGGetOctVolume(_vector3sToFloats(_vector63sToVector3s(vInputOcts)));
            if (fComp <= probeGen.fMinVolume)
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
                    out sHColors[j]
                    );
            }
            if (_compareSHs(ref sHColors, probeGen.fSHDiff))
            {
                foreach (var v in vParentOct.vectors)
                    cachedSubPositions.Add(v);
                continue;
            }

            _subdivideOct(
                ref tmpCamera,
                ref tmpTexture,
                fTotalWeight,
                vSubOct
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
        {
            tmpCamera.allowHDR = false;
            tmpCamera.allowMSAA = false;
            tmpCamera.backgroundColor = new Color(0.192157f, 0.3019608f, 0.4745098f);
            tmpCamera.aspect = 1.0f;
            tmpCamera.nearClipPlane = 0.0001f;
            tmpCamera.farClipPlane = 1000.0f;
            //tmpCamera.clearFlags = CameraClearFlags.SolidColor;
            tmpCamera.clearFlags = CameraClearFlags.Skybox;
        }

        float fTotalWeight = 0.0f;
        {
            const float fB = -1.0f + 1.0f / (float)(PROBE_RENDER_SIZE);
            const float fS = (2.0f * (1.0f - 1.0f / (float)(PROBE_RENDER_SIZE)) / ((float)(PROBE_RENDER_SIZE) - 1.0f));

            for (int y = 0; y < PROBE_RENDER_SIZE; ++y)
            {
                float v = (float)(y) * fS + fB;
                float v2 = v * v;

                for (int x = 0; x < PROBE_RENDER_SIZE; ++x)
                {
                    float u = (float)(x) * fS + fB;
                    float u2 = u * u;

                    float temp = 1.0f + u2 + v2;
                    float weight = 4.0f / (temp * Mathf.Sqrt(temp));

                    fTotalWeight += weight;
                }
            }
            fTotalWeight *= 6.0f;
            fTotalWeight = (4.0f * 3.14159f) / fTotalWeight;
        }

        var bOldRendererVisible = new bool[probeFinderRendererList.Count];
        for (int i = 0; i < probeFinderRendererList.Count; ++i)
        {
            bOldRendererVisible[i] = probeFinderRendererList[i].enabled;
            probeFinderRendererList[i].enabled = false;
        }

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

            var fRawRootVertices = new float[iTetCount * 4 * 3];
            TGGetTetVertices(fRawRootVertices);
            var vRootTetVertices = _vector3sToVector43s(_floatsToVector3s(fRawRootVertices));

            for (int i = 0; i < iTetCount; ++i)
            {
                SHColor[] shColors = new SHColor[4];
                for (int j = 0; j < 4; ++j)
                {
                    shColors[j] = new SHColor();
                    _rebuildSH(
                        ref tmpCamera,
                        ref tmpTexture,
                        fTotalWeight,
                        vRootTetVertices[i].vectors[j],
                        out shColors[j]
                        );
                }

                _subdivideTet(
                    ref tmpCamera,
                    ref tmpTexture,
                    fTotalWeight,
                    vRootTetVertices[i]
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

            var fRawSubVertices = new float[iTetCount * 4 * 3];
            TGGetTetVertices(fRawSubVertices);
            var vTetVertices = _prb_vector3sToVector43s(_floatsToVector3s(fRawSubVertices));

            var uRawSubIntraIndices = new uint[iTetCount * 4];
            TGGetTetIntraIndices(uRawSubIntraIndices);
            var vSubIntraIndices = _uintsToUint4s(uRawSubIntraIndices);

            probeGen.vTetIndices = new TetInt4[iTetCount];

            var iTetSHIndex = 0;
            var vTmpIndices = new Dictionary<Vector3, int>();

            var vTmpPositions = new List<Vector3>();
            var vTmpSHColors = new List<SHColor>();

            for (i = 0; i < iTetCount; ++i)
            {
                TetFloat43 vNewVert = new TetFloat43();
                {
                    vNewVert._0 = _prb_float43ToIndex(ref vTetVertices[i], (int)vSubIntraIndices[i].ind[0]);
                    vNewVert._1 = _prb_float43ToIndex(ref vTetVertices[i], (int)vSubIntraIndices[i].ind[1]);
                    vNewVert._2 = _prb_float43ToIndex(ref vTetVertices[i], (int)vSubIntraIndices[i].ind[2]);
                    vNewVert._3 = _prb_float43ToIndex(ref vTetVertices[i], (int)vSubIntraIndices[i].ind[3]);
                    vTetVertices[i] = vNewVert;
                }

                {
                    var shColor = new SHColor();
                    _rebuildSH(
                        ref tmpCamera,
                        ref tmpTexture,
                        fTotalWeight,
                        vNewVert._0,
                        out shColor
                        );

                    if (!vTmpIndices.ContainsKey(vNewVert._0))
                    {
                        probeGen.vTetIndices[i]._0 = iTetSHIndex;
                        vTmpIndices[vNewVert._0] = iTetSHIndex;
                        vTmpPositions.Add(vNewVert._0);
                        vTmpSHColors.Add(shColor);
                        ++iTetSHIndex;
                    }
                    else
                        probeGen.vTetIndices[i]._0 = vTmpIndices[vNewVert._0];
                }
                {
                    var shColor = new SHColor();
                    _rebuildSH(
                        ref tmpCamera,
                        ref tmpTexture,
                        fTotalWeight,
                        vNewVert._1,
                        out shColor
                        );

                    if (!vTmpIndices.ContainsKey(vNewVert._1))
                    {
                        probeGen.vTetIndices[i]._1 = iTetSHIndex;
                        vTmpIndices[vNewVert._1] = iTetSHIndex;
                        vTmpPositions.Add(vNewVert._1);
                        vTmpSHColors.Add(shColor);
                        ++iTetSHIndex;
                    }
                    else
                        probeGen.vTetIndices[i]._1 = vTmpIndices[vNewVert._1];
                }
                {
                    var shColor = new SHColor();
                    _rebuildSH(
                        ref tmpCamera,
                        ref tmpTexture,
                        fTotalWeight,
                        vNewVert._2,
                        out shColor
                        );

                    if (!vTmpIndices.ContainsKey(vNewVert._2))
                    {
                        probeGen.vTetIndices[i]._2 = iTetSHIndex;
                        vTmpIndices[vNewVert._2] = iTetSHIndex;
                        vTmpPositions.Add(vNewVert._2);
                        vTmpSHColors.Add(shColor);
                        ++iTetSHIndex;
                    }
                    else
                        probeGen.vTetIndices[i]._2 = vTmpIndices[vNewVert._2];
                }
                {
                    var shColor = new SHColor();
                    _rebuildSH(
                        ref tmpCamera,
                        ref tmpTexture,
                        fTotalWeight,
                        vNewVert._3,
                        out shColor
                        );

                    if (!vTmpIndices.ContainsKey(vNewVert._3))
                    {
                        probeGen.vTetIndices[i]._3 = iTetSHIndex;
                        vTmpIndices[vNewVert._3] = iTetSHIndex;
                        vTmpPositions.Add(vNewVert._3);
                        vTmpSHColors.Add(shColor);
                        ++iTetSHIndex;
                    }
                    else
                        probeGen.vTetIndices[i]._3 = vTmpIndices[vNewVert._3];
                }
            }

            probeGen.vTetPositions = new Vector3[vTmpPositions.Count];
            for (i = 0; i < vTmpPositions.Count; ++i)
                probeGen.vTetPositions[i] = vTmpPositions[i];

            probeGen.vSHColors = new SHColor[vTmpSHColors.Count];
            for (i = 0; i < vTmpSHColors.Count; ++i)
                probeGen.vSHColors[i] = vTmpSHColors[i];

            uint[] uRawSubAdjIndices = new uint[iTetCount * 4];
            TGGetTetAjacentIndices(uRawSubAdjIndices);
            probeGen.vTetAdjIndices = _prb_uintsToInt4s(uRawSubAdjIndices);

            float[] fRawSubBaryMatrices = new float[iTetCount * 4 * 3];
            TGGetTetBaryMatrices(fRawSubBaryMatrices);
            probeGen.vTetBaryMatrices = _prb_vector3sToVector43s(_floatsToVector3s(fRawSubBaryMatrices));

            probeGen.vTetDepthMap = new TetDepthMap[iTetCount];
        }

        DestroyImmediate(tmpObject);

        for (int i = 0; i < probeFinderRendererList.Count; ++i)
            probeFinderRendererList[i].enabled = bOldRendererVisible[i];

        _updateLineList();
    }

    private void _fillDepthInfo()
    {
        { // init colliders
            int iSubMeshCount = 0;
            foreach (var iFilter in renderableMeshList)
                iSubMeshCount += iFilter.sharedMesh.subMeshCount;
            CDReserveColliderTable((uint)iSubMeshCount);

            foreach (var iFilter in renderableMeshList)
            {
                for (int iSubMesh = 0; iSubMesh < iFilter.sharedMesh.subMeshCount; ++iSubMesh)
                {
                    var vertices = new Vector3[iFilter.sharedMesh.vertices.Length];
                    for (int iIndex = 0; iIndex < vertices.Length; ++iIndex)
                        vertices[iIndex] = iFilter.transform.TransformPoint(iFilter.sharedMesh.vertices[iIndex]);

                    var indices = iFilter.sharedMesh.GetIndices(iSubMesh);
                    var triangles = new float[indices.Length * 3];
                    for (int iIndex = 0; iIndex < indices.Length; iIndex += 3)
                    {
                        var v0 = vertices[indices[iIndex + 0]];
                        var v1 = vertices[indices[iIndex + 1]];
                        var v2 = vertices[indices[iIndex + 2]];

                        triangles[(iIndex + 0) * 3 + 0] = v0.x;
                        triangles[(iIndex + 0) * 3 + 1] = v0.y;
                        triangles[(iIndex + 0) * 3 + 2] = v0.z;

                        triangles[(iIndex + 1) * 3 + 0] = v1.x;
                        triangles[(iIndex + 1) * 3 + 1] = v1.y;
                        triangles[(iIndex + 1) * 3 + 2] = v1.z;

                        triangles[(iIndex + 2) * 3 + 0] = v2.x;
                        triangles[(iIndex + 2) * 3 + 1] = v2.y;
                        triangles[(iIndex + 2) * 3 + 2] = v2.z;
                    }

                    CDAddCollider(triangles, (uint)triangles.Length);
                }
            }
        }

        {
            var vertices = new float[probeGen.vTetIndices.Length * 4 * 3];
            var depthMap = new byte[probeGen.vTetDepthMap.Length * 4 * 15];

            for (int i = 0; i < probeGen.vTetIndices.Length; ++i)
            {
                ref var vertex = ref probeGen.vTetIndices[i];

                vertices[(i * 4 * 3) + (0 * 3) + 0] = probeGen.vTetPositions[vertex._0].x;
                vertices[(i * 4 * 3) + (0 * 3) + 1] = probeGen.vTetPositions[vertex._0].y;
                vertices[(i * 4 * 3) + (0 * 3) + 2] = probeGen.vTetPositions[vertex._0].z;

                vertices[(i * 4 * 3) + (1 * 3) + 0] = probeGen.vTetPositions[vertex._1].x;
                vertices[(i * 4 * 3) + (1 * 3) + 1] = probeGen.vTetPositions[vertex._1].y;
                vertices[(i * 4 * 3) + (1 * 3) + 2] = probeGen.vTetPositions[vertex._1].z;

                vertices[(i * 4 * 3) + (2 * 3) + 0] = probeGen.vTetPositions[vertex._2].x;
                vertices[(i * 4 * 3) + (2 * 3) + 1] = probeGen.vTetPositions[vertex._2].y;
                vertices[(i * 4 * 3) + (2 * 3) + 2] = probeGen.vTetPositions[vertex._2].z;

                vertices[(i * 4 * 3) + (3 * 3) + 0] = probeGen.vTetPositions[vertex._3].x;
                vertices[(i * 4 * 3) + (3 * 3) + 1] = probeGen.vTetPositions[vertex._3].y;
                vertices[(i * 4 * 3) + (3 * 3) + 2] = probeGen.vTetPositions[vertex._3].z;
            }

            CDFillDepthInfo(vertices, depthMap, (uint)probeGen.vTetIndices.Length);

            for (int i = 0; i < probeGen.vTetDepthMap.Length; ++i)
            {
                ref var depth = ref probeGen.vTetDepthMap[i];

                depth._0._00 = depthMap[(i * 4 * 15) + (0 * 15) + 0];
                depth._0._01 = depthMap[(i * 4 * 15) + (0 * 15) + 1];
                depth._0._02 = depthMap[(i * 4 * 15) + (0 * 15) + 2];
                depth._0._03 = depthMap[(i * 4 * 15) + (0 * 15) + 3];
                depth._0._04 = depthMap[(i * 4 * 15) + (0 * 15) + 4];
                depth._0._05 = depthMap[(i * 4 * 15) + (0 * 15) + 5];
                depth._0._06 = depthMap[(i * 4 * 15) + (0 * 15) + 6];
                depth._0._07 = depthMap[(i * 4 * 15) + (0 * 15) + 7];
                depth._0._08 = depthMap[(i * 4 * 15) + (0 * 15) + 8];
                depth._0._09 = depthMap[(i * 4 * 15) + (0 * 15) + 9];
                depth._0._10 = depthMap[(i * 4 * 15) + (0 * 15) + 10];
                depth._0._11 = depthMap[(i * 4 * 15) + (0 * 15) + 11];
                depth._0._12 = depthMap[(i * 4 * 15) + (0 * 15) + 12];
                depth._0._13 = depthMap[(i * 4 * 15) + (0 * 15) + 13];
                depth._0._14 = depthMap[(i * 4 * 15) + (0 * 15) + 14];

                depth._1._00 = depthMap[(i * 4 * 15) + (1 * 15) + 0];
                depth._1._01 = depthMap[(i * 4 * 15) + (1 * 15) + 1];
                depth._1._02 = depthMap[(i * 4 * 15) + (1 * 15) + 2];
                depth._1._03 = depthMap[(i * 4 * 15) + (1 * 15) + 3];
                depth._1._04 = depthMap[(i * 4 * 15) + (1 * 15) + 4];
                depth._1._05 = depthMap[(i * 4 * 15) + (1 * 15) + 5];
                depth._1._06 = depthMap[(i * 4 * 15) + (1 * 15) + 6];
                depth._1._07 = depthMap[(i * 4 * 15) + (1 * 15) + 7];
                depth._1._08 = depthMap[(i * 4 * 15) + (1 * 15) + 8];
                depth._1._09 = depthMap[(i * 4 * 15) + (1 * 15) + 9];
                depth._1._10 = depthMap[(i * 4 * 15) + (1 * 15) + 10];
                depth._1._11 = depthMap[(i * 4 * 15) + (1 * 15) + 11];
                depth._1._12 = depthMap[(i * 4 * 15) + (1 * 15) + 12];
                depth._1._13 = depthMap[(i * 4 * 15) + (1 * 15) + 13];
                depth._1._14 = depthMap[(i * 4 * 15) + (1 * 15) + 14];

                depth._2._00 = depthMap[(i * 4 * 15) + (2 * 15) + 0];
                depth._2._01 = depthMap[(i * 4 * 15) + (2 * 15) + 1];
                depth._2._02 = depthMap[(i * 4 * 15) + (2 * 15) + 2];
                depth._2._03 = depthMap[(i * 4 * 15) + (2 * 15) + 3];
                depth._2._04 = depthMap[(i * 4 * 15) + (2 * 15) + 4];
                depth._2._05 = depthMap[(i * 4 * 15) + (2 * 15) + 5];
                depth._2._06 = depthMap[(i * 4 * 15) + (2 * 15) + 6];
                depth._2._07 = depthMap[(i * 4 * 15) + (2 * 15) + 7];
                depth._2._08 = depthMap[(i * 4 * 15) + (2 * 15) + 8];
                depth._2._09 = depthMap[(i * 4 * 15) + (2 * 15) + 9];
                depth._2._10 = depthMap[(i * 4 * 15) + (2 * 15) + 10];
                depth._2._11 = depthMap[(i * 4 * 15) + (2 * 15) + 11];
                depth._2._12 = depthMap[(i * 4 * 15) + (2 * 15) + 12];
                depth._2._13 = depthMap[(i * 4 * 15) + (2 * 15) + 13];
                depth._2._14 = depthMap[(i * 4 * 15) + (2 * 15) + 14];

                depth._3._00 = depthMap[(i * 4 * 15) + (3 * 15) + 0];
                depth._3._01 = depthMap[(i * 4 * 15) + (3 * 15) + 1];
                depth._3._02 = depthMap[(i * 4 * 15) + (3 * 15) + 2];
                depth._3._03 = depthMap[(i * 4 * 15) + (3 * 15) + 3];
                depth._3._04 = depthMap[(i * 4 * 15) + (3 * 15) + 4];
                depth._3._05 = depthMap[(i * 4 * 15) + (3 * 15) + 5];
                depth._3._06 = depthMap[(i * 4 * 15) + (3 * 15) + 6];
                depth._3._07 = depthMap[(i * 4 * 15) + (3 * 15) + 7];
                depth._3._08 = depthMap[(i * 4 * 15) + (3 * 15) + 8];
                depth._3._09 = depthMap[(i * 4 * 15) + (3 * 15) + 9];
                depth._3._10 = depthMap[(i * 4 * 15) + (3 * 15) + 10];
                depth._3._11 = depthMap[(i * 4 * 15) + (3 * 15) + 11];
                depth._3._12 = depthMap[(i * 4 * 15) + (3 * 15) + 12];
                depth._3._13 = depthMap[(i * 4 * 15) + (3 * 15) + 13];
                depth._3._14 = depthMap[(i * 4 * 15) + (3 * 15) + 14];
            }
        }
    }

    private void _rebuildProbes()
    {
        _collectObjects();
        _generateTets();
        _fillDepthInfo();

        foreach (var probeFinder in probeFinderList)
            probeFinder.InitProbeFinder();
    }

    private void _updateLineList()
    {
        renderLines.Clear();

        if (probeGen.vTetIndices == null)
            return;

        foreach (var iIndices in probeGen.vTetIndices)
        {
            var vVal = new TGVector4x3();
            vVal.vectors = new Vector3[4];

            vVal.vectors[0] = probeGen.vTetPositions[iIndices._0];
            vVal.vectors[1] = probeGen.vTetPositions[iIndices._1];
            vVal.vectors[2] = probeGen.vTetPositions[iIndices._2];
            vVal.vectors[3] = probeGen.vTetPositions[iIndices._3];

            _pushRenderTet(vVal);
        }
    }


    private void OnEnable()
    {
        probeGen = (InfProbeGen)target;

        bufTmpBuffers[0] = new ComputeBuffer(6 * 9 * 3, sizeof(float));
        bufTmpBuffers[1] = new ComputeBuffer(6 * PROBE_RENDER_SIZE * 9 * 3, sizeof(float));

        _updateLineList();
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

        if (renderLines.Count > 0)
        { // render Tets
            Handles.color = Color.gray;
            foreach (var vLine in renderLines)
                Handles.DrawLine(vLine.v0, vLine.v1);
        }

        if(probeGen.vTetPositions != null && probeGen.vTetPositions.Length > 0)
        { // render Probes
            var vSize = new Vector3(0.2f, 0.2f, 0.2f);

            Handles.color = Color.magenta;
            foreach (var vProbe in probeGen.vTetPositions)
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

