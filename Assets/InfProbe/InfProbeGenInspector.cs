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

    private int[] depthIndexTable = new int[] {
        0,
        5, 1,
        9, 6, 2,
        12, 10, 7, 3,
        14, 13, 11, 8, 4
    };

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

    private static TGVector4x3 _prb_convert(TetFloat43 vInp)
    {
        TGVector4x3 vOut = new TGVector4x3();
        vOut.vectors = new Vector3[4];
        vOut.vectors[0] = vInp._0;
        vOut.vectors[1] = vInp._1;
        vOut.vectors[2] = vInp._2;
        vOut.vectors[3] = vInp._3;
        return vOut;
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
    private static ref byte _prb_depthToIndex(ref TetDepth tetDepth, int iID)
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
            tmpCamera.backgroundColor = Color.black;
            tmpCamera.aspect = 1.0f;
            tmpCamera.nearClipPlane = 0.01f;
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

        bool[] oldRendererVisible = new bool[probeFinderRendererList.Count];
        for (int i = 0; i < probeFinderRendererList.Count; ++i)
        {
            oldRendererVisible[i] = probeFinderRendererList[i].enabled;
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

            float[] fRawRootVertices = new float[iTetCount * 4 * 3];
            TGGetTetVertices(fRawRootVertices);
            TGVector4x3[] vRootTetVertices = _vector3sToVector43s(_floatsToVector3s(fRawRootVertices));

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

            float[] fRawSubVertices = new float[iTetCount * 4 * 3];
            TGGetTetVertices(fRawSubVertices);
            probeGen.vTetVertices = _prb_vector3sToVector43s(_floatsToVector3s(fRawSubVertices));

            uint[] uRawSubIntraIndices = new uint[iTetCount * 4];
            TGGetTetIntraIndices(uRawSubIntraIndices);
            TGUint4[] vSubIntraIndices = _uintsToUint4s(uRawSubIntraIndices);

            probeGen.vTetSHIndices = new TetInt4[iTetCount];

            var iTetSHIndex = 0;
            var vTmpSHIndices = new Dictionary<Vector3, int>();

            var vTmpSHColors = new List<SHColor>();

            for (i = 0; i < iTetCount; ++i)
            {
                TetFloat43 vNewVert = new TetFloat43();
                {
                    vNewVert._0 = _prb_float43ToIndex(ref probeGen.vTetVertices[i], (int)vSubIntraIndices[i].ind[0]);
                    vNewVert._1 = _prb_float43ToIndex(ref probeGen.vTetVertices[i], (int)vSubIntraIndices[i].ind[1]);
                    vNewVert._2 = _prb_float43ToIndex(ref probeGen.vTetVertices[i], (int)vSubIntraIndices[i].ind[2]);
                    vNewVert._3 = _prb_float43ToIndex(ref probeGen.vTetVertices[i], (int)vSubIntraIndices[i].ind[3]);
                    probeGen.vTetVertices[i] = vNewVert;
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

                    if (!vTmpSHIndices.ContainsKey(vNewVert._0))
                    {
                        probeGen.vTetSHIndices[i]._0 = iTetSHIndex;
                        vTmpSHIndices[vNewVert._0] = iTetSHIndex;
                        vTmpSHColors.Add(shColor);
                        ++iTetSHIndex;
                    }
                    else
                        probeGen.vTetSHIndices[i]._0 = vTmpSHIndices[vNewVert._0];
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

                    if (!vTmpSHIndices.ContainsKey(vNewVert._1))
                    {
                        probeGen.vTetSHIndices[i]._1 = iTetSHIndex;
                        vTmpSHIndices[vNewVert._1] = iTetSHIndex;
                        vTmpSHColors.Add(shColor);
                        ++iTetSHIndex;
                    }
                    else
                        probeGen.vTetSHIndices[i]._1 = vTmpSHIndices[vNewVert._1];
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

                    if (!vTmpSHIndices.ContainsKey(vNewVert._2))
                    {
                        probeGen.vTetSHIndices[i]._2 = iTetSHIndex;
                        vTmpSHIndices[vNewVert._2] = iTetSHIndex;
                        vTmpSHColors.Add(shColor);
                        ++iTetSHIndex;
                    }
                    else
                        probeGen.vTetSHIndices[i]._2 = vTmpSHIndices[vNewVert._2];
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

                    if (!vTmpSHIndices.ContainsKey(vNewVert._3))
                    {
                        probeGen.vTetSHIndices[i]._3 = iTetSHIndex;
                        vTmpSHIndices[vNewVert._3] = iTetSHIndex;
                        vTmpSHColors.Add(shColor);
                        ++iTetSHIndex;
                    }
                    else
                        probeGen.vTetSHIndices[i]._3 = vTmpSHIndices[vNewVert._3];
                }
            }

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

            renderLines.Clear();
            foreach (var vTet in probeGen.vTetVertices)
                _pushRenderTet(_prb_convert(vTet));
        }

        DestroyImmediate(tmpObject);

        for (int i = 0; i < probeFinderRendererList.Count; ++i)
            probeFinderRendererList[i].enabled = oldRendererVisible[i];

        foreach (var probeFinder in probeFinderList)
            probeFinder.InitProbeFinder();
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

        // (0,0)      (1,0)
        //  v0 ------- v1
        //  |         /
        //  |        /
        //  |       /
        //  |      /
        //  |     /
        //  |    /
        //  |   /
        //  |  /
        //  | /
        //  v2
        // (0,1)

        for (int i = 0; i <= 4; ++i)
        {
            var vP = Vector3.Lerp(v0, v2, i * 0.25f);
            var vQ = Vector3.Lerp(v0, v1, i * 0.25f);

            for (int j = 0; j <= i; ++j)
            {
                var vTarget = Vector3.Lerp(vP, vQ, j * 0.25f);

                var vDiff = vTarget - vOrigin;
                var fLongest = vDiff.magnitude;
                if (fLongest <= 0.0001f)
                {
                    _prb_depthToIndex(ref tetDepth, iID++) = 0x00;
                }
                else {
                    var vToward = vDiff / fLongest;
                    var rToward = new Ray(vOrigin, vToward);

                    var fShortest = fLongest;
                    foreach (var filter in renderableMeshList)
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
                    _prb_depthToIndex(ref tetDepth, depthIndexTable[iID++]) = (byte)fShortest;
                }
            }
        }
    }
    private void _fillDepthInfo()
    {
        for (int i = 0, e = probeGen.vTetVertices.Length; i < e; ++i)
        {
            var vTet = probeGen.vTetVertices[i];
            ref var iTetDepth = ref probeGen.vTetDepthMap[i];

            { // 0 -> 1, 2, 3
                _fillTriInfo(ref iTetDepth._0, vTet._0, vTet._1, vTet._2, vTet._3);
            }
            { // 1 -> 0, 2, 3
                _fillTriInfo(ref iTetDepth._1, vTet._1, vTet._0, vTet._2, vTet._3);
            }
            { // 2 -> 0, 1, 3
                _fillTriInfo(ref iTetDepth._2, vTet._2, vTet._0, vTet._1, vTet._3);
            }
            { // 3 -> 0, 1, 2
                _fillTriInfo(ref iTetDepth._3, vTet._3, vTet._0, vTet._1, vTet._2);
            }
        }
    }

    private void _rebuildProbes()
    {
        _collectObjects();
        _generateTets();
        _fillDepthInfo();
    }


    private void OnEnable()
    {
        probeGen = (InfProbeGen)target;

        bufTmpBuffers[0] = new ComputeBuffer(6 * 9 * 3, sizeof(float));
        bufTmpBuffers[1] = new ComputeBuffer(6 * PROBE_RENDER_SIZE * 9 * 3, sizeof(float));
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