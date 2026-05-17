using UnityEngine;

public class ShipHitboxAuthoring : MonoBehaviour
{
    public HitboxShape Shape = HitboxShape.Circle;
    public Vector2 Size = Vector2.one;

    private void OnDrawGizmos()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.green;

        switch (Shape)
        {
            case HitboxShape.Circle:
                Gizmos.DrawWireSphere(Vector3.zero, Size.x);
                break;

            case HitboxShape.Rectangle:
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(Size.x * 2, Size.y * 2, 0));
                break;

            case HitboxShape.Triangle:
                Vector3 bottomLeft = new (-Size.x, 0, 0);
                Vector3 bottomRight = new (Size.x, 0, 0);
                Vector3 topTip = new (0, Size.y, 0);

                Gizmos.DrawLine(bottomLeft, bottomRight);
                Gizmos.DrawLine(bottomRight, topTip);
                Gizmos.DrawLine(topTip, bottomLeft);
                break;
        }
    }
}
