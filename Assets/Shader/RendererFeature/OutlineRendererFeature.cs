using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class OutlineRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class OutlineSettings
    {
        public Color outlineColor = Color.black;
        [Range(0.5f, 5f)] public float outlineThickness = 1f;
        [Range(0f, 1f)]   public float normalThreshold  = 0.3f;
        [Tooltip("深度边缘检测阈值。保持 999 = 禁用（推荐）；调低如 0.1 可开启，但可能出现背景描边穿透到前景。")]
        public float depthThreshold = 999f;
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public OutlineSettings settings = new OutlineSettings();

    private OutlinePass    outlinePass;
    private Material       outlineMaterial;

    public override void Create()
    {
        var shader = Shader.Find("Hidden/GourmetLine/ScreenSpaceOutline");
        if (shader == null)
        {
            Debug.LogWarning("OutlineRendererFeature: 找不到 ScreenSpaceOutline shader");
            return;
        }
        outlineMaterial = CoreUtils.CreateEngineMaterial(shader);
        outlinePass = new OutlinePass(outlineMaterial);
        outlinePass.renderPassEvent = settings.renderPassEvent;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (outlineMaterial == null) return;
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;

        outlinePass.Setup(settings);
        // 自动请求深度图和法线图（无需在 URP Asset 里手动勾选）
        outlinePass.ConfigureInput(ScriptableRenderPassInput.Normal | ScriptableRenderPassInput.Depth);
        renderer.EnqueuePass(outlinePass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(outlineMaterial);
    }

    // ─── Inner Pass ─────────────────────────────────────────────────────────────

    class OutlinePass : ScriptableRenderPass
    {
        private Material       material;
        private OutlineSettings settings;
        private RTHandle       tempRT;

        static readonly int OutlineColorID     = Shader.PropertyToID("_OutlineColor");
        static readonly int OutlineThicknessID = Shader.PropertyToID("_OutlineThickness");
        static readonly int DepthThresholdID   = Shader.PropertyToID("_DepthThreshold");
        static readonly int NormalThresholdID  = Shader.PropertyToID("_NormalThreshold");

        public OutlinePass(Material mat)
        {
            material = mat;
        }

        public void Setup(OutlineSettings s)
        {
            settings = s;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref tempRT, desc, name: "_OutlineTempRT");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (material == null) return;

            var cmd = CommandBufferPool.Get("ScreenSpaceOutline");

            material.SetColor(OutlineColorID,       settings.outlineColor);
            material.SetFloat(OutlineThicknessID,   settings.outlineThickness);
            material.SetFloat(DepthThresholdID,     settings.depthThreshold);
            material.SetFloat(NormalThresholdID,    settings.normalThreshold);

            var cameraTarget = renderingData.cameraData.renderer.cameraColorTargetHandle;

            // 读 cameraTarget → 用 outline shader 写到 tempRT → 回写 cameraTarget
            Blitter.BlitCameraTexture(cmd, cameraTarget, tempRT, material, 0);
            Blitter.BlitCameraTexture(cmd, tempRT, cameraTarget);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd) { }

        public void Dispose()
        {
            tempRT?.Release();
        }
    }
}
