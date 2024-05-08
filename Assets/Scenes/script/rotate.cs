using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class rotate : MonoBehaviour
{
    public Transform myObject;
    private Matrix4x4 lastM;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        var myMaterial = myObject.GetComponent<Renderer>().material;
        myMaterial.SetMatrix("_LastM", lastM);
    }

    void OnEnable()
    {
        Debug.Log("OnEnable");
        RenderPipelineManager.endCameraRendering += RenderPipelineManager_endCameraRendering;
    }

    void OnDisable()
    {
        RenderPipelineManager.endCameraRendering -= RenderPipelineManager_endCameraRendering;
    }

    private void RenderPipelineManager_endCameraRendering(ScriptableRenderContext context, Camera camera)
    {
        OnPostRender();
    }
    private void OnPostRender()
    {
        Debug.Log("Rotate");
        lastM = GetComponent<Transform>().localToWorldMatrix;
        myObject.Rotate(new Vector3(0, -2, 0));
    }
}
