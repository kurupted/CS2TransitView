using System;
using System.Linq;
using System.Reflection;
using Game.Simulation;
using Unity.Mathematics;

namespace BetterTransitView.Utils
{
    internal static class Time2WorkInterop
    {
        private static bool s_Checked;
        private static float s_Factor = 1f;

        public static float GetTimeFactor()
        {
            if (s_Checked)
                return s_Factor;

            s_Checked = true;
            s_Factor = 1f;

            try
            {
                Type type = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a != null)
                    .SelectMany(a =>
                    {
                        try { return a.GetTypes(); }
                        catch (ReflectionTypeLoadException e) { return e.Types?.Where(t => t != null) ?? Enumerable.Empty<Type>(); }
                        catch { return Enumerable.Empty<Type>(); }
                    })
                    .FirstOrDefault(t => t != null && t.FullName == "Time2Work.Time2WorkTimeSystem");

                if (type == null)
                    return s_Factor;

                FieldInfo factorField = type.GetField("timeReductionFactor", BindingFlags.Public | BindingFlags.Static);
                if (factorField?.GetValue(null) is float factor && factor > 0f)
                {
                    s_Factor = factor;
                    return s_Factor;
                }

                FieldInfo ticksField = type.GetField("kTicksPerDay", BindingFlags.Public | BindingFlags.Static);
                if (ticksField?.GetValue(null) is int ticksPerDay && ticksPerDay > 0)
                {
                    s_Factor = math.max(1f, ticksPerDay / (float)TimeSystem.kTicksPerDay);
                    return s_Factor;
                }
            }
            catch
            {
                s_Factor = 1f;
            }

            return s_Factor;
        }

        public static void InvalidateCache()
        {
            s_Checked = false;
        }
    }
}
