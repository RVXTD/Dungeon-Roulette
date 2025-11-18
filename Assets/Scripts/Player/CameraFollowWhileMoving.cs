using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraFollowWhileMoving : MonoBehaviour
{
    public Transform target;                    // Sonic root
    public Vector3 offset = new Vector3(0f, 1.8f, -4f);
    public float positionLerp = 8f;
    public float rotationLerp = 12f;
    public float moveThreshold = 0.05f;         // how fast Sonic must be to count as "moving"

    CharacterController cc;
    Vector3 lastTargetPos;
    bool hadCC;

    void Start()
    {
        if (!target) Debug.LogWarning("Assign target to CameraFollowWhileMoving.");
        if (target)
        {
            cc = target.GetComponent<CharacterController>();
            hadCC = cc != null;
            lastTargetPos = target.position;
        }
    }

    void LateUpdate()
    {
        if (!target) return;

        // Detect motion (prefer CharacterController velocity; fall back to position delta).
        float speed =
            (hadCC && cc) ? cc.velocity.magnitude
                          : (target.position - lastTargetPos).magnitude / Mathf.Max(Time.deltaTime, 1e-5f);

        bool isMoving = speed > moveThreshold;

        if (isMoving)
        {
            Vector3 desiredPos = target.position + target.TransformVector(offset);
            float pt = 1f - Mathf.Exp(-positionLerp * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desiredPos, pt);

            Quaternion desiredRot =
                Quaternion.LookRotation((target.position + Vector3.up * 1.2f) - transform.position, Vector3.up);
            float rt = 1f - Mathf.Exp(-rotationLerp * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rt);
        }

        lastTargetPos = target.position; // update for next frame
    }
}