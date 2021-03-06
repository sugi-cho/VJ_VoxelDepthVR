﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

using Valve.VR;

public class MotionController : MonoBehaviour
{
    public Transform controllerDirection;
    public TransformEvent onGrip;
    public XYEvent onTouchPad;
    public XYEvent onStick;
    public TransformEvent onPadButton;
    public TransformEvent onTrigger;
    public UnityEvent onMenu;

    public Vector2 padAxis;
    public Vector2 stickAxis;

    SteamVR_TrackedObject trackedObject;

    readonly ulong touchPad = SteamVR_Controller.ButtonMask.Touchpad;
    readonly ulong grip = SteamVR_Controller.ButtonMask.Grip;
    readonly ulong trigger = SteamVR_Controller.ButtonMask.Trigger;
    readonly ulong menu = SteamVR_Controller.ButtonMask.ApplicationMenu;

    // Use this for initialization
    void Start()
    {
        trackedObject = GetComponent<SteamVR_TrackedObject>();
    }

    // Update is called once per frame
    void Update()
    {
        var device = SteamVR_Controller.Input((int)trackedObject.index);

        if (device.GetPress(grip))
            onGrip.Invoke(controllerDirection);

        padAxis = device.GetAxis(EVRButtonId.k_EButton_Axis0);
        stickAxis = device.GetAxis(EVRButtonId.k_EButton_Axis2);

        if (device.GetTouch(touchPad))
            onTouchPad.Invoke(padAxis);
        if (device.GetPress(touchPad))
            onPadButton.Invoke(controllerDirection);

        var sleep = Control.Instance.stickSleep;
        if (sleep * sleep < Vector2.Dot(stickAxis, stickAxis))
            onStick.Invoke(stickAxis);

        if (device.GetPressDown(trigger))
            onTrigger.Invoke(controllerDirection);

        if (device.GetPressDown(menu))
            onMenu.Invoke();
    }

    [System.Serializable]
    public class TransformEvent : UnityEvent<Transform> { }
    [System.Serializable]
    public class XYEvent : UnityEvent<Vector2> { }
}
