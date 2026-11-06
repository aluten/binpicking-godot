using System.Collections.Generic;
using Godot;

/// <summary>
/// Measures the contact ring against the full mesh of one link, which is the truth in simulation.
/// Rays only see this link, so a ring that spans two links does not count as a seal.
/// </summary>
public static class CupContactProbe
{
    private const int RingPoints = 32;
    private const float RayMargin = 0.03f;
    private const uint ProbeLayer = 1u << 19;

    public static List<float?> MeasureRing(Node3D parent, Shape3D fullShape, Transform3D linkTransform, Vector3 point, Vector3 normal, float cupRadius)
    {
        var body = new StaticBody3D { Transform = linkTransform, CollisionLayer = ProbeLayer, CollisionMask = 0 };
        body.AddChild(new CollisionShape3D { Shape = fullShape });
        parent.AddChild(body);
        PhysicsServer3D.BodySetState(body.GetRid(), PhysicsServer3D.BodyState.Transform, linkTransform);

        Vector3 helper = Mathf.Abs(normal.Dot(Vector3.Forward)) > 0.9f ? Vector3.Right : Vector3.Forward;
        Vector3 axisA = helper.Cross(normal).Normalized();
        Vector3 axisB = normal.Cross(axisA).Normalized();
        PhysicsDirectSpaceState3D space = parent.GetWorld3D().DirectSpaceState;

        var distances = new List<float?>(RingPoints);
        for (int i = 0; i < RingPoints; i++)
        {
            float angle = i * Mathf.Tau / RingPoints;
            Vector3 ringPoint = point + (axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)) * cupRadius;
            var query = PhysicsRayQueryParameters3D.Create(ringPoint + normal * RayMargin, ringPoint - normal * RayMargin, ProbeLayer);
            query.HitBackFaces = false;
            var hit = space.IntersectRay(query);
            if (hit.Count == 0)
            {
                distances.Add(null);
            }
            else
            {
                distances.Add((hit["position"].AsVector3() - ringPoint).Dot(normal));
            }
        }

        parent.RemoveChild(body);
        body.Free();
        return distances;
    }
}
