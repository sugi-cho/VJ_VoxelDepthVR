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
    public Renderer previewPlane;

    [Header("camera, light")]
    public Transform cameraTrs;
    public Transform focalPoint;
    public PostProcessVolume postProcessVolume;
    Camera handCam;
    SpoutSender sender;
    DepthOfField dof;

    public Transform lightTrs;
    Light keyLight;
    Vector3 litDefaultPos;

    [Header("stage")]
    public Transform stageRoot;
    public Transform turnObject;

    [Header("motion controller settings")]
    public float stickSleep = 0.1f;
    public float dragFactor = 10f;
    public float rotateSpeedMax = 10f;
    public float breakRadius = 0.5f;
    public float focalDistanceMin = 0.3f;
    public float focalDistanceMax = 5.0f;

    [Header("obj for effect")]
    public RealMesh realMesh;

    [Header("for debug")]
    public Transform trs4debug;

    #region Grip
    public void OnGripLight(Transform trs)
    {
        GripObject(lightTrs, trs);
    }
    public void OnGripCamera(Transform trs)
    {
        GripObject(cameraTrs, trs);
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
        keyLight.color = Color.HSVToRGB(angle, 0.4f, 1f);
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
        turnObject.RotateAround(focalPoint.position, Vector3.up, angle);
    }
    public void MoveStage(Vector2 axis)
    {
        stageRoot.position += (head.right * axis.x + head.forward * axis.y) * Time.deltaTime;
    }
    #endregion

    #region Trigger
    public void AddInpact(Transform trs)
    {
        realMesh.AddImpact(trs.position);
    }
    public void TogglePause(Transform trs)
    {
        realMesh.pause = !realMesh.pause;
    }
    #endregion

    #region Menu
    public void AddRandomEffect()
    {

    }
    public void ResetStage()
    {
        cameraTrs.parent = turnObject;
        turnObject.localPosition =  Vector3.zero;
        turnObject.localRotation =  Quaternion.identity;
        cameraTrs.parent = stageRoot;

        var headDir = head.forward;
        headDir.y = 0f;
        headDir = headDir.normalized;
        var rot = Quaternion.LookRotation(headDir);
        stageRoot.position = head.position + headDir * 0.25f;
        stageRoot.rotation = rot;
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
        previewPlane.SetTexture("_MainTex", sender.sharedTexture);
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
    }
}
