using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[ExecuteInEditMode]
public class FogOfWar : MonoBehaviour
{
    public enum Mode
    {
        BlendWithShadow,
        Overlay
    }

    public enum Resolution
    {
        _2048,
        _1024,
        _512,
        _256,
        _128
    }

    enum Pass
    {
        Vision,
        Occluder,
        PostProcess,
        Sampling,
        Saturate
    }

    private const int MaxBlurItterations = 10;

    public Mode mode;
    [Space]
    public float radius = 10f;
    public Resolution resolution;
    [Tooltip("distance of the near clip plane from vision source, useful when the vision source inside the geometry to clip the ceiling")]
    public float CameraOffset = 5f;
    public float ShadowReach = 1f;
    public Rect area;
    public List<Transform> VisionSources = new List<Transform>();
    public List<MeshFilter> Occluders = new List<MeshFilter>();
    [Space]
    public float NoiseScale = 20;
    public float NoiseMagnitude = 1;
    [Space]
    [Range(0, MaxBlurItterations)]
    public int BlurItterations = 2;
    [Space]
    public float BlendSpeed = 5;

    static int
        fow_rt_id = Shader.PropertyToID("_FOW"),
        saturate_rt_id = Shader.PropertyToID("_Saturate"),
        visionSrcWS_id = Shader.PropertyToID("_VisionSource_WS"),
        visionSrcSS_id = Shader.PropertyToID("_VisionSource_SS"),
        noiseParam_id = Shader.PropertyToID("_NoiseParam"),
        blendFactor_id = Shader.PropertyToID("_BlendFactor"),
        bounds_id = Shader.PropertyToID("_Bounds");

    private Camera camera;
    private CommandBuffer buffer;
    private Mesh quad_mesh;
    private Material fow_mat;
    private Material blur_mat;

    private static int BlurPyramidID;
    private RenderTexture fow_0;
    private RenderTexture fow_1;

