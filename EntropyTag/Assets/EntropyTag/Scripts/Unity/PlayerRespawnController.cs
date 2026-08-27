using System;
using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    [RequireComponent(typeof(ThirdPersonMotor))]
    public sealed class PlayerRespawnController : MonoBehaviour
    {
        [SerializeField]
        private Transform spawnPoint;

        [SerializeField]
        private float fallThreshold = -5f;

        private ThirdPersonMotor motor;

        public int RespawnCount { get; private set; }

        public Transform SpawnPoint => spawnPoint;

        public void Configure(Transform point, float threshold = -5f)
        {
            spawnPoint = point != null ? point : throw new ArgumentNullException(nameof(point));
            fallThreshold = threshold;
        }

        private void Awake()
        {
            motor = GetComponent<ThirdPersonMotor>();
        }

        private void Update()
        {
            if (spawnPoint == null || transform.position.y >= fallThreshold)
            {
                return;
            }

            motor.Respawn(spawnPoint.position, spawnPoint.rotation);
            RespawnCount++;
        }
    }
}
