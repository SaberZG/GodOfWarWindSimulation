using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;


[ExecuteInEditMode]
public class PlanarReflection : MonoBehaviour {
    public LayerMask _reflectionMask = -1;
    public bool _reflectSkybox = false;
    public float _clipPlaneOffset = 0.07F;
    //反射图属性名
    const string _reflectionTex = "_ReflectionTex";
    Camera _reflectionCamera;
    Vector3 _oldpos;
    RenderTexture _bluredReflectionTexture;
    Material _sharedMaterial;
    //模糊效果相关参数
    public bool _blurOn = true;
    [Range(0.0f, 5.0f)]
    public float _blurSize = 1;
    [Range(0, 10)]
    public int _blurIterations = 2;
    [Range(1.0f, 4.0f)]
    public float _downsample = 1;
    //记录上述模糊参数，用于判断参数是否发生变化   
    private bool _oldBlurOn;
    private float _oldBlurSize;
    private int _oldBlurIterations;
    private float _oldDownsample;
    //模糊shader
    private Shader _blurShader;
    private Material _blurMaterial;
    //用来判断当前是否正在渲染反射图
    private static bool _insideRendering;
    private RenderTexture _reflectionTexture;

    Material BlurMaterial {
        get {
            if (_blurMaterial == null) {
                _blurMaterial = new Material(_blurShader);
                return _blurMaterial;
            }
            return _blurMaterial;
        }
    }

    void Awake() {
        _oldBlurOn = _blurOn;
        _oldBlurSize = _blurSize;
        _oldBlurIterations = _blurIterations;
        _oldDownsample = _downsample;
    }

    void Start() {
        _sharedMaterial = GetComponent<MeshRenderer>().sharedMaterial;
        _blurShader = Shader.Find("Hidden/KawaseBlur");
        if (_blurShader == null)
            Debug.LogError("缺少Hidden/KawaseBlur Shader");
    }
    
    // Cleanup all the objects we possibly have created
    private void OnDisable()
    {
        Cleanup();
    }

    private void OnDestroy()
    {
        Cleanup();
    }
    
