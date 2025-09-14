using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    public static class AngleConversions
    {

        /// <summary>
        /// Convert NaN into the default if it is in-fact NaN
        /// </summary>
        /// <param name="x"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public static double AlwaysANumber(this double x, double def=0)
        {
            if (double.IsNaN(x))
                return def;
            return x;
        }

        /// <summary>
        /// Convert NaN/Inf into the default if it is in-fact NaN or +-Inf
        /// </summary>
        /// <param name="x"></param>
        /// <param name="def"></param>
        /// <returns></returns>
        public static float AlwaysANumber(this float x, float def=0)
        {
            if (float.IsNaN(x))
                return def;
            if (float.IsInfinity(x) || float.IsNegativeInfinity(x))
                return def;
            return x;
        }

        /// <summary>
        /// Clamps a value, shorthand for AngleConversions.Modulo(x, divisor)
        /// C#'s % operator is remainder, so this supports negative numbers
        /// </summary>
        /// <param name="x"></param>
        /// <param name="divisor"></param>
        /// <returns></returns>
        public static double Modulo(this double x, double divisor)
        {
            double r = x % divisor;
            return (r < 0 ? r + divisor : r);//(x % divisor + divisor) % divisor;
        }

        /// <summary>
        /// Clamps a value, shorthand for MathHelper.Clamp(x, min, max)
        /// </summary>
        /// <param name="x"></param>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static double Clamp(this double x, double min, double max)
        {
            return MathHelper.Clamp(x, min, max);
        }

        /// <summary>
        /// Converts radians to degrees
        /// </summary>
        /// <param name="radians"></param>
        /// <returns></returns>
        public static double ToDegrees(this double radians)
        {
            return MathHelper.ToDegrees(radians);
        }

        /// <summary>
        /// Converts radians to degrees
        /// </summary>
        /// <param name="radians"></param>
        /// <returns></returns>
        public static float ToDegrees(this float radians)
        {
            return MathHelper.ToDegrees(radians);
        }

        /// <summary>
        /// Converts degrees to radians
        /// </summary>
        /// <param name="degrees"></param>
        /// <returns></returns>
        public static double ToRadians(this double degrees)
        {
            return MathHelper.ToRadians(degrees);
        }

        /// <summary>
        /// Converts degrees to radians
        /// </summary>
        /// <param name="radians"></param>
        /// <returns></returns>
        public static float ToRadians(this float degrees)
        {
            return MathHelper.ToRadians(degrees);
        }

        /// <summary>
        /// Absolutes the value, shorthand for Math.Abs(x)
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static double Absolute(this double x) => Math.Abs(x);

        /// <summary>
        /// Absolutes the value, shorthand for Math.Abs(x)
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        public static float Absolute(this float x) => Math.Abs(x);

        public static double ClampHinge(this double x)
        {
            return MathHelper.Clamp(x, -90, 90);
        }
    }
}
