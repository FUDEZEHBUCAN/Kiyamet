using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace _Root.Scripts.Network
{
    /// <summary>
    /// Oturum genelinde en yüksek kayıtlı checkpoint aşamasını tutar. Sahneye NetworkObject ile ekleyin.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkCheckpointManager : NetworkBehaviour
    {
        public static NetworkCheckpointManager Instance { get; private set; }

        [Networked] public int ActiveCheckpointStage { get; private set; }
        [Networked] public int CaptureNotifySequence { get; private set; }

        private readonly List<CheckpointCapturePoint> _capturePoints = new();

        public static NetworkCheckpointManager FindActiveInstance()
        {
            if (Instance != null && Instance.Object != null && Instance.Object.IsValid)
                return Instance;

            var managers = FindObjectsOfType<NetworkCheckpointManager>();
            for (int i = 0; i < managers.Length; i++)
            {
                var manager = managers[i];
                if (manager != null && manager.Object != null && manager.Object.IsValid)
                    return manager;
            }

            return null;
        }

        public static void TryCaptureStageFromWorld(int stage)
        {
            if (!TryResolveServerRunner(out _))
                return;

            var manager = FindActiveInstance();
            if (manager == null || !manager.Object.HasStateAuthority)
                return;

            manager.TryCaptureStage(stage);
        }

        public override void Spawned()
        {
            Instance = this;

            if (!Object.HasStateAuthority)
                return;

            _capturePoints.Clear();
            var points = FindObjectsOfType<CheckpointCapturePoint>();
            for (int i = 0; i < points.Length; i++)
            {
                if (points[i] != null)
                    RegisterCapturePoint(points[i]);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this)
                Instance = null;

            _capturePoints.Clear();
        }

        public void RegisterCapturePoint(CheckpointCapturePoint point)
        {
            if (point == null || _capturePoints.Contains(point))
                return;

            _capturePoints.Add(point);
        }

        public void UnregisterCapturePoint(CheckpointCapturePoint point)
        {
            if (point == null)
                return;

            _capturePoints.Remove(point);
        }

        public void TryCaptureStage(int stage)
        {
            if (!Object.HasStateAuthority)
                return;

            stage = Mathf.Max(1, stage);
            if (stage <= ActiveCheckpointStage)
                return;

            ActiveCheckpointStage = stage;
            CaptureNotifySequence++;
        }

        public bool TryGetRespawnPose(out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;

            if (ActiveCheckpointStage <= 0)
                return false;

            for (int i = 0; i < _capturePoints.Count; i++)
            {
                var point = _capturePoints[i];
                if (point == null || point.Stage != ActiveCheckpointStage)
                    continue;

                point.GetRespawnPose(out position, out rotation);
                return true;
            }

            var scenePoints = FindObjectsOfType<CheckpointCapturePoint>();
            for (int i = 0; i < scenePoints.Length; i++)
            {
                var point = scenePoints[i];
                if (point == null || point.Stage != ActiveCheckpointStage)
                    continue;

                point.GetRespawnPose(out position, out rotation);
                return true;
            }

            return false;
        }

        private static bool TryResolveServerRunner(out NetworkRunner runner)
        {
            runner = null;

            foreach (var activeRunner in NetworkRunner.Instances)
            {
                if (activeRunner == null || !activeRunner.IsRunning || !activeRunner.IsServer)
                    continue;

                runner = activeRunner;
                return true;
            }

            return false;
        }
    }
}
