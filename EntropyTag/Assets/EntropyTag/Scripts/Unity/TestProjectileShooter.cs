using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    [DefaultExecutionOrder(200)]
    public sealed class TestProjectileShooter : MonoBehaviour
    {
        [SerializeField]
        private PlayerInputSource input;

        [SerializeField]
        private ThirdPersonAimSolver aimSolver;

        [SerializeField]
        private Transform muzzle;

        [SerializeField]
        private int poolSize = 24;

        [SerializeField]
        private float projectileSpeed = 20f;

        [SerializeField]
        private float projectileLifetime = 4f;

        [SerializeField]
        private float shotsPerSecond = 8f;

        private GameObject[] projectiles;
        private Rigidbody[] bodies;
        private float[] expiryTimes;
        private float nextShotTime;
        private int nextProjectileIndex;

        public int ActiveProjectileCount { get; private set; }

        public Vector3 LastFiredVelocity { get; private set; }

        public void Configure(PlayerInputSource inputSource, ThirdPersonAimSolver solver, Transform muzzleTransform)
        {
            input = inputSource;
            aimSolver = solver;
            muzzle = muzzleTransform;
        }

        public bool FireOnce()
        {
            if (aimSolver == null || muzzle == null || projectiles == null)
            {
                return false;
            }

            int projectileIndex = FindAvailableProjectile();

            if (projectileIndex < 0)
            {
                return false;
            }

            AimSolution aim = aimSolver.Current;
            Vector3 direction = aim.Point - muzzle.position;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = aim.Direction;
            }

            direction.Normalize();
            GameObject projectile = projectiles[projectileIndex];
            Rigidbody body = bodies[projectileIndex];
            projectile.transform.SetPositionAndRotation(muzzle.position, Quaternion.LookRotation(direction));
            projectile.SetActive(true);
            body.velocity = direction * projectileSpeed;
            expiryTimes[projectileIndex] = Time.unscaledTime + projectileLifetime;
            LastFiredVelocity = body.velocity;
            ActiveProjectileCount++;
            nextProjectileIndex = (projectileIndex + 1) % projectiles.Length;
            return true;
        }

        private void Awake()
        {
            CreatePool();
        }

        private void LateUpdate()
        {
            RecycleExpiredProjectiles();

            if (input != null && input.IsFiring && Time.unscaledTime >= nextShotTime && FireOnce())
            {
                nextShotTime = Time.unscaledTime + 1f / Mathf.Max(1f, shotsPerSecond);
            }
        }

        private void OnDestroy()
        {
            if (projectiles == null)
            {
                return;
            }

            for (int index = 0; index < projectiles.Length; index++)
            {
                if (projectiles[index] != null)
                {
                    Destroy(projectiles[index]);
                }
            }
        }

        private void CreatePool()
        {
            int count = Mathf.Max(1, poolSize);
            projectiles = new GameObject[count];
            bodies = new Rigidbody[count];
            expiryTimes = new float[count];
            CharacterController ownerController = GetComponent<CharacterController>();

            for (int index = 0; index < count; index++)
            {
                GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectile.name = $"Test Projectile {index + 1:00}";
                projectile.transform.localScale = Vector3.one * 0.2f;

                Rigidbody body = projectile.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                if (ownerController != null)
                {
                    Physics.IgnoreCollision(projectile.GetComponent<Collider>(), ownerController);
                }

                projectile.SetActive(false);
                projectiles[index] = projectile;
                bodies[index] = body;
            }
        }

        private int FindAvailableProjectile()
        {
            for (int offset = 0; offset < projectiles.Length; offset++)
            {
                int index = (nextProjectileIndex + offset) % projectiles.Length;

                if (!projectiles[index].activeSelf)
                {
                    return index;
                }
            }

            return -1;
        }

        private void RecycleExpiredProjectiles()
        {
            float currentTime = Time.unscaledTime;

            for (int index = 0; index < projectiles.Length; index++)
            {
                if (!projectiles[index].activeSelf || currentTime < expiryTimes[index])
                {
                    continue;
                }

                bodies[index].velocity = Vector3.zero;
                bodies[index].angularVelocity = Vector3.zero;
                projectiles[index].SetActive(false);
                ActiveProjectileCount--;
            }
        }
    }
}
