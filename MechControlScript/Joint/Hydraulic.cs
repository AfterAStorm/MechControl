using Sandbox.ModAPI.Ingame;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    public partial class Program
    {
        public void IterateHydraulics()
        {
            List<IMyPistonBase> subPistons = new List<IMyPistonBase>();
            foreach (var group in legs)
            {
                var tri = group.Value as TriLegGroup;
                if (tri == null)
                    continue;

                List<Hydraulic> ignoreHydraulics = new List<Hydraulic>();
                foreach (var hy in tri.Hydraulics)
                {
                    IMyCubeGrid grid = hy.Pistons.Last().TopGrid;
                    do
                    {
                        subPistons.Clear();
                        Log($"subPistons: {subPistons} {grid}");
                        GridTerminalSystem.GetBlocksOfType(subPistons, piston => piston.CubeGrid.Equals(grid));
                        if (subPistons.Count != 1 || hy.Pistons.Contains(subPistons[0]))
                            break; // uhhhh.. no
                        var subHydraulic = tri.Hydraulics.Find(h => h.Pistons.Contains(subPistons[0]));
                        if (subHydraulic != null)
                        {
                            ignoreHydraulics.Add(subHydraulic);
                            hy.Pistons.Add(subPistons[0]);
                            grid = subPistons[0].TopGrid;
                        }
                    }
                    while (true);
                }
                tri.Hydraulics = tri.Hydraulics.Where(hy => !ignoreHydraulics.Contains(hy)).ToList();
                foreach (var hy in tri.Hydraulics)
                {
                    Log($"iterate hydraulic {hy}");
                    hy.Iterate();
                }
            }
        }

        public class Hydraulic
        {

            public List<IMyPistonBase> Pistons;

            public IMyMotorStator TopStator; // the 2 reference points
            public IMyMotorStator BottomStator;

            public IMyCubeGrid TopGrid; // grids the stators attach to, not the TopGrid, but the "leg appendage"/etc.
            public IMyCubeGrid BottomGrid;

            public double TopDistance; // offset for calculating piston distance
            public double BottomDistance;
            public double IntermediateDistance;

            public MatrixD TopPosition; // generated elsewhere
            public MatrixD BottomPosition;

            public bool Valid { get; private set; }
            public FetchedBlock Source { get; private set; }

            public Hydraulic(FetchedBlock block)
            {
                Source = block;
                Pistons = new List<IMyPistonBase>() { block.Block as IMyPistonBase };
            }

            public void Iterate()
            {
                // find stators
                IMyPistonBase first = Pistons.First();
                IMyPistonBase last = Pistons.Last();
                Singleton.Echo($"pistons: {Pistons.Count}");
                Singleton.Echo($"first: {first}");
                Singleton.Echo($"last: {last}");
                List<IMyMotorStator> connectedStators = new List<IMyMotorStator>(); // "yucky, singleton!" - wa wa wa
                Singleton.GridTerminalSystem.GetBlocksOfType(connectedStators, (b) => b.IsSameConstructAs(Singleton.Me) && b.Top != null && (b.CubeGrid.Equals(first.CubeGrid) || b.TopGrid.Equals(first.CubeGrid)));
                List<IMyMotorStator> connectedStators2 = new List<IMyMotorStator>(); // WHY IS THIS LIKE THE ONLY FUNCTION THAT CLEARS THE LIST? AAAAAAAAAAAA
                Singleton.GridTerminalSystem.GetBlocksOfType(connectedStators2, (b) => b.IsSameConstructAs(Singleton.Me) && b.Top != null && (b.CubeGrid.Equals(last.TopGrid) || b.TopGrid.Equals(last.TopGrid)));
                connectedStators.AddRange(connectedStators2);

                Valid = true;
                if (connectedStators.Count != 2)
                {
                    Valid = false;
                    StaticWarn($"Hydraulic missing rotors", $"Hyraulic {first.CustomName} is missing rotors (required 2, found {connectedStators.Count})");
                    return;
                }

                TopStator = connectedStators[0];
                BottomStator = connectedStators[1];

                TopDistance = CountDistance(TopStator, first, out TopGrid) * TopGrid.GridSize;
                BottomDistance = CountDistance(BottomStator, last, out BottomGrid) * BottomGrid.GridSize;

                IntermediateDistance = 0;
                for (int i = 1; i < Pistons.Count; i++)
                {
                    var from = Pistons[i - 1];
                    var to = Pistons[i];
                    IntermediateDistance += Vector3I.Dot(Base6Directions.GetIntVector(from.Top.Orientation.Up), to.Position) - Vector3I.Dot(Base6Directions.GetIntVector(from.Top.Orientation.Up), from.Top.Position);
                }
            }

            public void Update()
            {
                //double topX = Vector3D.Dot(Base6Directions.GetIntVector(Pistons[0].Orientation.Left), TopStator.WorldMatrix.Translation) - Vector3D.Dot(Base6Directions.GetIntVector(Pistons[0].Orientation.Left), Pistons[0].WorldMatrix.Translation);
                //double topZ = Vector3D.Dot(Base6Directions.GetIntVector(Pistons[0].Orientation.Forward), TopStator.WorldMatrix.Translation) - Vector3D.Dot(Base6Directions.GetIntVector(Pistons[0].Orientation.Forward), Pistons[0].WorldMatrix.Translation);
                //double bottomX = Vector3D.Dot(Base6Directions.GetIntVector(Pistons[0].Orientation.Left), BottomStator.WorldMatrix.Translation) - Vector3D.Dot(Base6Directions.GetIntVector(Pistons[0].Orientation.Left), Pistons[0].WorldMatrix.Translation);
                //double bottomZ = Vector3D.Dot(Base6Directions.GetIntVector(Pistons[0].Orientation.Forward), BottomStator.WorldMatrix.Translation) - Vector3D.Dot(Base6Directions.GetIntVector(Pistons[0].Orientation.Forward), Pistons[0].WorldMatrix.Translation);

                //Log("bottomX:", bottomX);
                //Log("bottomZ:", bottomZ);
                //Log("topX:", topX);
                //Log("topZ:", topZ);

                /*MatrixD localBottom = BottomStator.WorldMatrix * MatrixD.Invert(Pistons[0].WorldMatrix);
                Log("localBottom X:", localBottom.Translation.X);
                Log("localBottom.Y:", localBottom.Translation.Y);
                Log("localBottom Z:", localBottom.Translation.Z);*/
                Log($"PISTONS: {Pistons.Count}");
                var last = Pistons.Last();
                //Vector3D direction = last.WorldMatrix.Up;
                //Vector3D relDirection = Vector3D.TransformNormal(direction, MatrixD.Invert(BottomStator.WorldMatrix));
                //Vector3D newDirection = Vector3D.TransformNormal(relDirection, BottomPosition);

                var p1 = BottomPosition.Translation;
                var p2 = TopPosition.Translation;

                /*
                
                ex:
                side view:
                A ---------\
                         L |
                         L |
                         LB|

                we get direction, they're parallel always, if not, then user error xd
                
                ------>

                dot their difference on it

                \-
                  \-
                    \-
                      \-
                        \-
                          \-

                becomes
                         |
                         |
                         |
                         |

                */

                var u = Vector3D.Normalize(BottomPosition.Up);
                //var v = Vector3D.Normalize(TopPosition.Up);

                var dir = p2 - p1;
                double alpha = Vector3D.Dot(dir, u);
                Vector3D p = dir - alpha * u;
                double len = p.Length();

                float positionDistance = (float)len;// (Math.Abs(Vector3.Dot(BottomPosition.Translation - TopPosition.Translation, axis)));
                    //(float)Vector3D.Distance(TopPosition.Translation + Vector3D.TransformNormal(localTop, TopPosition), BottomPosition.Translation + Vector3D.Transform(localBottom, BottomPosition) /*- bottomTop.Translation * and*/);
                Log("positionDistance:", positionDistance);
                float distance = (float)(positionDistance - (IntermediateDistance + TopDistance + BottomDistance + .064f * TopStator.CubeGrid.GridSize * Pistons.Count));
                float perDistance = distance / Pistons.Count;
                foreach (var piston in Pistons)
                {
                    piston.MoveToPosition(perDistance, 60);
                }
            }

            internal static double CountDistance(IMyMotorStator stator, IMyPistonBase piston, out IMyCubeGrid grid, int basegridOffset = 1)
            {
                if (stator.TopGrid.Equals(piston.TopGrid))
                {
                    grid = stator.CubeGrid;
                    return Vector3I.Dot(Base6Directions.GetIntVector(piston.Top.Orientation.Up), stator.Top.Position) - Vector3I.Dot(Base6Directions.GetIntVector(piston.Top.Orientation.Up), piston.Top.Position); //(stator.Top.Position - Piston.Top.Position).Length();
                }
                else if (stator.CubeGrid.Equals(piston.TopGrid))
                {
                    grid = stator.TopGrid;
                    return Vector3I.Dot(Base6Directions.GetIntVector(piston.Top.Orientation.Up), stator.Position) - Vector3I.Dot(Base6Directions.GetIntVector(piston.Top.Orientation.Up), piston.Top.Position); //(stator.Position - Piston.Top.Position).Length();
                }
                else if (stator.TopGrid.Equals(piston.CubeGrid)) // these two are plus one because they reference the top part of the bottom part of the piston.. i guess?
                {
                    grid = stator.CubeGrid;
                    return Vector3I.Dot(-Base6Directions.GetIntVector(piston.Orientation.Up), stator.Top.Position) - Vector3I.Dot(-Base6Directions.GetIntVector(piston.Orientation.Up), piston.Position) + basegridOffset; //(stator.Top.Position - Piston.Position).Length() + 1;
                }
                else if (stator.CubeGrid.Equals(piston.CubeGrid))
                {
                    grid = stator.TopGrid;
                    return Vector3I.Dot(-Base6Directions.GetIntVector(piston.Orientation.Up), stator.Position) - Vector3I.Dot(-Base6Directions.GetIntVector(piston.Orientation.Up), piston.Position) + basegridOffset; //(stator.Position - Piston.Position).Length() + 1;
                }
                grid = stator.CubeGrid;
                return 0; // should be impossible! (assuming args are valid!)
            }

        }

        /*public class HydraulicGroup
        {
            public IMyMotorStator Reference; // just one... all we need it for is rotation

            public IMyCubeGrid Grid; // the grid it represents (TopGrid)

            public double Target; // the target angle

            public Vector3D Axis => IsHinge ? Vector3D.Right : Vector3D.Up;
            public bool IsHinge => Reference.BlockDefinition.SubtypeName.Contains("Hinge");

            public HydraulicGroup(IMyMotorStator reference, double target)
            {
                Reference = reference;
                Grid = reference.TopGrid;
                Target = (target.Modulo(360) - reference.Angle + 540).Modulo(360) - 180;//target - reference.Angle; // get the difference (to rotate the fake subgrid)
            }
        }*/
    }
}
