using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

using sugi.cc;
using Klak.Spout;

public class Control : SingletonMonoBehaviour<Control>
{
    [Header("VR Control")]
    public Transform head;
    public Transform handPosL;
    public Transform handPosR;
    public Renderer previewPlane;

    [Header("camera, light")]
    public Transform cameraTrs;
    public Transform focalPoint;
    public PostProcessVolume postProcessVolume;
    Camera handCam;
    SpoutSender sender;
    DepthOfField dof;

    public Transform lightTrs;
    public Light backLight;
    Light keyLight;
    Vector3 litDefaultPos;

    float backLightIntensity;
    float keyLightIntensity;


    [Header("stage")]
    public Transform stageRoot;
    public Transform turnObject;

    [Header("motion controller settings")]
    public float stickSleep = 0.1f;
    public float dragFactor = 10f;
    public float rotateSpeedMax = 10f;
    public float moveSpeedMax = 3f;
    public float breakRadius = 0.5f;
    public float focalDistanceMin = 0.3f;
    public float focalDistanceMax = 5.0f;

    [Header("obj for effect")]
    public RealMesh realMesh;

    [Header("for debug")]
    public Transform trs4debug;

    System.Action[] randomEffects;

    #region Grip
    public void OnGripLight(Transform trs)
    {
        GripObject(lightTrs, trs);
    }
    public void OnGripCamera(Transform trs)
    {
        cameraTrs.position = Vector3.Lerp(trs.position, cameraTrs.position, Mathf.Exp(-Time.deltaTime * dragFactor));
        var toDir = Vector3.Lerp(trs.forward, cameraTrs.forward, Mathf.Exp(-Time.deltaTime * dragFactor));
        cameraTrs.rotation = Quaternion.LookRotation(toDir);
    }
    void GripObject(Transform gripped, Transform gripper)
    {
        gripped.position = Vector3.Lerp(gripper.position, gripped.position, Mathf.Exp(-Time.deltaTime * dragFactor));
        gripped.rotation = Quaternion.Lerp(gripper.rotation, gripped.rotation, Mathf.Exp(-Time.deltaTime * dragFactor));
    }
    #endregion

    #region TouchPad
    public void OnLightColor(Vector2 axis)
    {
        var angle = Mathf.Atan2(axis.y, axis.x) / (2f * Mathf.PI);
        angle = (angle + 1f) % 1f;
        var sat = axis.magnitude * 0.5f;
        keyLight.color = Color.HSVToRGB(angle, sat * sat, 1f);
    }
    public void OnLightTarget(Transform trs)
    {
        lightTrs.localPosition = litDefaultPos;
        lightTrs.LookAt(trs);
    }

    public void SetFocalDistance(Vector2 axis)
    {
        var t = Mathf.InverseLerp(-1f, 1f, axis.y);
        dof.focusDistance.value = Mathf.Lerp(focalDistanceMin, focalDistanceMax, t);
    }
    public void EmitLitParticle(Transform trs)
    {
        var dst = (cameraTrs.position - trs.position).magnitude;
        dof.focusDistance.value = dst;
        realMesh.EmitParticle(trs.position);
    }
    #endregion

    #region Stick
    public void RotateStage(Vector2 axis)
    {
        var angle = rotateSpeedMax * axis.x * axis.x * Mathf.Sign(axis.x) * Time.deltaTime;
        turnObject.RotateAround(handPosL.position, Vector3.up, angle);
    }
    public void MoveStage(Vector2 axis)
    {
        axis.x = -axis.x * axis.x * Mathf.Sign(axis.x);
        axis.y = -axis.y * axis.y * Mathf.Sign(axis.y);

        stageRoot.position += (head.right * axis.x + head.forward * axis.y) * Time.deltaTime * moveSpeedMax;
    }
    #endregion

