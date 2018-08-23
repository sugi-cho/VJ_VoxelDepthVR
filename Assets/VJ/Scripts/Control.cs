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


    public float focusDistance = 0.4f;

    public RealMesh realMesh;
    public Transform turnTable;


    Vector3 resetTurnPos;

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
        stageRoot.position = head.position + headDir*0.25f;
        stageRoot.rotation = rot;
    }

    public void TogglePauseVoxels()
    {
        realMesh.pause = !realMesh.pause;
    }

    // Use this for initialization
    void Start()
    {
        dof = postProcessVolume.profile.GetSetting<DepthOfField>();
        keyLight = lightTrs.GetComponent<Light>();
        litDefaultPos = lightTrs.localPosition;
        litDefaultRot = lightTrs.localRotation;
    }

    // Update is called once per frame
    void Update()
    {
        dof.focusDistance.value = focusDistance;
        if (Input.GetKeyDown(KeyCode.Space))
            ResetStage();
    }
}
