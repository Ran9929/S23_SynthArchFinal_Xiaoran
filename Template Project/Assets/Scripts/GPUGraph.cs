using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GPUGraph : MonoBehaviour
{
    [SerializeField] private float range = 10f;
    [SerializeField]
    ComputeShader computeShader; //访问computer shader
    
    [SerializeField]
    Material material;

    [SerializeField]
    Mesh mesh;
    
    const int maxResolution = 1000; //始终为最大分辨率分配一个缓冲区
    
    [SerializeField, Range(10, maxResolution)]
    int resolution = 10;

    [SerializeField]
    FunctionLibrary.FunctionName function;

    public enum TransitionMode { Cycle, Random }

    [SerializeField]
    TransitionMode transitionMode = TransitionMode.Cycle;

    [SerializeField, Min(0f)]
    float functionDuration = 1f, transitionDuration = 1f;
    
    float duration;

    bool transitioning;

    FunctionLibrary.FunctionName transitionFunction;

    ComputeBuffer positionsBuffer;

    static readonly int //标识符永远不变 故 static readonly
        positionsId = Shader.PropertyToID("_Positions"),
        resolutionId = Shader.PropertyToID("_Resolution"),
        stepId = Shader.PropertyToID("_Step"),
        timeId = Shader.PropertyToID("_Time"),
        transitionProgressId = Shader.PropertyToID("_TransitionProgress");

    void UpdateFunctionOnGPU () {
        float step = range / resolution;
        computeShader.SetInt(resolutionId, resolution);
        computeShader.SetFloat(stepId, step);
        computeShader.SetFloat(timeId, Time.time);
        if (transitioning) {
            computeShader.SetFloat(
                transitionProgressId,
                Mathf.SmoothStep(0f, range/2f, duration / transitionDuration) //更平滑的lerp
            );
        }
        var kernelIndex =
            (int)function +
            (int)(transitioning ? transitionFunction : function) *
            FunctionLibrary.FunctionCount; //function 目录
        computeShader.SetBuffer(kernelIndex, positionsId, positionsBuffer); //设置位置缓冲区
        int groups = Mathf.CeilToInt(resolution / 8f);//向上取整
        computeShader.Dispatch(kernelIndex, groups, groups, 1);//运行kernel
        material.SetBuffer(positionsId, positionsBuffer);
        material.SetFloat(stepId, step);//只需要buffer 和 scale
        var bounds = new Bounds(Vector3.zero, Vector3.one * (2 * range / resolution)); //AABB bounds 也要考虑点的大小
        Graphics.DrawMeshInstancedProcedural(
            mesh, 0, material, bounds, resolution * resolution
            ); 
    }
    
    void OnEnable () {  //enable 时调用 在播放模式改代码也不会消失 hotreload
        positionsBuffer = new ComputeBuffer(maxResolution * maxResolution,3*4); //在GPU上储存最大resolution
    }
    
    void OnDisable () { //禁用时释放buffer
        positionsBuffer.Release();
        positionsBuffer = null;
    }
    
    void Update () {
        duration += Time.deltaTime;
        if (transitioning)
        {
            if (duration >= transitionDuration) {
                duration -= transitionDuration;
                transitioning = false;
            }
        }
        else if (duration >= functionDuration) 
        {
            duration -= functionDuration;
            transitioning = true;
            transitionFunction = function;
            PickNextFunction();
        }
        UpdateFunctionOnGPU();
    }
	
    void PickNextFunction () {
        function = transitionMode == TransitionMode.Cycle ?
            FunctionLibrary.GetNextFunctionName(function) :
            FunctionLibrary.GetRandomFunctionNameOtherThan(function);
    }
    
    
}
