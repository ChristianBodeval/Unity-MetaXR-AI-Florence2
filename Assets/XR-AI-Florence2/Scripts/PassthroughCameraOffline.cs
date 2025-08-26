// Put this in your project (e.g., Assets/Scripts/PassthroughCameraOffline.cs)
// Namespace matches the package so it's easy to call side-by-side.
using System;
using UnityEngine;

namespace PassthroughCameraSamples
{
    /// <summary>
    /// Utilities for working with *saved* passthrough camera data, so you can
    /// raycast later using the pose/intrinsics that existed when a frame was captured.
    /// </summary>
    public static class PassthroughCameraOffline
    {
        /// <summary>
        /// A snapshot tying together the camera eye, its *world* pose at capture time,
        /// the camera intrinsics (in pixels), the resolution those intrinsics map to,
        /// and an optional Texture2D containing the saved frame you rendered or copied.
        /// </summary>
        [Serializable]
        public struct SavedSnapshot
        {
            public PassthroughCameraEye Eye;
            public Pose CameraPoseWorld;                 // World pose when the frame was captured
            public PassthroughCameraIntrinsics Intrinsics;
            public Vector2Int Resolution;                // Intrinsics.Resolution for convenience
            public long TimestampNs;                     // Optional: your own timestamp
            public Texture2D FrameRGBA;                  // Optional: saved frame you rendered/copied

            public bool HasFrame => FrameRGBA != null;
        }

        /// <summary>
        /// Capture a snapshot using the *current* pose + intrinsics, and optionally attach your saved frame.
        /// Use this right before you kick off the async call (e.g., to Florence2).
        /// </summary>
        public static SavedSnapshot CaptureSnapshot(PassthroughCameraEye eye, Texture2D savedFrame = null, long timestampNs = 0)
        {
            var intr = PassthroughCameraUtils.GetCameraIntrinsics(eye);
            var pose = PassthroughCameraUtils.GetCameraPoseInWorld(eye);

            return new SavedSnapshot
            {
                Eye = eye,
                CameraPoseWorld = pose,
                Intrinsics = intr,
                Resolution = intr.Resolution,
                TimestampNs = timestampNs,
                FrameRGBA = savedFrame
            };
        }

        /// <summary>
        /// Create a snapshot from values you already saved elsewhere (e.g., you stored a Pose + intrinsics earlier).
        /// </summary>
        public static SavedSnapshot FromSaved(PassthroughCameraEye eye, Pose cameraPoseWorld, PassthroughCameraIntrinsics intrinsics, Texture2D savedFrame = null, long timestampNs = 0)
        {
            return new SavedSnapshot
            {
                Eye = eye,
                CameraPoseWorld = cameraPoseWorld,
                Intrinsics = intrinsics,
                Resolution = intrinsics.Resolution,
                TimestampNs = timestampNs,
                FrameRGBA = savedFrame
            };
        }

        /// <summary>
        /// Compute a ray in *camera space* using saved intrinsics and a pixel coordinate in that saved frame.
        /// The pixel must be in the *intrinsics’ resolution space* (usually intrinsics.Resolution).
        /// </summary>
        public static Ray ScreenPointToRayInCamera(in PassthroughCameraIntrinsics intrinsics, Vector2Int pixel)
        {
            // Matches the logic in PassthroughCameraUtils.ScreenPointToRayInCamera
            var dirCam = new Vector3
            {
                x = (pixel.x - intrinsics.PrincipalPoint.x) / intrinsics.FocalLength.x,
                y = (pixel.y - intrinsics.PrincipalPoint.y) / intrinsics.FocalLength.y,
                z = 1f
            };

            return new Ray(Vector3.zero, dirCam);
        }

        /// <summary>
        /// Same as above but with normalized UV in [0..1], automatically scaled to the saved resolution.
        /// Handy when your detector returns normalized coords.
        /// </summary>
        public static Ray NormalizedToRayInCamera(in PassthroughCameraIntrinsics intrinsics, Vector2 uv01)
        {
            var px = Mathf.Clamp01(uv01.x) * (intrinsics.Resolution.x - 1);
            var py = Mathf.Clamp01(uv01.y) * (intrinsics.Resolution.y - 1);
            return ScreenPointToRayInCamera(intrinsics, new Vector2Int(Mathf.RoundToInt(px), Mathf.RoundToInt(py)));
        }

        /// <summary>
        /// Project a saved *camera-space* ray into *world space* using the saved world pose from the snapshot.
        /// </summary>
        public static Ray CameraRayToWorld(in SavedSnapshot snap, in Ray rayInCamera)
        {
            var worldOrigin = snap.CameraPoseWorld.position;
            var worldDir = snap.CameraPoseWorld.rotation * rayInCamera.direction;
            return new Ray(worldOrigin, worldDir);
        }

        /// <summary>
        /// Direct helper: compute a *world-space* ray from a *saved snapshot* and a pixel in that saved frame.
        /// </summary>
        public static Ray ScreenPointToRayInWorld(in SavedSnapshot snap, Vector2Int pixel)
        {
            var rayCam = ScreenPointToRayInCamera(in snap.Intrinsics, pixel);
            return CameraRayToWorld(in snap, in rayCam);
        }

        /// <summary>
        /// Direct helper: compute a *world-space* ray using normalized [0..1] UV and a saved snapshot.
        /// </summary>
        public static Ray NormalizedToRayInWorld(in SavedSnapshot snap, Vector2 uv01)
        {
            var rayCam = NormalizedToRayInCamera(in snap.Intrinsics, uv01);
            return CameraRayToWorld(in snap, in rayCam);
        }

        /// <summary>
        /// Utility to convert normalized UV (relative to saved frame) to pixel coords in that saved frame.
        /// </summary>
        public static Vector2Int UV01ToPixel(in SavedSnapshot snap, Vector2 uv01)
        {
            var x = Mathf.Clamp01(uv01.x) * (snap.Resolution.x - 1);
            var y = Mathf.Clamp01(uv01.y) * (snap.Resolution.y - 1);
            return new Vector2Int(Mathf.RoundToInt(x), Mathf.RoundToInt(y));
        }
    }
}
