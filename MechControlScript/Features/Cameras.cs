using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage;
using VRageMath;
using static IngameScript.Program;

namespace IngameScript
{
    static class Cameras
    {
        public static Dictionary<int, MyTuple<double, double>> legCameras = new Dictionary<int, MyTuple<double, double>>();

        public static void SetGroup(int group, double left, double right)
        {
            if (legCameras.ContainsKey(group))
            {
                var tuple = legCameras[group];
                tuple.Item1 = left;
                tuple.Item2 = right;
                legCameras[group] = tuple;
            }
            else
            {
                legCameras.Add(group, MyTuple.Create(left, right));
            }
        }

        public static MyTuple<double, double> GetGroup(int group)
        {
            if (!legCameras.ContainsKey(group))
                return MyTuple.Create(0d, 0d);
            return legCameras[group];
        }

        public static MyTuple<double, double> CalculateGroup(int group)
        {
            var tuple = GetGroup(group);
            if (tuple.Item1 == 0 || tuple.Item2 == 0)
                return tuple;

            double min = double.MaxValue;
            double max = double.MinValue;
            foreach (var cam in legCameras.Values)
            {
                min = MathHelperD.Min(min, MathHelperD.Min(cam.Item1, cam.Item2));
                max = MathHelperD.Max(max, MathHelperD.Max(cam.Item1, cam.Item2));
            }
            Program.Log("CalculateGroup", group, max, min);

            double difference = (max - min);

            return MyTuple.Create(difference - (tuple.Item1 - min), difference - (tuple.Item2 - min));
        }

        public static MyTuple<double, double> GetGroups()
        {
            double left = 0;
            double right = 0;
            foreach (var kv in legCameras)
            {
                left += kv.Value.Item1;
                right += kv.Value.Item2;
            }

            return MyTuple.Create(left, right);
        }

        public static void Reset()
        {
            legCameras.Clear();
        }
    }
}
