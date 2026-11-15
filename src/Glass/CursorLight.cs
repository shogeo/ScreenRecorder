// Visual renderer adapted from NLsalfetka/launcher/CursorLight.cs; no loader or backend code.
using System;
using System.Drawing;

namespace Recording
{
    // A fixed scene light viewed through a tilted glass bevel. The mouse changes
    // the lens orientation; it does not carry a light or a painted hotspot.
    internal static class CursorLight
    {
        internal const float TrackingMargin = 28;
        internal const double PeakOpacity = .44;
        internal struct Reflection { internal double Edge, Transmission; }
        internal struct Lighting
        { internal double LX, LY, LZ, VX, VY, VZ, HX, HY, HZ, TX, TY, TZ; }
        internal static Lighting ForTilt(double tiltX, double tiltY)
        {
            double rz = -tiltX * .95 * Math.PI / 180, ry = -tiltX * .24, rx = -tiltY * .24;
            double lx = -.48, ly = -.56, lz = .675;
            double length = Math.Sqrt(lx * lx + ly * ly + lz * lz); lx /= length; ly /= length; lz /= length;
            double vx = 0, vy = 0, vz = 1;
            Rotate(ref lx, ref ly, ref lz, rx, ry, rz); Rotate(ref vx, ref vy, ref vz, rx, ry, rz);
            double hx = lx + vx, hy = ly + vy, hz = lz + vz;
            length = Math.Sqrt(hx * hx + hy * hy + hz * hz);
            const double eta = 1 / 1.46;
            // Snell refraction through the front plane; projected rays illuminate
            // the inner edge on the far side of the lens.
            return new Lighting { LX = lx, LY = ly, LZ = lz, VX = vx, VY = vy, VZ = vz,
                HX = hx / length, HY = hy / length, HZ = hz / length, TX = -eta * lx, TY = -eta * ly,
                TZ = -Math.Sqrt(Math.Max(0, 1 - eta * eta * (1 - lz * lz))) };
        }
        internal static Reflection Evaluate(double distance, double edgeX, double edgeY, Lighting light)
        {
            if (distance < -4 || distance > 1) return new Reflection();
            double bevel = Math.Max(0, Math.Min(1, 1 + distance / 3.2));
            double slope = bevel * bevel * 2.8;
            double nx = edgeX * slope, ny = edgeY * slope;
            double normalLength = Math.Sqrt(nx * nx + ny * ny + 1), nz = 1 / normalLength;
            nx /= normalLength; ny /= normalLength;
            double dotNL = Math.Max(0, nx * light.LX + ny * light.LY + nz * light.LZ);
            double dotNV = Math.Max(0, nx * light.VX + ny * light.VY + nz * light.VZ);
            double dotNH = Math.Max(0, nx * light.HX + ny * light.HY + nz * light.HZ);
            double square = dotNH * dotNH, fourth = square * square, eighth = fourth * fourth;
            double fresnel = .04 + .96 * Fifth(1 - dotNV);
            double edge = Math.Min(PeakOpacity, fresnel * eighth * eighth * (4 + 6 * dotNL));
            double planarLength = Math.Max(.0001, Math.Sqrt(light.TX * light.TX + light.TY * light.TY));
            double focus = Math.Max(0, (edgeX * light.TX + edgeY * light.TY) / planarLength);
            double transmission = .12 * focus * focus * (1 - fresnel) * -light.TZ;
            return new Reflection { Edge = edge, Transmission = transmission };
        }
        private static void Rotate(ref double x, ref double y, ref double z, double rx, double ry, double rz)
        {
            double nextX = x * Math.Cos(rz) - y * Math.Sin(rz), nextY = x * Math.Sin(rz) + y * Math.Cos(rz);
            x = nextX; y = nextY;
            nextX = x * Math.Cos(ry) + z * Math.Sin(ry); double nextZ = -x * Math.Sin(ry) + z * Math.Cos(ry);
            x = nextX; z = nextZ;
            nextY = y * Math.Cos(rx) - z * Math.Sin(rx); nextZ = y * Math.Sin(rx) + z * Math.Cos(rx);
            y = nextY; z = nextZ;
        }
        private static double Fifth(double value) { double square = value * value; return square * square * value; }
        internal static Rectangle Bounds(Point point, float scale)
        {
            int radius = (int)Math.Ceiling(TrackingMargin * scale) + 3;
            return new Rectangle(point.X - radius, point.Y - radius, radius * 2, radius * 2);
        }
    }
}
