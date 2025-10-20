using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Grasshopper.GUI;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Attributes;
using Rhino;

namespace ScriptParasite;

public class ParasiteAttributes : GH_ComponentAttributes
{
    private IGH_Component TargetComponent => OwnerParasite.TargetComponent;
    private bool _isDragging = false;
    private PointF _dragPoint;
    private IGH_Component _hoverComponent;
    private bool _hoveringCircle = false;
    private float _circleRadius = 4f; // Larger like GH dots

    public ParasiteAttributes(IGH_Component owner) : base(owner)
    {
        if (owner is not IParasiteComponent)
            throw new ArgumentException("Owner must implement IParasiteComponent");
    }
    
    protected override void Layout()
    {
        base.Layout();
        BaseBounds = Bounds;
        // Add space at top for our connection circle
        var bounds = Bounds;
        bounds.Height += 5; // Extra space for larger dot
        bounds.Y -= 5; // Move up to accommodate dot
        AdjustedBounds = bounds;
        Bounds = AdjustedBounds;
    }

    public RectangleF AdjustedBounds { get; set; }

    public RectangleF BaseBounds { get; set; }

    protected override void Render(GH_Canvas canvas, Graphics graphics, GH_CanvasChannel channel)
    {
        Bounds = BaseBounds;
        base.Render(canvas, graphics, channel);
        Bounds = AdjustedBounds;
        
        switch (channel)
        {
            case GH_CanvasChannel.Objects:
            {
                // Draw Grasshopper-style connection dot at top center
                var circleCenter = GetConnectionPoint();
                DrawGrasshopperDot(graphics, circleCenter);
                break;
            }
            case GH_CanvasChannel.Wires:
            {
                // Draw the dragging wire or connected arrow
                if (_isDragging || TargetComponent != null)
                {
                    var startPoint = GetConnectionPoint();
                    var endPoint = _isDragging ? _dragPoint : GetTargetEdgePoint(TargetComponent);

                    var wireColor = _isDragging ? Color.Red : Color.Orange;
                
                    using (var pen = new Pen(Color.FromArgb(150, wireColor), 2.5f))
                    {
                        DrawWire(graphics, pen, startPoint, endPoint);
                    }
                
                    // Highlight hovered component during drag
                    if (_isDragging && _hoverComponent != null && OwnerParasite.IsValid(_hoverComponent))
                    {
                        var bounds = _hoverComponent.Attributes.Bounds;
                        using var highlightPen = new Pen(Color.Red, 4);
                        graphics.DrawRectangle(highlightPen, Rectangle.Round(bounds));
                    }
                }
                break;
            }
        }
    }
    
    private void DrawGrasshopperDot(Graphics g, PointF center)
    {
        var circleRect = new RectangleF(
            center.X - _circleRadius, 
            center.Y - _circleRadius, 
            _circleRadius * 2, 
            _circleRadius * 2);
        
        // Create gradient for half-sphere effect
        using (var path = new GraphicsPath())
        {
            path.AddEllipse(circleRect);
            
            using (var gradientBrush = new PathGradientBrush(path))
            {
                gradientBrush.CenterPoint = new PointF(center.X - 2, center.Y - 2); // Offset for 3D effect
                gradientBrush.CenterColor = _hoveringCircle ? Color.White : Color.White;
                gradientBrush.SurroundColors = new[] { 
                    _hoveringCircle ? Color.White : Color.White 
                };
                
                g.FillEllipse(gradientBrush, circleRect);
            }
        }
        
        // Black outline like Grasshopper dots
        using (var pen = new Pen(_isDragging ? Color.Red : Color.Black, 2f))
        {
            g.DrawEllipse(pen, circleRect);
        }
        
        // Small highlight for extra 3D effect
        var highlightRect = new RectangleF(
            center.X - _circleRadius * 0.4f,
            center.Y - _circleRadius * 0.4f,
            _circleRadius * 0.6f,
            _circleRadius * 0.6f);
            
        using (var brush = new SolidBrush(Color.FromArgb(120, Color.White)))
        {
            g.FillEllipse(brush, highlightRect);
        }
    }
    
    private PointF GetConnectionPoint()
    {
        // Dot at top center of component
        var bounds = Bounds;
        return new PointF(bounds.X + bounds.Width / 2, bounds.Y + 5);
    }
    
    private PointF GetTargetEdgePoint(IGH_Component targetComponent)
    {
        if (targetComponent == null) return PointF.Empty;
        
        var startPoint = GetConnectionPoint();
        var targetBounds = targetComponent.Attributes.Bounds;
        var targetCenter = new PointF(
            targetBounds.X + targetBounds.Width / 2,
            targetBounds.Y + targetBounds.Height / 2);
        
        // Calculate intersection with target component's edge
        return GetRectangleEdgeIntersection(startPoint, targetCenter, targetBounds);
    }
    
