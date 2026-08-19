using EntropyTag.Application;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public sealed class ProjectBootstrap : MonoBehaviour
    {
        public StartupState State { get; private set; }

        private void Awake()
        {
            State = StartupState.CreateDefault();
        }
    }
}
