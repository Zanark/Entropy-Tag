using Unity.Profiling;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public sealed class PlayerSandboxDiagnostics : MonoBehaviour
    {
        private ProfilerRecorder gcAllocations;
        private ProfilerRecorder mainThreadTime;

        public long LastGcAllocatedBytes => gcAllocations.Valid ? (long)gcAllocations.LastValue : 0L;

        public double LastMainThreadMilliseconds =>
            mainThreadTime.Valid ? mainThreadTime.LastValue * (1d / 1000000d) : 0d;

        public bool IsRecording => gcAllocations.Valid && mainThreadTime.Valid;

        private void OnEnable()
        {
            gcAllocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            mainThreadTime = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        }

        private void OnDisable()
        {
            gcAllocations.Dispose();
            mainThreadTime.Dispose();
        }
    }
}
