using Assets.Scripts.GridSystem;
using Assets.Scripts;
using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

using UnityEngine;

// Rationale: Speed up room searches by skipping some more "easy" cases
// When we search for a new room starting at grid G, and we eventually find all
// six neighboring grids, then no structure in grid G might have changed the room connectivity
// -> no further search is needed
//
// Use this idea to check if a short path (<=3steps) exists to each neighboring grid
//
// Note that we are checking in 6 directions because we don't know which exact structure changed
// and triggered the room recalculation, so we have to be conservative here
// With this additional information, we could reduce the number of expensive searches even more

namespace AproposmathsStationeersPatches
{
    [HarmonyPatch]
    static class PatchFasterRoomSearch
    {
        // checks if the both voxels belong to the same room (i.e. no blocking wall in between)
        static bool IsFaceOpen(Grid3 grid0, Grid3 grid1)
        {
            var p0 = grid0.ToVector3();
            var p1 = grid1.ToVector3();

            // not sure if this is how we get the two possible walls at either side
            var s0 = GridController.World.GetFaceStructure(grid0, p1 - p0);
            var s1 = GridController.World.GetFaceStructure(grid1, p0 - p1);

            return (s0 == null || s0.CanGravityPass) && (s1 == null || s1.CanGravityPass);
        }

        // checks if both sides of a face are connected (belong to the same room)
        // by walking around one of the four edges in three steps
        // n is the face normal direction (sign does not matter)
        // dir is the vector from the center of the face to one of its edges (four possible choices)
        static bool HasFaceConnectedSides(Grid3 grid, Vector3 n, Vector3 dir)
        {
            Grid3 g0 = grid + new Grid3(2 * dir);
            Grid3 g1 = g0 + new Grid3(2 * n);
            Grid3 g2 = g1 - new Grid3(-2 * dir);

            // I think the CanAirPass checks are not handle voxels blocked by terrain, this might need to be changed
            return GridController.World.CanAirPass(g0) &&
              GridController.World.CanAirPass(g1) &&
              IsFaceOpen(grid, g0) &&
              IsFaceOpen(g0, g1) &&
              IsFaceOpen(g1, g2);
        }

        // checks if any of the four edges of a face allow going to the other side
        static bool HasFaceConnectedSides(Grid3 grid, Vector3 n)
        {
            // check direct path (one step)
            Grid3 neighbor = grid + new Grid3(2*n);
            if (IsFaceOpen(grid, neighbor))
                return true;

            Vector3 dir2 = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(n, Vector3.up)) > 0.9)
                dir2 = Vector3.right;
            Vector3 dir3 = Vector3.Cross(n, dir2);

