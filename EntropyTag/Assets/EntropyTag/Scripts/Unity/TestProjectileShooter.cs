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

        [SerializeField]
        private int splatPoolSize = 96;

        [SerializeField]
        private float splatSize = 0.65f;

        private GameObject[] projectiles;
        private Rigidbody[] bodies;
        private float[] expiryTimes;
        private GameObject[] splats;
        private float nextShotTime;
        private int nextProjectileIndex;
        private int nextSplatIndex;

        public int ActiveProjectileCount { get; private set; }

        public Vector3 LastFiredVelocity { get; private set; }

        public int SplatCount { get; private set; }

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

        public void HandleProjectileImpact(int projectileIndex, Vector3 point, Vector3 normal)
        {
            if (projectiles == null ||
                projectileIndex < 0 ||
                projectileIndex >= projectiles.Length ||
                !projectiles[projectileIndex].activeSelf)
            {
                return;
            }

            PlaceSplat(point, normal);
            RecycleProjectile(projectileIndex);
        }

        private void Awake()
        {
            CreatePool();
            CreateSplatPool();
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

            if (splats == null)
            {
                return;
            }

            for (int index = 0; index < splats.Length; index++)
            {
                if (splats[index] != null)
                {
                    Destroy(splats[index]);
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
                projectile.layer = LayerMask.NameToLayer("Ignore Raycast");

                Rigidbody body = projectile.AddComponent<Rigidbody>();
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                projectile.AddComponent<TestPaintProjectile>().Configure(this, index);

                if (ownerController != null)
                {
                    Physics.IgnoreCollision(projectile.GetComponent<Collider>(), ownerController);
                }

                projectile.SetActive(false);
                projectiles[index] = projectile;
                bodies[index] = body;
            }
        }

        private void CreateSplatPool()
        {
            int count = Mathf.Max(1, splatPoolSize);
            splats = new GameObject[count];

            for (int index = 0; index < count; index++)
            {
                GameObject splat = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                splat.name = $"Test Paint Splat {index + 1:00}";
                splat.layer = LayerMask.NameToLayer("Ignore Raycast");
                splat.transform.localScale = new Vector3(splatSize, 0.025f, splatSize);
                Destroy(splat.GetComponent<Collider>());

                Renderer renderer = splat.GetComponent<Renderer>();
                renderer.material.color = new Color(0.05f, 0.85f, 1f);

                splat.SetActive(false);
                splats[index] = splat;
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

                RecycleProjectile(index);
            }
        }

        private void PlaceSplat(Vector3 point, Vector3 normal)
        {
            GameObject splat = splats[nextSplatIndex];
            splat.transform.SetPositionAndRotation(
                point + normal * 0.0125f,
                Quaternion.FromToRotation(Vector3.up, normal));
            splat.SetActive(true);
            nextSplatIndex = (nextSplatIndex + 1) % splats.Length;
            SplatCount = Mathf.Min(SplatCount + 1, splats.Length);
        }

        private void RecycleProjectile(int index)
        {
            bodies[index].velocity = Vector3.zero;
            bodies[index].angularVelocity = Vector3.zero;
            projectiles[index].SetActive(false);
            ActiveProjectileCount--;
        }
    }
}