    private void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += ExecutePlanarReflections;
    }
    
    private void Cleanup()
    {
        RenderPipelineManager.beginCameraRendering -= ExecutePlanarReflections;

        if (_reflectionCamera)
        {
            _reflectionCamera.targetTexture = null;
            SafeDestroy(_reflectionCamera.gameObject);
        }
        
        // if (_reflectionTexture)
        // {
        //     RenderTexture.ReleaseTemporary(_reflectionTexture);
        // }
        //
        // if (_bluredReflectionTexture)
        // {
        //     RenderTexture.ReleaseTemporary(_bluredReflectionTexture);
        // }
    }
    
    private static void SafeDestroy(Object obj)
    {
        if (Application.isEditor)
        {
            DestroyImmediate(obj);
        }
        else
        {
            Destroy(obj);
        }
    }

    bool _blurParamChanged;
    void Update()
    {
        if (_blurParamChanged)
        {
            _oldBlurOn = _blurOn;
            _oldBlurSize = _blurSize;
            _oldBlurIterations = _blurIterations;
            _oldDownsample = _downsample;
        }

        if (_blurOn != _oldBlurOn || _blurSize != _oldBlurSize || _blurIterations != _oldBlurIterations || _downsample!= _oldDownsample)
        {
            _blurParamChanged = true;
        }
    }

    //创建反射用的摄像机
    Camera CreateReflectionCamera(Camera cam)
    {
        //生成Camera
        String reflName = gameObject.name + "Reflection" + cam.name; 
        GameObject go = new GameObject(reflName);
        //go.hideFlags = HideFlags.HideAndDontSave;
        go.hideFlags = HideFlags.HideAndDontSave;
        Camera reflectCamera = go.AddComponent<Camera>();
        //设置反射相机的参数
        HoldCameraSettings(reflectCamera);
        //创建RT并绑定Camera
        if (!reflectCamera.targetTexture)
        {
            _reflectionTexture = CreateTexture(cam);
            reflectCamera.targetTexture = _reflectionTexture;
        }

        return reflectCamera;
    }
    
    //设置反射相机的参数
    void HoldCameraSettings(Camera heplerCam)
    {
        heplerCam.backgroundColor = Color.black;
        heplerCam.clearFlags = _reflectSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        // heplerCam.renderingPath = RenderingPath.Forward;
        heplerCam.cullingMask = _reflectionMask;
        heplerCam.allowMSAA = false;
        // heplerCam.enabled = false;
    }
    
    //创建RT 
    RenderTexture CreateTexture(Camera sourceCam)
    {
        int width = Mathf.RoundToInt(Screen.width / _downsample);
        int height = Mathf.RoundToInt(Screen.height / _downsample);
        RenderTextureFormat formatRT = sourceCam.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
        RenderTexture rt = new RenderTexture(width, height, 24, formatRT);
        rt.hideFlags = HideFlags.DontSave;
        return rt;
    }
    
    private void ExecutePlanarReflections(ScriptableRenderContext context, Camera camera)
    {
        Camera currentCam = camera;
        if (currentCam == null) {
            return;
        }

#if !UNITY_EDITOR
        if (!currentCam.gameObject.CompareTag("MainCamera"))
            return;
#endif

        if (_insideRendering) {
            return;
        }
        _insideRendering = true;

        if (_reflectionCamera == null) {
            _reflectionCamera = CreateReflectionCamera(currentCam);
        }

        //渲染反射图
        RenderReflection(context, currentCam, _reflectionCamera);

        //是否对反射图进行模糊
        if (_reflectionCamera && _sharedMaterial) {
            if (_blurOn) {
                if (_bluredReflectionTexture == null)
                    _bluredReflectionTexture = CreateTexture(currentCam);
                PostProcessTexture(context, currentCam, _reflectionCamera.targetTexture, _bluredReflectionTexture);
                _sharedMaterial.SetTexture(_reflectionTex, _bluredReflectionTexture);
            }
            else {
                _sharedMaterial.SetTexture(_reflectionTex, _reflectionCamera.targetTexture);
            }
        }

        _insideRendering = false;
    }

    //调用反射相机，渲染反射图
    void RenderReflection(ScriptableRenderContext context, Camera currentCam, Camera reflectCamera)
    {
        if (reflectCamera == null) {
            Debug.LogError("反射Camera无效");
            return;
        }
        if (_sharedMaterial && !_sharedMaterial.HasProperty(_reflectionTex))
        {
            Debug.LogError("Shader中缺少_ReflectionTex属性");
            return;
        }
        //保持反射相机的参数
        HoldCameraSettings(reflectCamera);

        if (_reflectSkybox) {
            if (currentCam.gameObject.GetComponent(typeof(Skybox))) {
                Skybox sb = (Skybox)reflectCamera.gameObject.GetComponent(typeof(Skybox));
                if (!sb) {
                    sb = (Skybox)reflectCamera.gameObject.AddComponent(typeof(Skybox));
                }
                sb.material = ((Skybox)currentCam.GetComponent(typeof(Skybox))).material;
            }
        }

        bool isInvertCulling = GL.invertCulling;
        GL.invertCulling = true;

        Transform reflectiveSurface = this.transform; //waterHeight;

        Vector3 eulerA = currentCam.transform.eulerAngles;

        reflectCamera.transform.eulerAngles = new Vector3(-eulerA.x, eulerA.y, eulerA.z);
        reflectCamera.transform.position = currentCam.transform.position;

        Vector3 pos = reflectiveSurface.transform.position;
        pos.y = reflectiveSurface.position.y;
        Vector3 normal = reflectiveSurface.transform.up;
        float d = -Vector3.Dot(normal, pos) - _clipPlaneOffset;
        Vector4 reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

        Matrix4x4 reflection = Matrix4x4.zero;
        reflection = CalculateReflectionMatrix(reflection, reflectionPlane);
        _oldpos = currentCam.transform.position;
        Vector3 newpos = reflection.MultiplyPoint(_oldpos);

        reflectCamera.worldToCameraMatrix = currentCam.worldToCameraMatrix * reflection;

        Vector4 clipPlane = CameraSpacePlane(reflectCamera, pos, normal, 1.0f);

        Matrix4x4 projection = currentCam.projectionMatrix;
        projection = CalculateObliqueMatrix(projection, clipPlane);
        reflectCamera.projectionMatrix = projection;

        reflectCamera.transform.position = newpos;
        Vector3 euler = currentCam.transform.eulerAngles;
        reflectCamera.transform.eulerAngles = new Vector3(-euler.x, euler.y, euler.z);

        UniversalRenderPipeline.RenderSingleCamera(context, reflectCamera);

        GL.invertCulling = isInvertCulling;
    }

    static Matrix4x4 CalculateObliqueMatrix(Matrix4x4 projection, Vector4 clipPlane) {
        Vector4 q = projection.inverse * new Vector4(
            Mathf.Sign(clipPlane.x),
            Mathf.Sign(clipPlane.y),
            1.0F,
            1.0F
            );
        Vector4 c = clipPlane * (2.0F / (Vector4.Dot(clipPlane, q)));
        // third row = clip plane - fourth row
        projection[2] = c.x - projection[3];
        projection[6] = c.y - projection[7];
        projection[10] = c.z - projection[11];
        projection[14] = c.w - projection[15];

        return projection;
    }

    static Matrix4x4 CalculateReflectionMatrix(Matrix4x4 reflectionMat, Vector4 plane) {
        reflectionMat.m00 = (1.0F - 2.0F * plane[0] * plane[0]);
        reflectionMat.m01 = (-2.0F * plane[0] * plane[1]);
        reflectionMat.m02 = (-2.0F * plane[0] * plane[2]);
        reflectionMat.m03 = (-2.0F * plane[3] * plane[0]);

        reflectionMat.m10 = (-2.0F * plane[1] * plane[0]);
        reflectionMat.m11 = (1.0F - 2.0F * plane[1] * plane[1]);
        reflectionMat.m12 = (-2.0F * plane[1] * plane[2]);
        reflectionMat.m13 = (-2.0F * plane[3] * plane[1]);

        reflectionMat.m20 = (-2.0F * plane[2] * plane[0]);
        reflectionMat.m21 = (-2.0F * plane[2] * plane[1]);
        reflectionMat.m22 = (1.0F - 2.0F * plane[2] * plane[2]);
        reflectionMat.m23 = (-2.0F * plane[3] * plane[2]);

        reflectionMat.m30 = 0.0F;
        reflectionMat.m31 = 0.0F;
        reflectionMat.m32 = 0.0F;
        reflectionMat.m33 = 1.0F;

        return reflectionMat;
    }

    Vector4 CameraSpacePlane(Camera cam, Vector3 pos, Vector3 normal, float sideSign) {
        Vector3 offsetPos = pos + normal * _clipPlaneOffset;
        Matrix4x4 m = cam.worldToCameraMatrix;
        Vector3 cpos = m.MultiplyPoint(offsetPos);
        Vector3 cnormal = m.MultiplyVector(normal).normalized * sideSign;

        return new Vector4(cnormal.x, cnormal.y, cnormal.z, -Vector3.Dot(cpos, cnormal));
    }

    void PostProcessTexture(ScriptableRenderContext context, Camera cam, RenderTexture source, RenderTexture dest)
    {
        CommandBuffer buf = CommandBufferPool.Get("Blur Reflection Texture");
        // _cameras[cam] = buf;
        float width = source.width;
        float height = source.height;
        int rtW = Mathf.RoundToInt(width / _downsample);
        int rtH = Mathf.RoundToInt(height / _downsample);

        int blurredID = Shader.PropertyToID("_Temp1");
        int blurredID2 = Shader.PropertyToID("_Temp2");
        buf.GetTemporaryRT(blurredID, rtW, rtH, 0, FilterMode.Bilinear, source.format);
        buf.GetTemporaryRT(blurredID2, rtW, rtH, 0, FilterMode.Bilinear, source.format);

        buf.Blit((Texture)source, blurredID);
        for (int i = 0; i < _blurIterations; i++)
        {
            float iterationOffs = (i * 1.0f);
            buf.SetGlobalFloat("_Offset", iterationOffs / _downsample + _blurSize);
            buf.Blit(blurredID, blurredID2, BlurMaterial, 0);
            buf.Blit(blurredID2, blurredID, BlurMaterial, 0);
        }
        buf.Blit(blurredID, dest);

        buf.ReleaseTemporaryRT(blurredID);
        buf.ReleaseTemporaryRT(blurredID2);

        context.ExecuteCommandBuffer(buf);
    }

}