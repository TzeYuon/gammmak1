using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

public partial class WeaponMountElement : VisualElement
{
    // Data
    private WeaponMount mountData;
    private float facingAngleDegrees;
    private float arcLimitDegrees;

    // State
    private Label label;
    private PipContainerVisualElement pipContainer;

    public WeaponMountElement()
    {
        AddToClassList("weapon-mount");

        pipContainer = new();
        pipContainer.Colour = PipColour.Orange;
        pipContainer.MaxPips = 0;
        pipContainer.Value = 0;
        pipContainer.style.position = Position.Absolute;
        pipContainer.style.maxWidth = 60;
        pipContainer.style.top = 12;
        Add(pipContainer);

        // Create label for weapon name
        label = new Label();
        label.AddToClassList("weapon-mount-text");
        Add(label);

        generateVisualContent += DrawTacticalArc;
    }

    public void Init(WeaponMount mount)
    {
        mountData = mount;
        arcLimitDegrees = mount.MaxAngle <= 0 ? 2f : mount.MaxAngle;
        Vector3 dir = mount.OriginalRotation * Vector3.up;
        facingAngleDegrees = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;

        Rebuild();
        MarkDirtyRepaint();
    }

    public void Rebuild()
    {
        if (mountData != null && mountData.EquippedWeapon != null)
        {
            label.text = mountData.EquippedWeapon.AbbrWpnName;
            pipContainer.MaxPips = mountData.EquippedWeapon.EquipCost;
            pipContainer.Value = pipContainer.MaxPips;
        }
        else
        {
            label.text = "";
            pipContainer.MaxPips = 0;
            pipContainer.Value = 0;
        }
    }

    private void DrawTacticalArc(MeshGenerationContext context)
    {
        var painter = context.painter2D;
        if (painter == null) return;

        Vector2 center = new Vector2(35, 35);
        float innerRadius = 35f;
        float outerRadius = 45f;
        float meanRadius = (innerRadius + outerRadius) * 0.5f;
        float donutThickness = outerRadius - innerRadius;

        bool isEmpty = mountData == null || mountData.EquippedWeapon == null;
        Color mainColor = Color.yellow;
        Color arcFillColor = new(mainColor.r, mainColor.g, mainColor.b, 0.3f);

        // Draw firing arc
        if (arcLimitDegrees >= 179.9f)
        {
            painter.BeginPath();
            painter.Arc(center, meanRadius, 0f, 360f);
            painter.strokeColor = arcFillColor;
            painter.lineWidth = donutThickness;
            painter.Stroke();

            painter.BeginPath();
            painter.Arc(center, outerRadius, 0f, 360f);
            painter.strokeColor = mainColor;
            painter.lineWidth = 1f;
            painter.Stroke();

            painter.BeginPath();
            painter.Arc(center, innerRadius, 0f, 360f);
            painter.strokeColor = mainColor;
            painter.lineWidth = 1f;
            painter.Stroke();
        }
        else
        {
            float startAngle = facingAngleDegrees - arcLimitDegrees;
            float endAngle = facingAngleDegrees + arcLimitDegrees;

            painter.BeginPath();
            painter.Arc(center, meanRadius, startAngle, endAngle);
            painter.strokeColor = arcFillColor;
            painter.lineWidth = donutThickness;
            painter.Stroke();

            painter.BeginPath();
            painter.Arc(center, outerRadius, startAngle, endAngle);
            painter.strokeColor = mainColor;
            painter.lineWidth = 1f;
            painter.Stroke();

            painter.BeginPath();
            painter.Arc(center, innerRadius, startAngle, endAngle);
            painter.strokeColor = mainColor;
            painter.lineWidth = 1f;
            painter.Stroke();

            DrawSideCap(painter, center, innerRadius, outerRadius, startAngle);
            DrawSideCap(painter, center, innerRadius, outerRadius, endAngle);
        }

        float angleRad = facingAngleDegrees * Mathf.Deg2Rad;
        float startX = center.x + Mathf.Cos(angleRad) * innerRadius;
        float endX = center.x + Mathf.Cos(angleRad) * outerRadius;
        float startY = center.y + Mathf.Sin(angleRad) * innerRadius;
        float endY = center.y + Mathf.Sin(angleRad) * outerRadius;
        Vector2 endPoint = new(endX, endY);

        painter.BeginPath();
        painter.MoveTo(new Vector2(startX, startY));
        painter.LineTo(endPoint);

        painter.strokeColor = mainColor;
        painter.lineWidth = 4f;
        painter.Stroke();

        void DrawSideCap(Painter2D painter, Vector2 center, float inner, float outer, float angleDeg)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector2 startPoint = center + new Vector2(Mathf.Cos(rad) * inner, Mathf.Sin(rad) * inner);
            Vector2 endPoint = center + new Vector2(Mathf.Cos(rad) * outer, Mathf.Sin(rad) * outer);

            painter.BeginPath();
            painter.MoveTo(startPoint);
            painter.LineTo(endPoint);
            painter.lineWidth = 1f;
            painter.Stroke();
        }
    }
}
