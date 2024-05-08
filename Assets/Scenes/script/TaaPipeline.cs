using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;


public class TaaPipeline : RenderPipeline
{
    private RenderTexture gdepth;                                               // depth attachment
    private RenderTexture[] gbuffers = new RenderTexture[4];                    // color attachments 
    private RenderTargetIdentifier[] gbufferID = new RenderTargetIdentifier[4]; // tex ID
    private RenderTexture[] colorBuffer = new RenderTexture[3];
    private CommandBuffer cmd;
    private int jitter_idx = 0;
    public Light lightcomponent;
    private Matrix4x4 lastVP;
    private int framecount;
    private int firstFrame;
    private bool _EnableTAA = false;
    private taa_behaviour taa_script;
    private Vector2[] Halton_2_3 =
    {
        new Vector2(0.0f, -1.0f / 3.0f),
        new Vector2(-1.0f / 2.0f, 1.0f / 3.0f),
        new Vector2(1.0f / 2.0f, -7.0f / 9.0f),
        new Vector2(-3.0f / 4.0f, -1.0f / 9.0f),
        new Vector2(1.0f / 4.0f, 5.0f / 9.0f),
        new Vector2(-1.0f / 4.0f, -5.0f / 9.0f),
        new Vector2(3.0f / 4.0f, 1.0f / 9.0f),
        new Vector2(-7.0f / 8.0f, 7.0f / 9.0f)
    };

    private void initTexutres()
    {
        gdepth = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth, RenderTextureReadWrite.Linear);
        //albedo 24 bits, spec 8
        gbuffers[0] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        //normal 30 bit (rgb), alpha 2 bit  
        gbuffers[1] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB2101010, RenderTextureReadWrite.Linear);
        //motion vector 32bits, roughness 16, metallic 16
        gbuffers[2] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.Linear);
        //emission 32 * 3, occulusion 8 bit
        gbuffers[3] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);

        colorBuffer[0] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        colorBuffer[1] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        colorBuffer[2] = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);

        for (int i = 0; i < gbuffers.Length; i++)
        {
            gbufferID[i] = gbuffers[i];
        }
        
    }
    private void releaseTextures()
    {
        gdepth.Release();
        gbuffers[0].Release();
        gbuffers[1].Release();
        gbuffers[2].Release();
        gbuffers[3].Release();
        colorBuffer[0].Release();
        colorBuffer[1].Release();
        colorBuffer[2].Release();
    }
        
    public TaaPipeline()
    {

        initTexutres();

        framecount = 0;

        firstFrame = 1;

        cmd = new CommandBuffer();


    }
    protected override void Render(ScriptableRenderContext context, Camera[] cameras)
    {
        // main camera
        Camera camera = cameras[0];
        if (camera.GetComponent<taa_behaviour>() != null && this._EnableTAA != camera.GetComponent<taa_behaviour>()._EnableTAA)
        {
            this._EnableTAA = camera.GetComponent<taa_behaviour>()._EnableTAA;
            Debug.Log("Status Changed" + _EnableTAA);
        }
        if (Screen.width != gdepth.width || Screen.height != gdepth.height)
        {
            releaseTextures();
            initTexutres();
        }
        context.SetupCameraProperties(camera);
        
        cmd.name = "gbuffer";
        cmd.SetRenderTarget(gbufferID, gdepth);
        
        for (int i = 0; i < 4; i++)
            cmd.SetGlobalTexture("_GT" + i, gbuffers[i]);

        cmd.SetGlobalVector("_ScreenSize", new Vector2(Screen.width, Screen.height));
        // clear screen
        cmd.ClearRenderTarget(true, true, new Color(0, 0, 0, 1), 1.0f);
        Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        //Debug.Log(camera.projectionMatrix);
        //Debug.Log(projMatrix);
        //projMatrix = camera.projectionMatrix;
        Matrix4x4 vpMatrix = projMatrix * viewMatrix;
        Matrix4x4 vpMatrixInv = vpMatrix.inverse;
        cmd.SetGlobalMatrix("_vpMatrix", vpMatrix);
        cmd.SetGlobalMatrix("_vpMatrixInv", vpMatrixInv);
        cmd.SetGlobalMatrix("_LastVP", lastVP);
        if(this._EnableTAA)
            cmd.SetGlobalVector("_Jitter", Halton_2_3[jitter_idx] / new Vector2(Screen.width, Screen.height));
        else
            cmd.SetGlobalVector("_Jitter", new Vector2(0, 0));
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        // cull
        camera.TryGetCullingParameters(out var cullingParameters);
        var cullingResults = context.Cull(ref cullingParameters);

        // config settings
        ShaderTagId shaderTagId = new ShaderTagId("gbuffer");   // 使用 LightMode 为 gbuffer 的 shader
        SortingSettings sortingSettings = new SortingSettings(camera);
        DrawingSettings drawingSettings = new DrawingSettings(shaderTagId, sortingSettings);
        FilteringSettings filteringSettings = FilteringSettings.defaultValue;
        Shader.SetGlobalColor("_Color", new Color(1, 1, 1));

        // render
        context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

        // skybox and Gizmos
        context.DrawSkybox(camera);
        //if (Handles.ShouldRenderGizmos())
        //{
        //   context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
        //   context.DrawGizmos(camera, GizmoSubset.PostImageEffects);
        //}
        context.Submit();

        LightPass(context, colorBuffer[framecount % 2]);

        if (this._EnableTAA)
            TAAPass(context, colorBuffer[framecount % 2], colorBuffer[(framecount + 1) % 2], colorBuffer[2]);

        ToScreen(context, colorBuffer[framecount % 2]);
        
        jitter_idx = (jitter_idx + 1) % 8;
        framecount += 1;
        framecount = framecount % 2;

        lastVP = vpMatrix;

    }
     
    
    void LightPass(ScriptableRenderContext context, RenderTexture rt)
    {
        //  Blit
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "lightpass";
        cmd.SetGlobalTexture("_gdepth", gdepth);
        Material mat = new Material(Shader.Find("TaaRP/lightpass"));
        cmd.Blit(gbuffers[0], rt, mat);
        context.ExecuteCommandBuffer(cmd);
        cmd.Release();

        context.Submit();
    }

    void TAAPass(ScriptableRenderContext context, RenderTexture currentColor , RenderTexture preColor, RenderTexture to)
    {
        //  Blit
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "taapass";
        
        cmd.SetGlobalInt("firstFrame", firstFrame);
        cmd.SetGlobalTexture("currentColor", currentColor);
        //cmd.SetGlobalTexture("previousColor", preColor);
        cmd.SetGlobalTexture("currentDepth", gdepth);
        cmd.SetGlobalTexture("velocityTexture", gbuffers[2]);
        cmd.SetGlobalVector("_ScreenSize", new Vector2(Screen.width, Screen.height));

        Material mat = new Material(Shader.Find("TaaRP/taapass"));
        cmd.Blit(preColor, to, mat);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        cmd.name = "copy";
        cmd.Blit(to, currentColor);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();

        firstFrame = 0;
        context.Submit();
    }

    void ToScreen(ScriptableRenderContext context, RenderTexture rt)
    {
        CommandBuffer cmd = new CommandBuffer();
        cmd.name = "toscreen";
        cmd.Blit(rt, BuiltinRenderTextureType.CameraTarget);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
        context.Submit();
    }
}
