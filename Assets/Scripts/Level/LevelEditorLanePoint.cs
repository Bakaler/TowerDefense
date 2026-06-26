using UnityEngine;

/// <summary>
/// One point on a lane placement zone. Width controls the ribbon thickness at this point;
/// the zone interpolates width smoothly between adjacent points.
/// </summary>
public class LevelEditorLanePoint : MonoBehaviour
{
    public float width = 3f;

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.55f, 0.5f);
        DrawCircleWire(transform.position, width * 0.5f, 24);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0.3f, 0.9f);
        DrawCircleWire(transform.position, width * 0.5f, 24);
        Gizmos.DrawSphere(transform.position, 0.12f);
    }

    static void DrawCircleWire(Vector3 c, float r, int seg)
    {
        Vector3 prev = c + new Vector3(r, 0f);
        for (int i = 1; i <= seg; i++)
        {
            float a  = (float)i / seg * Mathf.PI * 2f;
            var   pt = c + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            Gizmos.DrawLine(prev, pt);
            prev = pt;
        }
    }
}
