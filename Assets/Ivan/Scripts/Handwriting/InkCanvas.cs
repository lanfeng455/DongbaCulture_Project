using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

// Self-drawing handwriting surface for UI Toolkit.
//
// Collects pointer strokes (mouse / touch through the unified Pointer events)
// and renders them with Painter2D. Stored points are flipped to a math-style
// coordinate frame (origin bottom-left, y up) so template data is human
// readable in the JSON library. The recognizer normalizes everything anyway,
// but consistent storage keeps future tooling (preview / CNN export) simple.
public class InkCanvas : VisualElement
{
    // Minimum pointer travel (px) before a point is recorded — filters out
    // jitter noise so tiny tremors don't densify strokes.
    private const float k_minPointDistance = 1.5f;

    private readonly List<List<Vector2>> m_strokes = new List<List<Vector2>>();
    private readonly HashSet<int> m_activePointers = new HashSet<int>();

    public event Action StrokeStarted;
    public event Action StrokeEnded;

    public bool HasInk
    {
        get
        {
            foreach (List<Vector2> stroke in m_strokes)
            {
                if (stroke != null && stroke.Count >= 2)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public InkCanvas()
    {
        pickingMode = PickingMode.Position;
        generateVisualContent += DrawInk;

        RegisterCallback<PointerDownEvent>(HandlePointerDown, TrickleDown.TrickleDown);
        RegisterCallback<PointerMoveEvent>(HandlePointerMove);
        RegisterCallback<PointerUpEvent>(HandlePointerUp);
        RegisterCallback<PointerCancelEvent>(HandlePointerCancel);
        RegisterCallback<PointerLeaveEvent>(HandlePointerLeave);
    }

    // Deep copy so callers can't mutate canvas state through the snapshot
    public List<List<Vector2>> GetSnapshot()
    {
        List<List<Vector2>> snapshot = new List<List<Vector2>>(m_strokes.Count);
        foreach (List<Vector2> stroke in m_strokes)
        {
            if (stroke != null && stroke.Count >= 2)
            {
                snapshot.Add(new List<Vector2>(stroke));
            }
        }

        return snapshot;
    }

    // Named ClearInk instead of Clear: the base VisualElement.Clear() removes
    // child elements — an easy trap for callers expecting it to erase strokes
    public void ClearInk()
    {
        m_strokes.Clear();
        m_activePointers.Clear();
        MarkDirtyRepaint();
        StrokeEnded?.Invoke();
    }

    public void UndoLastStroke()
    {
        if (m_strokes.Count > 0)
        {
            m_strokes.RemoveAt(m_strokes.Count - 1);
            MarkDirtyRepaint();
            StrokeEnded?.Invoke();
        }
    }

    private void HandlePointerDown(PointerDownEvent e)
    {
        // New stroke begins regardless of previous ones — multi-stroke glyphs
        // (most Dongba glyphs are) need per-pen-down segmentation.
        this.CapturePointer(e.pointerId);
        m_activePointers.Add(e.pointerId);

        m_strokes.Add(new List<Vector2> { FlipY(e.localPosition) });
        MarkDirtyRepaint();
        StrokeStarted?.Invoke();
        e.StopPropagation();
    }

    private void HandlePointerMove(PointerMoveEvent e)
    {
        if (!m_activePointers.Contains(e.pointerId) || m_strokes.Count == 0)
        {
            return;
        }

        List<Vector2> current = m_strokes[m_strokes.Count - 1];
        Vector2 point = FlipY(e.localPosition);

        if (Vector2.Distance(point, current[current.Count - 1]) < k_minPointDistance)
        {
            return;
        }

        current.Add(point);
        MarkDirtyRepaint();
        e.StopPropagation();
    }

    private void HandlePointerUp(PointerUpEvent e)
    {
        EndStroke(e.pointerId);
        e.StopPropagation();
    }

    // Cancel shares the "pen left / input broken" semantics of pointer-up
    private void HandlePointerCancel(PointerCancelEvent e)
    {
        EndStroke(e.pointerId);
        e.StopPropagation();
    }

    private void EndStroke(int pointerId)
    {
        if (!m_activePointers.Remove(pointerId))
        {
            return;
        }

        this.ReleasePointer(pointerId);
        TrimDustStroke();
        StrokeEnded?.Invoke();
    }

    // Without capture, leaving the element mid-stroke would end the stroke
    // silently; with capture this only fires after release — still safe here.
    private void HandlePointerLeave(PointerLeaveEvent e)
    {
        if (!m_activePointers.Contains(e.pointerId))
        {
            return;
        }

        m_activePointers.Remove(e.pointerId);
        this.ReleasePointer(e.pointerId);
        TrimDustStroke();
        StrokeEnded?.Invoke();
    }

    // Drops a stroke that never grew past one point (an accidental click)
    private void TrimDustStroke()
    {
        if (m_strokes.Count > 0 && m_strokes[m_strokes.Count - 1].Count < 2)
        {
            m_strokes.RemoveAt(m_strokes.Count - 1);
            MarkDirtyRepaint();
        }
    }

    // UI Toolkit layout space has y pointing down; flip for storage so the
    // saved ink matches screen intuition (up = +y).
    private Vector2 FlipY(Vector2 localPosition)
    {
        return new Vector2(localPosition.x, contentRect.height - localPosition.y);
    }

    private void DrawInk(MeshGenerationContext context)
    {
        if (m_strokes.Count == 0)
        {
            return;
        }

        Painter2D painter = context.painter2D;
        painter.strokeColor = new Color(0.16f, 0.13f, 0.10f); // ink brown-black
        painter.lineWidth = 6f;
        painter.lineCap = LineCap.Round;
        painter.lineJoin = LineJoin.Round;

        foreach (List<Vector2> stroke in m_strokes)
        {
            if (stroke == null || stroke.Count < 2)
            {
                continue; // single dot has no path to stroke
            }

            painter.BeginPath();
            painter.MoveTo(FlipY(stroke[0]));
            for (int i = 1; i < stroke.Count; i++)
            {
                painter.LineTo(FlipY(stroke[i]));
            }

            painter.Stroke();
        }
    }
}
