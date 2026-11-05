using System;
using System.Collections.Generic;

namespace Airships.Core
{
    public readonly struct HubPoint
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public HubPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    // The part of the screen the menus leave clear, as shares of its width and height.
    public readonly struct HubClearArea
    {
        public readonly float Left;
        public readonly float Right;
        public readonly float Bottom;
        public readonly float Top;

        public HubClearArea(float left, float right, float bottom, float top)
        {
            Left = left;
            Right = right;
            Bottom = bottom;
            Top = top;
        }
    }

    public struct HubFrame
    {
        public float CameraX;
        public float CameraY;
        public float CameraZ;
        public float LookX;
        public float LookY;
        public float LookZ;
        public bool Fits;
    }

    // Finds the closest camera in the hangar that shows everyone in the clear area, on any screen shape.
    public static class HubFraming
    {
        public const float ClearBottom = 0.12f; // Room for name tags under the crew's feet.
        public const float ClearTop = 0.84f; // Below the ship's name in the top corner.
        const float NearestDistance = 10f;
        const float FarthestDistance = 40f;
        const float DistanceStep = 0.25f;
        const float BaseHeight = 3f;
        const float HeightPerDistance = 0.3f; // Backing away also rises, so the rail never crosses the view.
        const int AimPasses = 12;

        public static HubFrame Fit(IReadOnlyList<HubPoint> points, float aspect, float verticalFov, HubClearArea area)
        {
            var frame = new HubFrame();
            if (points.Count == 0) return frame;

            var minX = float.MaxValue;
            var maxX = float.MinValue;
            foreach (var point in points)
            {
                minX = Math.Min(minX, point.X);
                maxX = Math.Max(maxX, point.X);
            }
            var cameraX = (minX + maxX) * 0.5f;
            var tanHalf = (float)Math.Tan(verticalFov * 0.5f * Math.PI / 180.0);

            for (var distance = NearestDistance; distance <= FarthestDistance; distance += DistanceStep)
            {
                var height = BaseHeight + (distance - NearestDistance) * HeightPerDistance;
                frame = Aim(points, cameraX, height, -distance, aspect, tanHalf, area);
                if (frame.Fits) return frame;
            }
            return frame;
        }

        // Turns the camera until the crew are centered in the clear area, then checks they all fit inside it.
        static HubFrame Aim(IReadOnlyList<HubPoint> points, float x, float y, float z, float aspect, float tanHalf, HubClearArea area)
        {
            var yaw = 0f;
            var pitch = 0.2f;
            var wide = tanHalf * aspect;
            var bounds = default(ScreenBounds);
            for (var pass = 0; pass < AimPasses; pass++)
            {
                bounds = Measure(points, x, y, z, yaw, pitch, wide, tanHalf);
                var centerX = (area.Left + area.Right) - 1f;
                var centerY = (area.Bottom + area.Top) - 1f;
                yaw += (float)Math.Atan(((bounds.MinX + bounds.MaxX) * 0.5f - centerX) * wide);
                pitch -= (float)Math.Atan(((bounds.MinY + bounds.MaxY) * 0.5f - centerY) * tanHalf);
            }
            bounds = Measure(points, x, y, z, yaw, pitch, wide, tanHalf);
            var forwardX = (float)(Math.Sin(yaw) * Math.Cos(pitch));
            var forwardY = (float)-Math.Sin(pitch);
            var forwardZ = (float)(Math.Cos(yaw) * Math.Cos(pitch));
            return new HubFrame
            {
                CameraX = x,
                CameraY = y,
                CameraZ = z,
                LookX = x + forwardX * 10f,
                LookY = y + forwardY * 10f,
                LookZ = z + forwardZ * 10f,
                Fits = bounds.InFront
                    && bounds.MinX >= area.Left * 2f - 1f && bounds.MaxX <= area.Right * 2f - 1f
                    && bounds.MinY >= area.Bottom * 2f - 1f && bounds.MaxY <= area.Top * 2f - 1f,
            };
        }

        struct ScreenBounds
        {
            public float MinX;
            public float MaxX;
            public float MinY;
            public float MaxY;
            public bool InFront;
        }

        // Where the points land on screen, from -1 to 1 across each axis.
        static ScreenBounds Measure(IReadOnlyList<HubPoint> points, float x, float y, float z, float yaw, float pitch, float wide, float tall)
        {
            var bounds = new ScreenBounds { MinX = float.MaxValue, MaxX = float.MinValue, MinY = float.MaxValue, MaxY = float.MinValue, InFront = true };
            var sinYaw = (float)Math.Sin(yaw);
            var cosYaw = (float)Math.Cos(yaw);
            var sinPitch = (float)Math.Sin(pitch);
            var cosPitch = (float)Math.Cos(pitch);
            foreach (var point in points)
            {
                var dx = point.X - x;
                var dy = point.Y - y;
                var dz = point.Z - z;
                var side = dx * cosYaw - dz * sinYaw;
                var flat = dx * sinYaw + dz * cosYaw;
                var depth = flat * cosPitch - dy * sinPitch;
                var up = flat * sinPitch + dy * cosPitch;
                if (depth <= 0.1f)
                {
                    bounds.InFront = false;
                    continue;
                }
                var screenX = side / depth / wide;
                var screenY = up / depth / tall;
                bounds.MinX = Math.Min(bounds.MinX, screenX);
                bounds.MaxX = Math.Max(bounds.MaxX, screenX);
                bounds.MinY = Math.Min(bounds.MinY, screenY);
                bounds.MaxY = Math.Max(bounds.MaxY, screenY);
            }
            return bounds;
        }
    }
}
