using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Custom one-hand grab transformer: rotates the object around local Z only,
/// tracking the grabbing hand's yaw (rotation around world up) directly each
/// frame. X/Y stay pinned to baselineX/baselineY. Assign this (instead of
/// GrabFreeTransformer) to the Grabbable's "One Grab Transformer" slot.
/// </summary>
public class OneGrabRotateZTransformer : MonoBehaviour, ITransformer
{
    [SerializeField] private float baselineX = 90f;
    [SerializeField] private float baselineY = 0f;

    private IGrabbable _grabbable;
    private Vector3 _prevHandFlatForward;
    private float _currentZAngle;

    public void Initialize(IGrabbable grabbable)
    {
        _grabbable = grabbable;
        _currentZAngle = ExtractZAngle(_grabbable.Transform.localRotation);
    }

    public void BeginTransform()
    {
        Pose handPose = _grabbable.GrabPoints[0];
        _prevHandFlatForward = FlattenToUpPlane(handPose.rotation);
    }

    public void UpdateTransform()
    {
        Pose handPose = _grabbable.GrabPoints[0];
        Vector3 currentFlat = FlattenToUpPlane(handPose.rotation);

        float deltaAngle = Vector3.SignedAngle(_prevHandFlatForward, currentFlat, Vector3.up);
        _currentZAngle += deltaAngle;
        _prevHandFlatForward = currentFlat;

        _grabbable.Transform.localRotation = Quaternion.Euler(baselineX, baselineY, _currentZAngle);
    }

    public void EndTransform()
    {
    }

    /// <summary>Flattens the hand's forward (or right, if forward is near-vertical) onto the horizontal plane, for measuring yaw.</summary>
    private static Vector3 FlattenToUpPlane(Quaternion handRotation)
    {
        Vector3 flat = Vector3.ProjectOnPlane(handRotation * Vector3.forward, Vector3.up);
        if (flat.sqrMagnitude < 0.0001f)
        {
            flat = Vector3.ProjectOnPlane(handRotation * Vector3.right, Vector3.up);
        }
        return flat.normalized;
    }

    /// <summary>Swing-twist extraction, so if the object starts pre-rotated its Z spin isn't lost on grab.</summary>
    private static float ExtractZAngle(Quaternion localRotation)
    {
        Vector3 imaginary = new Vector3(localRotation.x, localRotation.y, localRotation.z);
        Vector3 projected = Vector3.Project(imaginary, Vector3.forward);
        Quaternion twist = new Quaternion(projected.x, projected.y, projected.z, localRotation.w);

        float magnitude = Mathf.Sqrt(twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
        if (magnitude < 1e-6f) return 0f;

        twist = new Quaternion(twist.x / magnitude, twist.y / magnitude, twist.z / magnitude, twist.w / magnitude);
        twist.ToAngleAxis(out float angle, out Vector3 axis);
        if (Vector3.Dot(axis, Vector3.forward) < 0f) angle = -angle;
        return angle;
    }
}
