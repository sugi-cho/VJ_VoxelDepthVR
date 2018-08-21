using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

public class Control : MonoBehaviour
{

    public float focusDistance = 0.4f;
    public Transform turnPoint;

    public RealMesh realMesh;
    public Transform turnTable;
    public PostProcessVolume postProcessVolume;
    DepthOfField dof;


    Vector3 resetTurnPos;

    public void ResetRotate()
    {
        turnPoint.position = resetTurnPos;
        turnTable.localPosition = Vector3.zero;
        turnTable.localRotation = Quaternion.identity;
    }

    public void TogglePauseVoxels()
    {
        realMesh.pause = !realMesh.pause;
    }

    // Use this for initialization
    void Start()
    {
        resetTurnPos = turnPoint.position;
        dof = postProcessVolume.profile.GetSetting<DepthOfField>();
    }

    // Update is called once per frame
    void Update()
    {
        dof.focusDistance.value = focusDistance;
        turnTable.RotateAround(turnPoint.position, Vector3.up, turnPoint.localPosition.y * 10f * Time.deltaTime);
    }
}
