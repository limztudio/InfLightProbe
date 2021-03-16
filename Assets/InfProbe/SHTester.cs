using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SHTester : MonoBehaviour
{
    private const int PROBE_RENDER_SIZE = 64;
    private const int PROBE_COFF_COUNT = 9;
    private const int RENDER_FACE_COUNT = 6;


    private new Renderer renderer = null;
    private MaterialPropertyBlock propBlock = null;

    public ComputeShader shdSHIntegrator;
    public ComputeShader shdSHReductor1;
    public ComputeShader shdSHReductor2;

    public RenderTexture shTexture;
    public SHShaderColor shColor;

    private float fTotalWeight;
    private ComputeBuffer[] bufTmpBuffers = new ComputeBuffer[2];


    private void RenderSH(
        ref Camera tmpCamera,
        ref RenderTexture tmpTexture,
        out Vector3[] vSH
        )
    {
        tmpCamera.transform.position = transform.position;
        tmpCamera.transform.rotation = Quaternion.identity;
        tmpCamera.RenderToCubemap(tmpTexture);

        var iKernel = shdSHIntegrator.FindKernel("CSMain");
        shdSHIntegrator.SetTexture(iKernel, "TexEnv", tmpTexture);
        shdSHIntegrator.SetBuffer(iKernel, "BufCoeff", bufTmpBuffers[1]);
        shdSHIntegrator.Dispatch(iKernel, 1, PROBE_RENDER_SIZE, RENDER_FACE_COUNT);

        iKernel = shdSHReductor1.FindKernel("CSMain");
        shdSHReductor1.SetBuffer(iKernel, "BufCoeff", bufTmpBuffers[1]);
        shdSHReductor1.SetBuffer(iKernel, "BufCoeffAcc", bufTmpBuffers[0]);
        shdSHReductor1.Dispatch(iKernel, 1, RENDER_FACE_COUNT, 1);

        iKernel = shdSHReductor2.FindKernel("CSMain");
        shdSHReductor2.SetFloat("FltTotalWeight", fTotalWeight);
        shdSHReductor2.SetBuffer(iKernel, "BufCoeff", bufTmpBuffers[0]);
        shdSHReductor2.SetBuffer(iKernel, "BufCoeffAcc", bufTmpBuffers[1]);
        shdSHReductor2.Dispatch(iKernel, 1, 1, 1);

        var vRawSH = new float[PROBE_COFF_COUNT * 3];
        bufTmpBuffers[1].GetData(vRawSH);

        vSH = new Vector3[PROBE_COFF_COUNT];
        for(int i = 0; i < PROBE_COFF_COUNT; ++i)
        {
            vSH[i].x = vRawSH[(i * 3) + 0];
            vSH[i].y = vRawSH[(i * 3) + 1];
            vSH[i].z = vRawSH[(i * 3) + 2];
        }
    }
    private void UpdateSH(Vector3[] vSH)
    {
        shColor.vBand1Base0R_vBand1Base1R_vBand1Base2R_vBand0Base0R = new Vector4(
            vSH[3].x * (2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f),
            vSH[1].x * (2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f),
            vSH[2].x * (2.094395102393195492f) * (0.488602511902919920f) * (0.318309886183790672f),
            vSH[0].x * (3.141592653589793238f) * (0.282094791773878140f) * (0.318309886183790672f) + vSH[6].x * (0.785398163397448309f) * (-0.315391565252520050f) * (0.318309886183790672f)
            );
        shColor.vBand1Base0G_vBand1Base1G_vBand1Base2G_vBand0Base0G = new Vector4(
            vSH[3].y * (2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f),
            vSH[1].y * (2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f),
            vSH[2].y * (2.094395102393195492f) * (0.488602511902919920f) * (0.318309886183790672f),
            vSH[0].y * (3.141592653589793238f) * (0.282094791773878140f) * (0.318309886183790672f) + vSH[6].y * (0.785398163397448309f) * (-0.315391565252520050f) * (0.318309886183790672f)
            );
        shColor.vBand1Base0B_vBand1Base1B_vBand1Base2B_vBand0Base0B = new Vector4(
            vSH[3].z * (2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f),
            vSH[1].z * (2.094395102393195492f) * (-0.488602511902919920f) * (0.318309886183790672f),
            vSH[2].z * (2.094395102393195492f) * (0.488602511902919920f) * (0.318309886183790672f),
            vSH[0].z * (3.141592653589793238f) * (0.282094791773878140f) * (0.318309886183790672f) + vSH[6].z * (0.785398163397448309f) * (-0.315391565252520050f) * (0.318309886183790672f)
            );
        shColor.vBand2Base0R_vBand2Base1R_vBand2Base2R_vBand2Base3R = new Vector4(
            vSH[4].x * (0.785398163397448309f) * (0.546274215296039590f * 2.0f) * (0.318309886183790672f),
            vSH[5].x * (0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f),
            vSH[6].x * (0.785398163397448309f) * (0.946174695757560080f) * (0.318309886183790672f),
            vSH[7].x * (0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f)
            );
        shColor.vBand2Base0G_vBand2Base1G_vBand2Base2G_vBand2Base3G = new Vector4(
            vSH[4].y * (0.785398163397448309f) * (0.546274215296039590f * 2.0f) * (0.318309886183790672f),
            vSH[5].y * (0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f),
            vSH[6].y * (0.785398163397448309f) * (0.946174695757560080f) * (0.318309886183790672f),
            vSH[7].y * (0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f)
            );
        shColor.vBand2Base0B_vBand2Base1B_vBand2Base2B_vBand2Base3B = new Vector4(
            vSH[4].z * (0.785398163397448309f) * (0.546274215296039590f * 2.0f) * (0.318309886183790672f),
            vSH[5].z * (0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f),
            vSH[6].z * (0.785398163397448309f) * (0.946174695757560080f) * (0.318309886183790672f),
            vSH[7].z * (0.785398163397448309f) * (-1.092548430592079200f) * (0.318309886183790672f)
            );
        shColor.vBand2Base4R_vBand2Base4G_vBand2Base4B =
            vSH[8] * (0.785398163397448309f) * (0.546274215296039590f) * (0.318309886183790672f)
            ;
    }

    public void InitUnit()
    {
        fTotalWeight = 0.0f;
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
            fTotalWeight = (4.0f * 3.141592653589793238f) / fTotalWeight;
        }

        renderer = transform.GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();

        bufTmpBuffers[0] = new ComputeBuffer(RENDER_FACE_COUNT * PROBE_COFF_COUNT * 3, sizeof(float));
        bufTmpBuffers[1] = new ComputeBuffer(RENDER_FACE_COUNT * PROBE_RENDER_SIZE * PROBE_COFF_COUNT * 3, sizeof(float));

        shColor = new SHShaderColor();

        shTexture = new RenderTexture(PROBE_RENDER_SIZE, PROBE_RENDER_SIZE, 24);
        shTexture.dimension = TextureDimension.Cube;
    }
    public void ReleaseUnit()
    {
        bufTmpBuffers[0].Release();
        bufTmpBuffers[1].Release();
    }
    public void BuildSH()
    {
        var tmpObject = new GameObject("ProbeCamera");
        var tmpCamera = tmpObject.AddComponent<Camera>();
        {
            //tmpCamera.renderingPath = RenderingPath.DeferredShading;
            //tmpCamera.allowHDR = true;
            tmpCamera.allowMSAA = false;
            tmpCamera.backgroundColor = new Color(0.192157f, 0.3019608f, 0.4745098f);
            tmpCamera.aspect = 1.0f;
            tmpCamera.fieldOfView = 90.0f;
            tmpCamera.nearClipPlane = 0.0001f;
            tmpCamera.farClipPlane = 1000.0f;
            //tmpCamera.clearFlags = CameraClearFlags.SolidColor;
            tmpCamera.clearFlags = CameraClearFlags.Skybox;
        }

        renderer.enabled = false;

        Vector3[] vSH;
        RenderSH(ref tmpCamera, ref shTexture, out vSH);
        UpdateSH(vSH);

        renderer.enabled = true;

        DestroyImmediate(tmpObject);
    }
    public void DrawSH()
    {
        if (renderer != null && propBlock != null)
        {
            renderer.GetPropertyBlock(propBlock);

            propBlock.SetTexture("_MainTex", shTexture);

            propBlock.SetVector("unity_SHAr", shColor.vBand1Base0R_vBand1Base1R_vBand1Base2R_vBand0Base0R);
            propBlock.SetVector("unity_SHAg", shColor.vBand1Base0G_vBand1Base1G_vBand1Base2G_vBand0Base0G);
            propBlock.SetVector("unity_SHAb", shColor.vBand1Base0B_vBand1Base1B_vBand1Base2B_vBand0Base0B);
            propBlock.SetVector("unity_SHBr", shColor.vBand2Base0R_vBand2Base1R_vBand2Base2R_vBand2Base3R);
            propBlock.SetVector("unity_SHBg", shColor.vBand2Base0G_vBand2Base1G_vBand2Base2G_vBand2Base3G);
            propBlock.SetVector("unity_SHBb", shColor.vBand2Base0B_vBand2Base1B_vBand2Base2B_vBand2Base3B);
            propBlock.SetVector("unity_SHC", shColor.vBand2Base4R_vBand2Base4G_vBand2Base4B);

            renderer.SetPropertyBlock(propBlock);
        }
    }


    private void Awake()
    {
        InitUnit();
    }

    private void Update()
    {
        DrawSH();
    }
}

