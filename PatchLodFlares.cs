using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

using Assets.Scripts;
using Assets.Scripts.Objects;

namespace AproposmathsStationeersPatches
{
    // Since 0.2.6376.27557 the game moves every lod flare onto the curved origin of its Thing.
    // The flare ends up inside/behind the collider of its own light and, beyond the curvature
    // start, below the (flat) terrain colliders, so Unity's flare occlusion hides it.
    // This keeps the flare at its bulb, does the occlusion test in the flat physics world
    // along the curved view ray and places the flare close to the camera in the rendered
    // direction of the bulb, such that Unity's own occlusion test is not affected by curvature.
    static class PatchLodFlares
    {
        class FlareState
        {
            public LensFlare Flare;
            public Thing Thing;
            public Transform Parent;
            public Vector3 LocalPosition;
        }

        const float FlareCameraDistance = 1f;
        const float OcclusionEndOffset = 0.1f;
        const int CurvedSegments = 16;

        static readonly int OcclusionMask = Physics.DefaultRaycastLayers
            & ~((1 << 1) | (1 << 17) | (1 << 8) | (1 << 9) | (1 << 10) | (1 << 11) | (1 << 26));

        static readonly Dictionary<LensFlare, FlareState> _states = new Dictionary<LensFlare, FlareState>();
        static readonly List<FlareState> _stateList = new List<FlareState>();
        static int _updateIndex = 0;

        public static void Enable()
        {
            Camera.onPreCull -= OnPreCull;
            Camera.onPreCull += OnPreCull;
        }

        public static void Disable()
        {
            Camera.onPreCull -= OnPreCull;
            foreach (var s in _stateList)
                if (s.Flare && s.Parent)
                    s.Flare.transform.localPosition = s.LocalPosition;
            _states.Clear();
            _stateList.Clear();
        }

        // the live transform may already have been moved (by the game or a previously loaded
        // version of this patch), so take the local position from the prefab
        static Vector3 OriginalLocalPosition(Thing thing, LensFlare flare)
        {
            var prefab = thing.SourcePrefab;
            if (prefab)
            {
                int index = thing.lodFlares.IndexOf(flare);
                var prefabFlares = prefab.GetComponentsInChildren<LensFlare>(true);
                if (index >= 0 && index < prefabFlares.Length && prefabFlares[index].name == flare.name)
                    return prefabFlares[index].transform.localPosition;
            }
            return flare.transform.localPosition;
        }

        static FlareState GetState(Thing thing, LensFlare flare)
        {
            if (_states.TryGetValue(flare, out var state))
                return state;
            var t = flare.transform;
            state = new FlareState
            {
                Flare = flare,
                Thing = thing,
                Parent = t.parent,
                LocalPosition = t.parent ? OriginalLocalPosition(thing, flare) : t.position,
            };
            _states.Add(flare, state);
            _stateList.Add(state);
            return state;
        }

        static Vector3 BulbPosition(FlareState s)
        {
            return s.Parent ? s.Parent.TransformPoint(s.LocalPosition) : s.LocalPosition;
        }

        static bool UseCurvature(FlareState s)
        {
            return s.Thing.Position.y < 1000f;
        }

        // inverse of TerrainCurvature.Curve
        static Vector3 Uncurve(Vector3 rendered, Vector3 cameraPosition)
        {
            var flat = new Vector3(rendered.x - OrbitalViewController.RecenterX, 0f, rendered.z - OrbitalViewController.RecenterZ);
            flat.y = rendered.y + TerrainCurvature.Drop(flat, cameraPosition);
            return flat;
        }

        static bool Linecast(Vector3 from, Vector3 to)
        {
            return Physics.Linecast(from, to, OcclusionMask, QueryTriggerInteraction.Ignore);
        }

        static bool IsOccluded(Vector3 cameraPosition, Vector3 bulb, bool curved)
        {
            var toCamera = cameraPosition - bulb;
            if (toCamera.magnitude <= OcclusionEndOffset)
                return false;
            if (!curved)
                return Linecast(cameraPosition, bulb + toCamera.normalized * OcclusionEndOffset);

            // straight ray in rendered space from camera to the curved bulb, mapped back to flat space
            var rendered = TerrainCurvature.Curve(bulb, cameraPosition);
            var prev = cameraPosition;
            for (int i = 1; i <= CurvedSegments; i++)
            {
                var next = i == CurvedSegments
                    ? bulb
                    : Uncurve(Vector3.Lerp(cameraPosition, rendered, (float)i / CurvedSegments), cameraPosition);
                if (i == CurvedSegments)
                {
                    var back = prev - next;
                    if (back.magnitude <= OcclusionEndOffset)
                        return false;
                    next += back.normalized * OcclusionEndOffset;
                }
                if (Linecast(prev, next))
                    return true;
                prev = next;
            }
            return false;
        }

        [HarmonyPatch(typeof(Thing)), HarmonyPatch(nameof(Thing.UpdateLodFlares)), HarmonyPrefix]
        static bool PrefixUpdateLodFlares()
        {
            var things = Thing.AllLodFlareThings;
            if (things.Count == 0)
                return false;
            TerrainCurvature.Refresh();
            var cameraPosition = CameraController.CameraPosition;
            var brightnessCurve = CursorManager.Instance.FlareBrightnessDistanceCurve;

            // same round robin as the original: 20% of all things per frame
            if (_updateIndex <= 0 || _updateIndex >= things.Count)
                _updateIndex = things.Count - 1;
            float end = Mathf.Max(0f, _updateIndex - things.Count * 0.2f);
            for (int i = _updateIndex; i >= end; i--)
            {
                _updateIndex = i;
                var thing = things[i];
                if ((object)thing == null)
                {
                    things.RemoveAt(i);
                    continue;
                }
                if (!thing)
                    continue;
                float brightness = brightnessCurve.Evaluate(Vector3.Distance(cameraPosition, thing.Position));
                foreach (var flare in thing.lodFlares)
                {
                    if (!flare)
                        continue;
                    var state = GetState(thing, flare);
                    if (brightness > 0f && flare.gameObject.activeInHierarchy
                        && IsOccluded(cameraPosition, BulbPosition(state), UseCurvature(state)))
                        flare.brightness = 0f;
                    else
                        flare.brightness = brightness;
                }
            }
            return false;
        }

        static void OnPreCull(Camera camera)
        {
            if (camera != CameraController.CurrentCamera || _stateList.Count == 0)
                return;
            TerrainCurvature.Refresh();
            var cameraPosition = camera.transform.position;
            for (int i = _stateList.Count - 1; i >= 0; i--)
            {
                var s = _stateList[i];
                if (!s.Flare || !s.Thing)
                {
                    _states.Remove(s.Flare);
                    _stateList[i] = _stateList[_stateList.Count - 1];
                    _stateList.RemoveAt(_stateList.Count - 1);
                    continue;
                }
                if (!s.Flare.isActiveAndEnabled)
                    continue;
                var bulb = BulbPosition(s);
                var target = UseCurvature(s) ? TerrainCurvature.Curve(bulb, cameraPosition) : bulb;
                var dir = target - cameraPosition;
                float distance = dir.magnitude;
                s.Flare.transform.position = distance > FlareCameraDistance
                    ? cameraPosition + dir * (FlareCameraDistance / distance)
                    : target;
            }
        }
    }
}