    #region Trigger
    public void AddInpact(Transform trs)
    {
        trs4debug.position = trs.position;
        realMesh.AddImpact(trs.position);
    }
    public void TogglePause(Transform trs)
    {
        trs4debug.position = trs.position;
        realMesh.pause = !realMesh.pause;
    }
    #endregion

    #region Menu
    public void AddRandomEffect()
    {
        var effect = randomEffects.GetRandom();
        effect.Invoke();
    }
    public void ResetStage()
    {
        cameraTrs.parent = turnObject;
        turnObject.localPosition = Vector3.zero;
        turnObject.localRotation = Quaternion.identity;
        cameraTrs.parent = stageRoot;

        var headDir = head.forward;
        headDir.y = 0f;
        headDir = headDir.normalized;
        var rot = Quaternion.LookRotation(headDir);
        stageRoot.position = head.position + headDir * 0.25f + Vector3.down;
        stageRoot.rotation = rot;

        realMesh.ResetParticle();
    }
    #endregion

    // Use this for initialization
    void Start()
    {
        handCam = cameraTrs.GetComponent<Camera>();
        dof = postProcessVolume.profile.GetSetting<DepthOfField>();
        keyLight = lightTrs.GetComponent<Light>();
        litDefaultPos = lightTrs.localPosition;

        sender = handCam.GetComponent<SpoutSender>();

        backLightIntensity = backLight.intensity;
        keyLightIntensity = keyLight.intensity;
        backLight.intensity = 0f;
        keyLight.intensity = 0f;

        randomEffects = new System.Action[] {
            () => {
                StartCoroutine(FadeLightRoutine(false));
                realMesh.SetMotionParticle(false);
                this.CallMethodDelayed(2f, realMesh.VerticalEffect);
            },
            () => {
                StartCoroutine(FadeLightRoutine(false));
                realMesh.SetMotionParticle(false);
                this.CallMethodDelayed(2f, realMesh.HorizonalEffect);
            },
            () => {
                StartCoroutine(FadeLightRoutine(false));
                realMesh.SetMotionParticle(false);
                this.CallMethodDelayed(2f, ()=>StartCoroutine(VanishAll()));
            },
        };
    }

    IEnumerator FadeLightRoutine(bool lit)
    {
        var from = lit ? 0 : 1f;
        var to = (from + 1f) % 2f;
        var t = 0f;
        while (t < 1f)
        {
            var val = Mathf.Lerp(from, to, t);
            backLight.intensity = val * backLightIntensity;
            keyLight.intensity = val * keyLightIntensity;
            yield return t += Time.deltaTime / 6f;
        }
        backLight.intensity = to * backLightIntensity;
        keyLight.intensity = to * keyLightIntensity;
        realMesh.SetMotionParticle(false);
    }
    IEnumerator VanishAll()
    {
        var from = -3f;
        var to = 3f;
        var t = 0f;
        while(t < 1f)
        {
            var val = Mathf.Lerp(from, to, t);
            realMesh.HeightLimitEffectt(val);
            yield return t += Time.deltaTime / 4f;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (previewPlane.material.mainTexture == null && sender.sharedTexture != null)
            previewPlane.material.mainTexture = sender.sharedTexture;
        focalPoint.position = cameraTrs.position + cameraTrs.forward * dof.focusDistance;

        if (Input.GetKeyDown(KeyCode.R))
            ResetStage();
        if (Input.GetKeyDown(KeyCode.I))
            AddInpact(trs4debug);
        if (Input.GetKey(KeyCode.E))
            EmitLitParticle(trs4debug);

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartCoroutine(FadeLightRoutine(false));
            realMesh.SetMotionParticle(false);
        }
        else if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            StartCoroutine(FadeLightRoutine(true));
        }
        else if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            realMesh.SetMotionParticle(true);
        }

        Shader.SetGlobalMatrix("_World2Handcam", cameraTrs.worldToLocalMatrix);
        Shader.SetGlobalFloat("_FocusDst", dof.focusDistance);
    }
}