    private PointF GetRectangleEdgeIntersection(PointF start, PointF center, RectangleF rect)
    {
        // Direction vector from start to center
        var dx = center.X - start.X;
        var dy = center.Y - start.Y;
        
        if (Math.Abs(dx) < 0.001f && Math.Abs(dy) < 0.001f)
            return center;
        
        // Normalize direction
        var length = (float)Math.Sqrt(dx * dx + dy * dy);
        dx /= length;
        dy /= length;
        
        // Calculate intersections with all four edges
        var intersections = new List<PointF>();
        
        // Top edge
        if (Math.Abs(dy) > 0.001f)
        {
            var t = (rect.Top - start.Y) / dy;
            var x = start.X + t * dx;
            if (x >= rect.Left && x <= rect.Right && t > 0)
                intersections.Add(new PointF(x, rect.Top));
        }
        
        // Bottom edge
        if (Math.Abs(dy) > 0.001f)
        {
            var t = (rect.Bottom - start.Y) / dy;
            var x = start.X + t * dx;
            if (x >= rect.Left && x <= rect.Right && t > 0)
                intersections.Add(new PointF(x, rect.Bottom));
        }
        
        // Left edge
        if (Math.Abs(dx) > 0.001f)
        {
            var t = (rect.Left - start.X) / dx;
            var y = start.Y + t * dy;
            if (y >= rect.Top && y <= rect.Bottom && t > 0)
                intersections.Add(new PointF(rect.Left, y));
        }
        
        // Right edge
        if (Math.Abs(dx) > 0.001f)
        {
            var t = (rect.Right - start.X) / dx;
            var y = start.Y + t * dy;
            if (y >= rect.Top && y <= rect.Bottom && t > 0)
                intersections.Add(new PointF(rect.Right, y));
        }
        
        // Return the closest intersection
        if (intersections.Count > 0)
        {
            var closest = intersections[0];
            var minDist = DistanceSquared(start, closest);
            
            foreach (var intersection in intersections)
            {
                var dist = DistanceSquared(start, intersection);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = intersection;
                }
            }
            return closest;
        }
        
        return center; // Fallback
    }
    
    private float DistanceSquared(PointF a, PointF b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
    
    public override GH_ObjectResponse RespondToMouseDown(GH_Canvas sender, GH_CanvasMouseEvent e)
    {
        if (e.Button == MouseButtons.Left)
        {
            var connectionPoint = GetConnectionPoint();
            
            // Check if click is on the connection circle
            if (IsPointInCircle(e.CanvasLocation, connectionPoint, _circleRadius))
            {
                _isDragging = true;
                _dragPoint = e.CanvasLocation;
                
                sender.Refresh();
                return GH_ObjectResponse.Capture;
            }
        }
        return base.RespondToMouseDown(sender, e);
    }
    
    public override GH_ObjectResponse RespondToMouseMove(GH_Canvas sender, GH_CanvasMouseEvent e)
    {
        if (_isDragging)
        {
            _dragPoint = e.CanvasLocation;
            _hoverComponent = FindComponentUnderPoint(e.CanvasLocation);
            sender.Refresh();
            return GH_ObjectResponse.Handled;
        }
        else
        {
            // Check if hovering over connection circle
            var connectionPoint = GetConnectionPoint();
            var wasHovering = _hoveringCircle;
            _hoveringCircle = IsPointInCircle(e.CanvasLocation, connectionPoint, _circleRadius + 2);
            
            if (wasHovering != _hoveringCircle)
            {
                sender.Cursor = _hoveringCircle ? Cursors.Hand : Cursors.Default;
                sender.Refresh();
            }
        }
        return base.RespondToMouseMove(sender, e);
    }
    
    public override GH_ObjectResponse RespondToMouseUp(GH_Canvas sender, GH_CanvasMouseEvent e)
    {
        if (_isDragging)
        {
            _isDragging = false;
            
            // Connect to target component if found
            var targetComp = FindComponentUnderPoint(e.CanvasLocation);
            OnComponentConnected(targetComp);

            _hoverComponent = null;
            sender.Cursor = Cursors.Default;
            sender.Refresh();
            return GH_ObjectResponse.Release;
        }
        return base.RespondToMouseUp(sender, e);
    }
    
    private void DrawWire(Graphics g, Pen pen, PointF start, PointF end)
    {
        // Draw a smooth bezier curve like regular GH wires
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        
        var cp1 = start with { X = start.X + dx * 0.5f };
        var cp2 = end with { X = end.X - dx * 0.5f };
        
        using (var path = new GraphicsPath())
        {
            path.AddBezier(start, cp1, cp2, end);
            g.DrawPath(pen, path);
        }
        
        // Draw arrowhead at end
        DrawArrowHead(g, pen.Color, start, end);
    }
    
    private void DrawArrowHead(Graphics g, Color color, PointF start, PointF end)
    {
        var angle = Math.Atan2(end.Y - start.Y, end.X - start.X);
        var arrowLength = 8;
        var arrowAngle = Math.PI / 6;
        
        var x1 = end.X - arrowLength * Math.Cos(angle - arrowAngle);
        var y1 = end.Y - arrowLength * Math.Sin(angle - arrowAngle);
        var x2 = end.X - arrowLength * Math.Cos(angle + arrowAngle);
        var y2 = end.Y - arrowLength * Math.Sin(angle + arrowAngle);

        using var brush = new SolidBrush(Color.FromArgb(180, color));
        var arrowHead = new[] {
            end,
            new PointF((float)x1, (float)y1),
            new PointF((float)x2, (float)y2)
        };
        g.FillPolygon(brush, arrowHead);
    }
    
    private bool IsPointInCircle(PointF point, PointF center, float radius)
    {
        var dx = point.X - center.X;
        var dy = point.Y - center.Y;
        return (dx * dx + dy * dy) <= (radius * radius);
    }
    
    private IGH_Component FindComponentUnderPoint(PointF point)
    {
        var doc = Owner.OnPingDocument();
        if (doc == null) return null;
        
        foreach (var obj in doc.Objects)
        {
            if (obj is IGH_Component comp && comp != Owner)
            {
                if (comp.Attributes.Bounds.Contains(Point.Round(point)))
                {
                    return comp;
                }
            }
        }
        return null;
    }
    
    public IParasiteComponent OwnerParasite => (ScriptParasiteComponent)Owner;
    
    protected virtual void OnComponentConnected(IGH_Component targetComponent)
    {
        OwnerParasite.SetTarget(targetComponent);
    }
}