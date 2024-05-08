using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/TaaPipeline")]
public class TaaPipelineAsset : RenderPipelineAsset
{

    protected override RenderPipeline CreatePipeline()
    {
        return new TaaPipeline();
    }
}