    [ImageEffectUsesCommandBuffer]
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (VisionSources.Count > 0 && Occluders.Count > 0)
        {
            buffer.Clear();

            buffer.SetGlobalVector(bounds_id, new Vector4(area.min.x, area.min.y, area.max.x, area.max.y));

            var desc = fow_0.descriptor;
            buffer.SetRenderTarget(fow_0, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.DontCare);
            buffer.ClearRenderTarget(false, true, Color.black);

            for (int i = 0; i < VisionSources.Count; i++)
                DrawVisionSource(VisionSources[i].position, radius);

            buffer.SetGlobalVector(noiseParam_id, new Vector4(NoiseScale, 0, NoiseMagnitude));
            buffer.SetGlobalFloat(blendFactor_id, BlendSpeed * Time.deltaTime);

            buffer.Blit(fow_0, fow_1, fow_mat, (int)Pass.PostProcess);
            buffer.Blit(fow_1, fow_0, fow_mat, (int)Pass.Saturate);
            buffer.Blit(fow_0, fow_1, fow_mat, (int)Pass.Saturate);

            Blur(fow_0);

            buffer.SetGlobalTexture(fow_rt_id, fow_0);

            if (mode == Mode.Overlay)
                FOWOverlay(source, destination);

            Graphics.ExecuteCommandBuffer(buffer);

            if (mode == Mode.BlendWithShadow)
                Graphics.Blit(source, destination);
        }
        else
            Graphics.Blit(source, destination);
    }

    void DrawVisionSource(Vector3 pos,float radius)
    {
        float dominant = Mathf.Max(area.width, area.height);
        float texelPerUnit = GetResolution(resolution) / dominant;
        Vector3 pos_ss = default;
        pos_ss.x = (pos.x - area.xMin); 
        pos_ss.y = (pos.z - area.yMin); 
        pos_ss.z = radius;
        pos_ss *= texelPerUnit;
        buffer.SetGlobalVector(visionSrcSS_id, pos_ss);
        buffer.SetGlobalVector(visionSrcWS_id, new Vector4(pos.x, pos.y, pos.z, radius));
        buffer.SetGlobalVector("_FOWParams", new Vector4(ShadowReach,0.1f,0,0));

        float n = 0.01f;
        float f = n + CameraOffset;

        Matrix4x4 view =
            Matrix4x4.TRS(new Vector3(area.center.x, pos.y + f, area.center.y),
            Quaternion.LookRotation(Vector3.down, Vector3.forward),
            Vector3.one).inverse;

        Matrix4x4 projection = default;
        projection.m00 = 2f/area.width;
        projection.m11 = -2f/area.height;
        projection.m22 =  2f / (f - n);  
        projection.m23 = -(f + n) / (f - n);
        projection.m33 = 1f;

        buffer.SetViewProjectionMatrices(view, projection);

        var visionMatrix = Matrix4x4.TRS(pos,
            Quaternion.LookRotation(Vector3.up, Vector3.forward),
            new Vector3(2*radius,2*radius, 1f));

        buffer.DrawMesh(quad_mesh, visionMatrix, fow_mat, 0, (int)Pass.Vision);

        pos.y = CameraOffset * 0.5f + pos.y;
        Bounds bounds = new Bounds(pos, new Vector3(radius*2, CameraOffset, radius*2));
        var cullResult = Cull(bounds);

        for (int i = 0; i < cullResult.Count; i++)
        {
            buffer.DrawMesh(cullResult[i].sharedMesh,
                cullResult[i].transform.localToWorldMatrix,
                fow_mat, 0, (int)Pass.Occluder);
        }
    }

    private void FOWOverlay(RenderTexture source, RenderTexture destination)
    {
        buffer.SetGlobalFloat("_BaseLevel", 0.2f);
        buffer.SetGlobalMatrix("_View_I", camera.transform.localToWorldMatrix);
        buffer.SetGlobalFloat("_FocalDistance", 1f / Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad));
        buffer.Blit(source, destination, fow_mat, (int)Pass.Sampling);
        buffer.SetGlobalTexture(fow_rt_id, Texture2D.whiteTexture);
    }

    private static string ToString(Matrix4x4 matrix4X4)
    {
        var format = "0.00";
        return
            $"{matrix4X4.m00.ToString(format)} ,{matrix4X4.m01.ToString(format)} ,{matrix4X4.m02.ToString(format)} ,{matrix4X4.m03.ToString(format)}\n" +
            $"{matrix4X4.m10.ToString(format)} ,{matrix4X4.m11.ToString(format)} ,{matrix4X4.m12.ToString(format)} ,{matrix4X4.m13.ToString(format)}\n" +
            $"{matrix4X4.m20.ToString(format)} ,{matrix4X4.m21.ToString(format)} ,{matrix4X4.m22.ToString(format)} ,{matrix4X4.m23.ToString(format)}\n" +
            $"{matrix4X4.m30.ToString(format)} ,{matrix4X4.m31.ToString(format)} ,{matrix4X4.m32.ToString(format)} ,{matrix4X4.m33.ToString(format)}";
    }

    private void OnEnable()
    {
        camera = Camera.main;

        blur_mat = new Material(Shader.Find("Hidden/Blur"));
        BlurPyramidID = Shader.PropertyToID("_BlurPyramid0");
        for (int i = 1; i < MaxBlurItterations * 2; i++)
            Shader.PropertyToID("_BlurPyramid" + i);

        var size = GetSize();
        var desc = new RenderTextureDescriptor()
        {
            width = size.x,
            height = size.y,
            dimension = TextureDimension.Tex2D,
            graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat,
            volumeDepth = 1,
            msaaSamples = 1,
            depthBufferBits = 0,
        };
        fow_0 = new RenderTexture(desc);
        fow_1 = new RenderTexture(desc);

        quad_mesh = GetPrimitiveMesh(PrimitiveType.Quad);
        fow_mat = new Material(Shader.Find("Hidden/FOW"));
        buffer = new CommandBuffer() { name = "FOW" };
    }
    
    Vector2Int GetSize()
    {
        var res = GetResolution(resolution);
        return area.width > area.height ?
            new Vector2Int(res, (int)(area.height * res / area.width) ) :
            new Vector2Int((int)(area.width * res / area.height) , res) ;
    }

    private void OnDisable()
    {
        fow_0?.Release();
        fow_1?.Release();
        SafeDestroy(fow_0);
        SafeDestroy(fow_1);
        Shader.SetGlobalTexture(fow_rt_id, Texture2D.whiteTexture);

        SafeDestroy(blur_mat);
        SafeDestroy(fow_mat);
        buffer?.Release();
    }

    //bloom by Jasper Flick
    private void Blur(RenderTargetIdentifier source)
    {
        if (BlurItterations < 1)
            return;

        var dsec = fow_0.descriptor;
        int height = GetResolution(resolution) / 2;
        int fromId = 0, toId = BlurPyramidID + 1;
        int i;
        for (i = 0; i < BlurItterations; i++)
        {
            if (height < 64)
                break;

            int midId = toId - 1;
            dsec.width = height;
            dsec.height = height;
            buffer.GetTemporaryRT( midId, dsec , FilterMode.Bilinear);
            buffer.GetTemporaryRT( toId, dsec, FilterMode.Bilinear);
            buffer.Blit(i == 0 ? source : fromId, midId, blur_mat, 0);
            buffer.Blit(midId, toId, blur_mat, 1);
            fromId = toId;
            toId += 2;
            height /= 2;
        }

        if (i > 1)
        {
            buffer.ReleaseTemporaryRT(fromId - 1);
            toId -= 5;
            for (i -= 1; i > 0; i--)
            {
                buffer.Blit(fromId, toId, blur_mat, 2);
                buffer.ReleaseTemporaryRT(fromId);
                buffer.ReleaseTemporaryRT(toId + 1);
                fromId = toId;
                toId -= 2;
            }
        }
        else
        {
            buffer.ReleaseTemporaryRT(BlurPyramidID);
        }
        buffer.Blit(fromId, source, blur_mat, 2);
        buffer.ReleaseTemporaryRT(fromId);
    }

    private int GetResolution(Resolution resolution)
    {
        switch (resolution)
        {
            case Resolution._2048:
                return 2048;
            case Resolution._1024:
                return 1024;
            case Resolution._512:
                return 512;
            case Resolution._256:
                return 256;
            case Resolution._128:
                return 128;
        }
        return 0;
    }

    public List<MeshFilter> Cull(Bounds bounds)
    {
        var renderers = new List<MeshFilter>();
        foreach (var filter in Occluders)
        {
            if (bounds.Intersects(filter.GetComponent<MeshRenderer>().bounds))
                renderers.Add(filter);
        }
        return renderers;
    }

    Mesh GetPrimitiveMesh(PrimitiveType primitive)
    {
        var go = GameObject.CreatePrimitive(primitive);
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        SafeDestroy(go);
        return mesh;
    }

    void SafeDestroy(Object obj)
    {
        if (obj == null)
            return;

#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlaying)
            Destroy(obj);
        else
            DestroyImmediate(obj, false);
#else
        Destroy(obj);
#endif
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(new Vector3(area.min.x, CameraOffset, area.min.y), new Vector3(area.max.x, CameraOffset, area.min.y));
        Gizmos.DrawLine(new Vector3(area.max.x, CameraOffset, area.min.y), new Vector3(area.max.x, CameraOffset, area.max.y));
        Gizmos.DrawLine(new Vector3(area.max.x, CameraOffset, area.max.y), new Vector3(area.min.x, CameraOffset, area.max.y));
        Gizmos.DrawLine(new Vector3(area.min.x, CameraOffset, area.max.y), new Vector3(area.min.x, CameraOffset, area.min.y));
    }
}
