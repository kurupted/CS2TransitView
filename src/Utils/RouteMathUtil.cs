using Colossal.Mathematics;
using System;
using Unity.Mathematics;

namespace BetterTransitView.Utils
{
    // A unique identifier for a specific segment of geometry to be drawn.
    // We use this as a Key in a HashMap to count how many agents are on the same segment.
    public struct CurveDef : IEquatable<CurveDef>
    {
        public Bezier4x3 curve;
        public byte type; // 4 = Vehicle, 2 = Pedestrian

        public CurveDef(Bezier4x3 curve, byte type)
        {
            this.curve = curve;
            this.type = type;
        }

        public bool Equals(CurveDef other)
        {
            // Manual comparison to ensure Burst compatibility
            return curve.a.Equals(other.curve.a) &&
                   curve.b.Equals(other.curve.b) &&
                   curve.c.Equals(other.curve.c) &&
                   curve.d.Equals(other.curve.d) &&
                   type == other.type;
        }

        public override int GetHashCode()
        {
            // Manual hash combination of the 4 control points + type
            // using Unity.Mathematics.math.hash to act on float3 directly
            uint h = 0;
            h = math.hash(curve.a);
            h = math.hash(curve.b) ^ h; // XOR mix
            h = math.hash(curve.c) ^ h;
            h = math.hash(curve.d) ^ h;
            
            // Mix in the type
            return (int)(h ^ type);
        }
    }

    public static class MathUtils
    {
        // Cuts the curve to return the segment from t to 1.0
        public static Bezier4x3 Cut(Bezier4x3 b, float t)
        {
            // Standard De Casteljau's algorithm to split curve at t
            // We only care about the right side (t -> 1)
            float3 q0 = math.lerp(b.a, b.b, t);
            float3 q1 = math.lerp(b.b, b.c, t);
            float3 q2 = math.lerp(b.c, b.d, t);
            float3 r0 = math.lerp(q0, q1, t);
            float3 r1 = math.lerp(q1, q2, t);
            float3 s = math.lerp(r0, r1, t); // The point at parameter t

            // Return segment from s to end (b.d)
            return new Bezier4x3(s, r1, q2, b.d);
        }

        // Cuts the curve to return the segment from range.x to range.y
        public static Bezier4x3 Cut(Bezier4x3 b, float2 range)
        {
            // 1. Cut from range.x to 1.0
            Bezier4x3 startToEnd = Cut(b, range.x);

            // 2. Map range.y to the new parameter space of startToEnd
            float denominator = 1f - range.x;
            if (denominator < 0.0001f) return startToEnd; // Prevent divide by zero

            float newEndT = (range.y - range.x) / denominator;

            // 3. Cut from 0 to newEndT (keep left side)
            return CutFromZero(startToEnd, newEndT);
        }

        private static Bezier4x3 CutFromZero(Bezier4x3 b, float t)
        {
            float3 q0 = math.lerp(b.a, b.b, t);
            float3 q1 = math.lerp(b.b, b.c, t);
            float3 q2 = math.lerp(b.c, b.d, t);
            float3 r0 = math.lerp(q0, q1, t);
            float3 r1 = math.lerp(q1, q2, t);
            float3 s = math.lerp(r0, r1, t);

            return new Bezier4x3(b.a, q0, r0, s);
        }
    }
}