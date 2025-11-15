using Sandbox.Game.EntityComponents;
using Sandbox.Game.Screens.Helpers;
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
    partial class Program : MyGridProgram
    {
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.


        const float SCAN_DISTANCE = 15f;

        public static Program Singleton;

        readonly BuildTools buildTools;
        readonly BlockFinder blockFinder;

        Vector3D targetA = new Vector3D(22109.6769786664, 17445.873015578, -21760.8787379871);
        Vector3D targetB = new Vector3D(22133.3670828157, 17446.2668889417, -21757.1633425961);

        Vector3D target;

        enum Classification
        {
            FLOOR,
            WALL
        }

        struct StatPoint
        {
            public Vector3D World;
            public IMyCameraBlock Source;
            public Classification Classification;
        }

        struct StatCamera
        {
            public IMyCameraBlock Camera;
            public int Iteration;
        }

        List<StatCamera> cameras;
        List<StatPoint> points;
        IMyShipController controller;

        List<IMyCubeGrid> grids;
        List<long> mechanicalGrids;
        double lowestDot;

        IMyProgrammableBlock mcs;
        IEnumerator cameraTask;

        public Program()
        {
            Singleton = this;
            buildTools = new BuildTools(this);
            buildTools.RemoveAll();
            blockFinder = new BlockFinder(GridTerminalSystem);
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.
            mcs = GridTerminalSystem.GetBlockWithName("PB Mech") as IMyProgrammableBlock;
            target = targetB;
            FindBlocks();
            Runtime.UpdateFrequency = UpdateFrequency.Update1;
        }

        void FindBlocks()
        {
            points = new List<StatPoint>();
            cameras = new List<StatCamera>();
            var cams = blockFinder.GetBlocksOfType<IMyCameraBlock>((c) => c.CustomName.Contains("ai"));
            foreach (var cam in cams)
            {
                cameras.Add(new StatCamera()
                {
                    Camera = cam,
                    Iteration = 0
                });
            }
            controller = blockFinder.GetBlocksOfType<IMyShipController>().First();

            var blocks = blockFinder.GetBlocksOfType<IMyMechanicalConnectionBlock>();
            grids = blocks.Select(b => b.CubeGrid).Distinct().ToList();
            mechanicalGrids = grids.Select(b => b.EntityId).ToList();
        }

        void DebugDraw()
        {
            buildTools.RemoveAll();
            foreach (var p in points)
            {
                buildTools.DrawPoint(p.World, p.Classification == Classification.FLOOR ? Color.Green : Color.Red);
                //buildTools.DrawLine(p.World, p.Source.WorldMatrix.Translation, Color.OrangeRed, 0.01f);
            }
        }

        public void Save()
        {
            // Called when the program needs to save its state. Use
            // this method to save your state to the Storage field
            // or some other means. 
            // 
            // This method is optional and can be removed if not
            // needed.
        }

        void PushPoint(StatPoint point)
        {
            if (points.Count > cameras.Count * 100)
                points.RemoveAt(0);
            points.Add(point);
        }

        IEnumerator UpdateCameras()
        {
            Vector3D gravity = controller.GetTotalGravity();
            Vector3D gravityNormal = gravity.Normalized();
            for (int i = 0; i < cameras.Count; i++)
            {
                yield return null;
                var stat = cameras[i];
                if (!stat.Camera.IsWorking)
                    continue;
                //Echo($"RaycastTimeMultiplier: {stat.Camera.RaycastTimeMultiplier * SCAN_DISTANCE}ms/{SCAN_DISTANCE}m");
                if (!stat.Camera.EnableRaycast)
                    stat.Camera.EnableRaycast = true;
                int backlog = 10;
                while (stat.Camera.AvailableScanRange > SCAN_DISTANCE)
                {
                    float pitch = MathHelper.Lerp(-stat.Camera.RaycastConeLimit, stat.Camera.RaycastConeLimit, (float)Math.Floor(stat.Iteration / 10f) / 9f);
                    float yaw = MathHelper.Lerp(-stat.Camera.RaycastConeLimit, stat.Camera.RaycastConeLimit, (stat.Iteration % 10) / 9f);
                    var ray = stat.Camera.Raycast(SCAN_DISTANCE, pitch, yaw);
                    stat.Iteration++;
                    if (stat.Iteration > 100)
                    {
                        stat.Iteration = 0;
                        break;
                    }
                    if (ray.IsEmpty())
                        continue;
                    if (ray.Type == MyDetectedEntityType.CharacterHuman)
                        continue;
                    if (mechanicalGrids.Contains(ray.EntityId)) // ignore self
                        continue;

                    // classify
                    Classification classification = Classification.WALL;
                    double gravityDot = Vector3D.Dot(ray.HitPosition.Value, gravityNormal);
                    if (lowestDot - gravityDot < 1f)
                    {
                        classification = Classification.FLOOR;
                    }

                    backlog -= 1;
                    if (backlog <= 0)
                    {
                        backlog = 10;
                        yield return null;
                    }
                    if (classification == Classification.FLOOR)
                        continue;

                    PushPoint(new StatPoint()
                    {
                        Source = stat.Camera,
                        World = ray.HitPosition.Value,
                        Classification = classification
                    });
                }
                cameras[i] = stat;
            }
            yield return null;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (!updateSource.HasFlag(UpdateType.Update1))
                return;


            Me.CustomData = ($"i am currently here: {controller.WorldMatrix.Translation}");

            Vector3D gravity = controller.GetTotalGravity();
            Vector3D gravityNormal = gravity.Normalized();

            /*double highestDot = double.MaxValue; // actually "inverted", since this is for ceiling and floor
            lowestDot = double.MinValue;
            foreach (var grid in grids)
            {
                double dot = Vector3D.Dot(grid.WorldAABB.Center, gravityNormal);
                if (dot > lowestDot)
                {
                    //refer = grid.WorldAABB.Center;
                }
                lowestDot = Math.Max(lowestDot, dot);
                highestDot = Math.Min(highestDot, dot);
            }
            Echo($"lowest dot: {lowestDot}");*/

            if (cameraTask == null)
            {
                cameraTask = UpdateCameras();
            }
            else
            {
                if (!cameraTask.MoveNext())
                    cameraTask = null;
            }
            Echo($"instr: {Runtime.CurrentInstructionCount} / {Runtime.MaxInstructionCount} ({((float)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount) * 100:f2}%)");

            var lowestGrid = GridTerminalSystem.GetBlockWithName("Top Mounted Camera 2 C L+").CubeGrid;
            lowestDot = Vector3D.Dot(lowestGrid.WorldAABB.Center, gravityNormal);

            DebugDraw();
            buildTools.DrawPoint(lowestGrid.WorldAABB.Center, Color.Plum, 0.2f);

            // find path?
            var current = controller.WorldMatrix.Translation;
            var direction = target - current;

            buildTools.DrawPoint(current, Color.Blue, .1f);
            buildTools.DrawPoint(target, Color.Teal, .1f);

            float totalDistance = (float)direction.Length();
            float bestPath = float.MinValue;
            int bestPathIndex = 0;
            for (int i = 0; i < 13; i++)
            {
                float offset = MathHelper.Lerp(-2f, 2f, i / 12f);
                float score = ScorePath(current, direction, offset, totalDistance);
                Echo($"path {i} ({offset}) with {score}");
                if (score > bestPath)
                {
                    bestPath = score;
                    bestPathIndex = i;
                }
                //Echo($"instr: {Runtime.CurrentInstructionCount} / {Runtime.MaxInstructionCount} ({((float)Runtime.CurrentInstructionCount / Runtime.MaxInstructionCount) * 100:f2}%)");
            }

            if (totalDistance < 5f)
            {
                // done
                mcs.TryRun("halt | turn =0");
                return;
            }

            Vector3D rotated = OffsetDirection(direction, MathHelper.Lerp(-2f, 2f, bestPathIndex / 12f));
            buildTools.DrawLine(current, current + rotated * 3, Color.Coral, .1f);

            double side = Vector3D.Dot(Vector3D.Cross(controller.WorldMatrix.Forward, rotated), controller.WorldMatrix.Up);

            Echo($"side: {side}");
            if (side > 0.01)
            {
                mcs.TryRun($"turn =-{MathHelper.Clamp(side * 3, -1, 1):f1}");
            }
            else if (side < -0.01)
            {
                mcs.TryRun($"turn ={-MathHelper.Clamp(side * 3, -1, 1):f1}");
            }
            else
            {
                mcs.TryRun("turn =0");
            }

            if (Math.Abs(side) > 0.5)
            {
                mcs.TryRun("halt");
            }
            else
            {
                mcs.TryRun("walk forwards");
            }


            /*Echo($"path -75 {ScorePath(current, direction, -.83f)}");
            Echo($"path -45 {ScorePath(current, direction, -.5f)}");
            Echo($"path 0 {ScorePath(current, direction, 0f)}");
            Echo($"path 45 {ScorePath(current, direction, .5f)}");
            Echo($"path 75 {ScorePath(current, direction, .83f)}");*/

        }

        float ScorePoint(Vector3D point, float offset)
        {
            float score = 0;//-Math.Abs(offset);//(float)(Vector3D.Distance(point, target) / baseDist);
            foreach (var stat in points)
            {
                if (stat.Classification != Classification.WALL)
                {
                    continue;
                }
                double dist = Vector3D.Distance(stat.World, point);
                if (dist < 4f)
                {
                    score -= (4f - (float)dist) * 4f;
                }
            }
            return score;
        }

        Vector3D OffsetDirection(Vector3D direction, float offset)
        {
            float offsetYaw = MathHelper.ToRadians(90f * offset);
            Quaternion quat = Quaternion.CreateFromAxisAngle(controller.WorldMatrix.Up, offsetYaw);

            return Vector3D.Transform(direction, quat).Normalized();
        }

        float ScorePath(Vector3D origin, Vector3D direction, float offset, float totalDistance)
        {
            direction = OffsetDirection(direction, offset);
            buildTools.DrawLine(origin, origin + direction * 2f, Color.Aqua, 0.01f);

            float score = 0.01f;

            // sample
            double dist = 4d; // meter look-a-head
            int steps = 2;
            for (int i = 0; i < steps; i++) // steps
            {
                double x = dist * (i / (double)(steps - 1));
                Vector3D checkPoint = origin + direction * x;
                score += ScorePoint(checkPoint, offset) / steps;// - (float)Vector3D.Distance(target, checkPoint);
            }
            //score -= /*totalDistance - */(float)Vector3D.Distance(origin + direction * dist, target) / 10;
            score -= Math.Abs(offset);
            score += (float)Vector3D.Dot(direction, controller.WorldMatrix.Forward); // score;

            return score;
        }
    }
}
