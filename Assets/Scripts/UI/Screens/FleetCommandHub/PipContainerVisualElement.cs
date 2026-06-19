using UnityEngine;
using UnityEngine.UIElements;

public enum PipColour
{
    Cyan,
    Orange,
}

[UxmlElement("PipContainer")] 
public partial class PipContainerVisualElement : VisualElement
{
    private int m_Value;
    private int m_MaxPips;
    private int m_GroupSize = 5;
    private PipColour m_Colour = PipColour.Cyan;

    [UxmlAttribute]
    public int Value
    {
        get => m_Value;
        set { 
            m_Value = Mathf.Clamp(value, 0, m_MaxPips); 
            Rebuild(); 
        }
    }

    [UxmlAttribute]
    public int MaxPips
    {
        get => m_MaxPips;
        set { 
            m_MaxPips = Mathf.Max(0, value); 
            Rebuild(); 
        }
    }

    [UxmlAttribute]
    public int GroupSize
    {
        get => m_GroupSize;
        set
        {
            m_GroupSize = Mathf.Max(1, value);
            Rebuild();
        }
    }

    [UxmlAttribute]
    public PipColour Colour
    {
        get => m_Colour;
        set
        {
            m_Colour = value;
            Rebuild();
        }
    }

    public PipContainerVisualElement()
    {
        AddToClassList("pip-container");
        Rebuild();
    }

    private void Rebuild()
    {
        Clear(); // Remove old pips

        RemoveFromClassList("colour-cyan");
        RemoveFromClassList("colour-orange");

        switch (m_Colour)
        {
            case PipColour.Cyan: AddToClassList("colour-cyan"); break;
            case PipColour.Orange: AddToClassList("colour-orange"); break;
        }

        VisualElement currentGroup = null;
        for (int i = 0; i < m_MaxPips; i++)
        {
            if (i % m_GroupSize == 0)
            {
                currentGroup = new VisualElement();
                currentGroup.style.flexDirection = FlexDirection.Row;

                currentGroup.style.marginRight = 4f;
                currentGroup.style.marginLeft = 4f;

                //currentGroup.style.marginBottom = 6f;

                Add(currentGroup);
            }

            var pip = new VisualElement();
            pip.AddToClassList("pip");

            if (i < m_Value)
            {
                pip.AddToClassList("pip--active");
            }
            else
            {
                pip.AddToClassList("pip--inactive");
            }

            currentGroup.Add(pip);
        }
    }
}