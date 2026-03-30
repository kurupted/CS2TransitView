using Colossal.Mathematics;
using Game.Rendering;
using BetterTransitView.Systems;
using BetterTransitView.Utils;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

namespace BetterTransitView.Jobs
{
    [BurstCompile]
    public struct RenderRouteOverlayJob : IJob
    {
        public SimpleOverlayRendererSystem.Buffer overlayBuffer;
        
        public float maxVehicleTraffic;
        public float maxPedestrianTraffic;
        
        public float alphaMultiplier;
        public bool isLaneDataOnly;

        [ReadOnly]
        public NativeArray<NativeHashMap<CurveDef, int>> curveData;

        // Helper struct for sorting
        private struct WeightedCurve
        {
            public CurveDef Curve;
            public int Weight;
        }

        // Comparer to sort ascending (Low Density -> High Density)
        private struct WeightedCurveComparer : IComparer<WeightedCurve>
        {
            public int Compare(WeightedCurve x, WeightedCurve y)
            {
                return x.Weight.CompareTo(y.Weight);
            }
        }

        public void Execute()
        {
            NativeHashMap<CurveDef, int> aggregatedCurves = new NativeHashMap<CurveDef, int>(1000, Allocator.Temp);

            // 1. Aggregate results from parallel batches
            for (int i = 0; i < curveData.Length; i++)
            {
                var batchMap = curveData[i];
                foreach (var kvp in batchMap)
                {
                    if (aggregatedCurves.ContainsKey(kvp.Key))
                    {
                        aggregatedCurves[kvp.Key] += kvp.Value;
                    }
                    else
                    {
                        aggregatedCurves.Add(kvp.Key, kvp.Value);
                    }
                }
            }

            // 2. Transfer to a list so we can sort them
            NativeList<WeightedCurve> sortedList = new NativeList<WeightedCurve>(aggregatedCurves.Count, Allocator.Temp);
            foreach (var kvp in aggregatedCurves)
            {
                sortedList.Add(new WeightedCurve { Curve = kvp.Key, Weight = kvp.Value });
            }

            // 3. Sort: Ascending order means Low traffic is drawn first, High traffic (Red) is drawn last (on top)
            sortedList.Sort(new WeightedCurveComparer());

            // 4. Draw
            foreach (var item in sortedList)
            {
                DrawWeightedCurve(item.Curve, item.Weight);
            }

            sortedList.Dispose();
            aggregatedCurves.Dispose();
        }

        private void DrawWeightedCurve(CurveDef curveDef, int weight)
        {
            float baseWidth = 0.9f; 
            float maxAdditionalWidth = 1.8f;
            float widthMultiplier = 0.15f;

            if (isLaneDataOnly)
            {
                baseWidth *= 0.5f;
                maxAdditionalWidth *= 0.5f;
                widthMultiplier *= 0.5f;
            }

            float width = baseWidth + math.min(weight * widthMultiplier, maxAdditionalWidth);
            float t = 0f;
            
            if (curveDef.type == 2) // Pedestrian
            {
                 t = math.clamp(weight / maxPedestrianTraffic, 0f, 1f);
                 width *= 0.75f;
            }
            else // Vehicle
            {
                 t = math.clamp(weight / maxVehicleTraffic, 0f, 1f);
            }

            float baseAlpha = alphaMultiplier; // e.g. 0.8

            // Cyan (Low Traffic): Fades out faster (0.7 * 0.8 = 0.56)
            Color cCyan = new Color(0f, 1f, 1f, 0.7f * baseAlpha);
        
            // Yellow (Med Traffic): Normal fade (0.8 * 0.8 = 0.64)
            Color cYellow = new Color(1f, 0.9f, 0f, 0.8f * baseAlpha);
        
            // Red (High Traffic): Stays strong! 
            // We clamp it so it never drops below 0.6 unless the slider is very low.
            float redAlpha = math.max(0.6f * baseAlpha, 0.95f * baseAlpha); 
            Color cRed = new Color(1f, 0.2f, 0f, redAlpha);

            Color color;
            
            if (t < 0.5f)
            {
                // Lerp Cyan -> Yellow
                float localT = t * 2.0f;
                color = Color.Lerp(cCyan, cYellow, localT);
            }
            else
            {
                // Lerp Yellow -> Red
                float localT = (t - 0.5f) * 2.0f;
                color = Color.Lerp(cYellow, cRed, localT);
            }

            overlayBuffer.DrawCurve(color, curveDef.curve, width, new float2(1, 1));
        }
    }
}