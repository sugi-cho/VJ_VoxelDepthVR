using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

using sugi.cc;

public class Control : SingletonMonoBehaviour<Control>
{
    [Header("VR Control")]
    public Transform head;

    [Header("camera, light")]
    public Transform cameraTrs;
    public PostProcessVolume postProcessVolume;
    Camera handCam;
    DepthOfField dof;

    public Transform lightTrs;
    Light keyLight;
    Vector3 litDefaultPos;
    Quaternion litDefaultRot;

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

    public RealMesh realMesh;
    public Transform turnTable;

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

    public void OnLightColor(Vector2 axis)
    {
        var angle = Mathf.Atan2(axis.y, axis.x) / (2f * Mathf.PI);
        angle = (angle + 1f) % 1f;
        keyLight.color = Color.HSVToRGB(angle, 0.4f, 1f);
    }
    public void OnLight(bool vanish)
    {
        keyLight.enabled = !vanish;
    }

    public void SetFocalDistance(Vector2 axis)
    {
        var t = Mathf.InverseLerp(-1f, 1f, axis.y);
        dof.focusDistance.value = Mathf.Lerp(focalDistanceMin, focalDistanceMax, t);
    }

    public void TogglePause(Transform trs)
    {
        realMesh.pause = !realMesh.pause;
    }

    public void ResetStage()
    {
        lightTrs.localPosition = litDefaultPos;
        lightTrs.localRotation = litDefaultRot;
        turnTable.localPosition = cameraTrs.localPosition = Vector3.zero;
        turnTable.localRotation = cameraTrs.localRotation = Quaternion.identity;

        var headDir = head.forward;
        headDir.y = 0f;
        headDir = headDir.normalized;
        var rot = Quaternion.LookRotation(headDir);
        stageRoot.position = head.position + headDir * 0.25f;
        stageRoot.rotation = rot;
    }

    public void TogglePauseVoxels()
    {
        realMesh.pause = !realMesh.pause;
    }

    // Use this for initialization
    void Start()
    {
        handCam = cameraTrs.GetComponent<Camera>();
        dof = postProcessVolume.profile.GetSetting<DepthOfField>();
        keyLight = lightTrs.GetComponent<Light>();
        litDefaultPos = lightTrs.localPosition;
        litDefaultRot = lightTrs.localRotation;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            ResetStage();
    }
}