            // check paths around edges (three steps)
            return HasFaceConnectedSides(grid, n, dir2) ||
                   HasFaceConnectedSides(grid, n, -dir2) ||
                   HasFaceConnectedSides(grid, n, dir3) ||
                   HasFaceConnectedSides(grid, n, -dir3);
        }

        // checks if all six faces of the grid have connected sides
        // if so, any structure change at this grid cannot change room connectivity
        // this could be much more efficient if we knew which face/structure
        // was changed and triggered the room calculation, but this info is not available in RoomCheck,
        // so we always check all six faces, which leads to false negatives
        static bool CanSkipCheck(RoomCheck check)
        {
            if (check.Source != RoomChangeSource.Structure)
                return false;

            Grid3 grid = check.Grid;
            bool result = HasFaceConnectedSides(grid, Vector3.up) &&
                   HasFaceConnectedSides(grid, Vector3.down) &&
                   HasFaceConnectedSides(grid, Vector3.left) &&
                   HasFaceConnectedSides(grid, Vector3.right) &&
                   HasFaceConnectedSides(grid, Vector3.forward) &&
                   HasFaceConnectedSides(grid, Vector3.back);

            UnityEngine.Debug.Log($"Can skip room check at {grid}: {result}");
            return result;
        }

        // patch to call CanSkipCheck(item) before searching for a room
        // if it returns true, we set num_found_grids = MAXIterations to skip room searching while loop completely
        // Don't look at it closely, it is AI generated
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(RoomController), "ThreadedWork")]
        public static IEnumerable<CodeInstruction> Transpiler(
              IEnumerable<CodeInstruction> instructions,
              ILGenerator il)
        {
            var codes = new List<CodeInstruction>(instructions);

            MethodInfo listAddMethod = AccessTools.Method(typeof(List<Grid3>), "Add");
            MethodInfo canSkip = AccessTools.Method(typeof(PatchFasterRoomSearch), "CanSkipCheck");
            FieldInfo maxIterationsField = AccessTools.Field(typeof(RoomController), "MAXIterations");

            int itemLocalIndex = -1;
            int numLocalIndex = -1;

            // -----------------------------------------------------------
            // Helper function to extract local index from ldloc.* opcodes
            // -----------------------------------------------------------
            bool IsLdloc(CodeInstruction ci, out int index)
            {
                index = -1;

                if (ci.opcode == OpCodes.Ldloc_0) { index = 0; return true; }
                if (ci.opcode == OpCodes.Ldloc_1) { index = 1; return true; }
                if (ci.opcode == OpCodes.Ldloc_2) { index = 2; return true; }
                if (ci.opcode == OpCodes.Ldloc_3) { index = 3; return true; }

                if (ci.opcode == OpCodes.Ldloc_S || ci.opcode == OpCodes.Ldloc)
                {
                    if (ci.operand is LocalBuilder lb)
                    {
                        index = lb.LocalIndex;
                        return true;
                    }
                    if (ci.operand is int idx)
                    {
                        index = idx;
                        return true;
                    }
                }

                return false;
            }

            // -----------------------------------------------------------
            // 1. Locate `item` local:
            // pattern is:
            //
            // ldloc.* item
            // ldfld RoomCheck::Grid
            // callvirt List<Grid3>.Add
            // -----------------------------------------------------------
            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(listAddMethod))
                {
                    if (i >= 2 && IsLdloc(codes[i - 2], out itemLocalIndex))
                    {
                        break;
                    }
                }
            }

            if (itemLocalIndex < 0)
            {
                UnityEngine.Debug.LogError("Transpiler: Could not find item local.");
                return codes;
            }

            // -----------------------------------------------------------
            // 2. Locate `num` local:
            // pattern used by the while loop:
            //
            // ldloc num
            // ldsfld MAXIterations
            // blt / blt.s
            // -----------------------------------------------------------
            for (int i = 0; i < codes.Count - 2; i++)
            {
                if (IsLdloc(codes[i], out int idx1) &&
                    codes[i + 1].opcode == OpCodes.Ldsfld &&
                    codes[i + 1].operand == maxIterationsField)
                {
                    numLocalIndex = idx1;
                    break;
                }
            }

            if (numLocalIndex < 0)
            {
                UnityEngine.Debug.LogError("Transpiler: Could not find num local.");
                return codes;
            }

            // -----------------------------------------------------------
            // 3. Find injection point after `list.Add(item.Grid)`
            // -----------------------------------------------------------
            int insertIndex = -1;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].Calls(listAddMethod))
                {
                    insertIndex = i + 1;
                    break;
                }
            }

            if (insertIndex < 0)
            {
                UnityEngine.Debug.LogError("Transpiler: Could not find List.Add to insert after.");
                return codes;
            }

            // -----------------------------------------------------------
            // 4. Insert patch:
            //
            // if (MyPatch.CanSkipItem(item))
            //     num = MAXIterations;
            // -----------------------------------------------------------
            System.Reflection.Emit.Label continueLabel = il.DefineLabel();
            codes[insertIndex].labels.Add(continueLabel);

            var injected = new List<CodeInstruction>()
        {
            // load item local
            new CodeInstruction(OpCodes.Ldloc, itemLocalIndex),

            // call CanSkipItem(item)
            new CodeInstruction(OpCodes.Call, canSkip),

            // if false → skip
            new CodeInstruction(OpCodes.Brfalse_S, continueLabel),

            // load MAXIterations
            new CodeInstruction(OpCodes.Ldsfld, maxIterationsField),

            // num = MAXIterations
            new CodeInstruction(OpCodes.Stloc, numLocalIndex),
        };

            codes.InsertRange(insertIndex, injected);

            return codes;
        }
    }
}
