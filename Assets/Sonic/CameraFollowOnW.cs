using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollowOnW : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                 

    [Header("Follow settings")]
    public Vector3 offset = new Vector3(0f, 1.8f, -4f);  
    public float positionLerp = 8f;          
    public float rotationLerp = 12f;

    [Header("Optional: return to an idle view when not moving")]
    public bool returnToIdle = false;
    public Transform idleAnchor;             // empty GameObject for idle cam pose
    Vector3 idlePos; Quaternion idleRot;

    void Awake()
    {
        if (!target) Debug.LogWarning("CameraFollowOnW: assign Target.");
        if (!idleAnchor) { idlePos = transform.position; idleRot = transform.rotation; }
    }

    void LateUpdate()
    {
        bool forward = Input.GetKey(KeyCode.W);

        if (forward && target)
        {
            Vector3 desiredPos = target.position + target.TransformVector(offset);
            float pt = 1f - Mathf.Exp(-positionLerp * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPos, pt);

            Quaternion desiredRot =
                Quaternion.LookRotation((target.position + Vector3.up * 1.2f) - transform.position, Vector3.up);
            float rt = 1f - Mathf.Exp(-rotationLerp * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rt);
        }
        else if (returnToIdle)
        {
            Vector3 backPos = idleAnchor ? idleAnchor.position : idlePos;
            Quaternion backRot = idleAnchor ? idleAnchor.rotation : idleRot;

            float pt = 1f - Mathf.Exp(-(positionLerp * 0.6f) * Time.deltaTime);
            float rt = 1f - Mathf.Exp(-(rotationLerp * 0.6f) * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, backPos, pt);
            transform.rotation = Quaternion.Slerp(transform.rotation, backRot, rt);
        }
    }
}