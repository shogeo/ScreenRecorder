// Visual renderer adapted from NLsalfetka/launcher/GlassMotion.cs; no loader or backend code.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace Recording
{
    internal struct GlassPose
    {
        internal float Hover, X, Y, LightX, LightY, Press, Pulse, OriginX, OriginY;
        internal static GlassPose Rest { get { return new GlassPose { Pulse = -1, OriginX = .5f, OriginY = .5f }; } }
    }

    // One time-based spring model is shared by every glass control. Rendering
    // reads a snapshot: paint count never advances the animation.
    internal sealed class GlassMotion
    {
        private float hoverTarget, xTarget, yTarget, pressTarget;
        private float hoverVelocity, xVelocity, yVelocity, pressVelocity;
        private float pulseSeconds = -1;
        internal GlassPose Pose = GlassPose.Rest;
        internal void Point(float x, float y)
        {
            xTarget = Math.Max(-1, Math.Min(1, x)); yTarget = Math.Max(-1, Math.Min(1, y));
            // Light is attached to the pointer immediately; only geometry has inertia.
            Pose.LightX = xTarget; Pose.LightY = yTarget;
        }
        internal void Hover(bool inside)
        { hoverTarget = inside ? 1 : 0; if (inside) Pose.Hover = Math.Max(.35f, Pose.Hover); else { xTarget = yTarget = 0; pressTarget = 0; } }
        internal void Press(float x, float y)
        {
            pressTarget = 1;
            Pose.Press = Math.Max(Pose.Press, .24f); // Immediate contact, followed by spring compression.
            Pose.OriginX = Math.Max(0, Math.Min(1, x)); Pose.OriginY = Math.Max(0, Math.Min(1, y));
            pulseSeconds = 0; Pose.Pulse = 0;
        }
        internal void Release() { pressTarget = 0; }
        internal bool Advance(float seconds)
        {
            seconds = Math.Max(0, Math.Min(.05f, seconds));
            int steps = Math.Max(1, (int)Math.Ceiling(seconds / .008));
            float dt = seconds / steps;
            for (int step = 0; step < steps; step++)
            {
                Spring(ref Pose.Hover, ref hoverVelocity, hoverTarget, 170, 25, dt);
                Spring(ref Pose.X, ref xVelocity, xTarget, 210, 27, dt);
                Spring(ref Pose.Y, ref yVelocity, yTarget, 210, 27, dt);
                Spring(ref Pose.Press, ref pressVelocity, pressTarget, 460, 20, dt);
            }
            if (pulseSeconds >= 0)
            {
                pulseSeconds += seconds;
                if (pulseSeconds >= .72f) { pulseSeconds = -1; Pose.Pulse = -1; }
                else Pose.Pulse = pulseSeconds / .72f;
            }
            return pulseSeconds >= 0 || Moving(Pose.Hover, hoverVelocity, hoverTarget) ||
                Moving(Pose.X, xVelocity, xTarget) || Moving(Pose.Y, yVelocity, yTarget) || Moving(Pose.Press, pressVelocity, pressTarget);
        }
        private static bool Moving(float value, float velocity, float target)
        { return Math.Abs(value - target) > .0005f || Math.Abs(velocity) > .002f; }
        private static void Spring(ref float value, ref float velocity, float target, float stiffness, float damping, float dt)
        {
            velocity += ((target - value) * stiffness - velocity * damping) * dt; value += velocity * dt;
            if (!Moving(value, velocity, target)) { value = target; velocity = 0; }
        }
        internal void Reset()
        { Pose = GlassPose.Rest; hoverTarget = xTarget = yTarget = pressTarget = hoverVelocity = xVelocity = yVelocity = pressVelocity = 0; pulseSeconds = -1; }
        internal void SetForCheck(GlassPose pose)
        { Reset(); Pose = pose; Pose.LightX = pose.X; Pose.LightY = pose.Y; hoverTarget = pose.Hover; xTarget = pose.X; yTarget = pose.Y; pressTarget = Math.Max(0, pose.Press); }

        internal static Matrix Transform(RectangleF bounds, GlassPose pose)
        {
            float hover = Math.Max(0, Math.Min(1, pose.Hover));
            float press = Math.Max(-.28f, Math.Min(1.15f, pose.Press));
            double radians = pose.X * hover * .95 * Math.PI / 180;
            float sx = 1 - hover * .022f - press * .045f, sy = 1 - hover * .025f - press * .14f;
            float shear = -pose.Y * hover * .055f;
            float cos = (float)Math.Cos(radians), sin = (float)Math.Sin(radians);
            float a = sx * cos, b = sx * sin, c = sy * (shear * cos - sin), d = sy * (shear * sin + cos);
            float cx = bounds.X + bounds.Width / 2, cy = bounds.Y + bounds.Height / 2;
            return new Matrix(a, b, c, d, cx + pose.X * hover * .8f - a * cx - c * cy,
                cy + pose.Y * hover * .6f - b * cx - d * cy);
        }

    }
}
