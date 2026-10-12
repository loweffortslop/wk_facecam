using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace FaceCam
{
    // Remains only move while unseen, and they check that with Renderer.isVisible, which is true when ANY camera
    // renders them, FaceCam included. Swap that call for one that only counts the player's own view.
    [HarmonyPatch(typeof(DEN_Remains_Active), nameof(DEN_Remains_Active.Update))]
    internal static class RemainsVisibilityPatch
    {
        private static readonly MethodInfo IsVisibleGetter = AccessTools.PropertyGetter(typeof(Renderer), nameof(Renderer.isVisible));
        private static readonly MethodInfo Replacement = AccessTools.Method(typeof(RemainsVisibilityPatch), nameof(SeenByPlayer));
        private static readonly Plane[] Planes = new Plane[6];

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            foreach (CodeInstruction ins in instructions)
            {
                if (ins.Calls(IsVisibleGetter))
                {
                    // Edit in place so jump labels on this instruction are kept.
                    replaced++;
                    ins.opcode = OpCodes.Call;
                    ins.operand = Replacement;
                }
                yield return ins;
            }
            if (replaced == 0) FaceCamPlugin.Log.LogWarning("Remains patch: isVisible call not found; Remains may freeze while on FaceCam.");
        }

        private static bool SeenByPlayer(Renderer renderer)
        {
            if (!renderer.isVisible) return false;
            if (!FaceCamPlugin.Rendering) return true; // FaceCam isn't drawing, so isVisible means the game's own cameras.

            ENT_Player player = ENT_Player.playerObject;
            Camera cam = player != null ? player.cam : null;
            if (cam == null) return true;

            // isVisible is frustum-based for the game's cameras, so test the player's frustum instead.
            GeometryUtility.CalculateFrustumPlanes(cam, Planes);
            return GeometryUtility.TestPlanesAABB(Planes, renderer.bounds);
        }
    }
}
