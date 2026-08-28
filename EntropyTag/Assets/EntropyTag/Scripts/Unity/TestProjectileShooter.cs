using EntropyTag.Domain;
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
        private TerritorySurface territorySurface;

        [SerializeField]
        private ElementReactionPresentationConfig presentationConfig;

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
        private Renderer[] projectileRenderers;
        private ElementId[] projectileElements;
        private float[] expiryTimes;
        private GameObject[] splats;
        private Renderer[] splatRenderers;
        private float nextShotTime;
        private int nextProjectileIndex;
        private int nextSplatIndex;

        public int ActiveProjectileCount { get; private set; }

        public Vector3 LastFiredVelocity { get; private set; }

        public int SplatCount { get; private set; }

        public ElementId CurrentElement { get; private set; } = ElementId.Ice;

        public TeamId CurrentTeamId =>
            CurrentElement == ElementId.Ice ? new TeamId(1) : new TeamId(2);

        public void Configure(
            PlayerInputSource inputSource,
            ThirdPersonAimSolver solver,
            Transform muzzleTransform,
            TerritorySurface paintSurface = null,
            ElementReactionPresentationConfig reactions = null)
        {
            input = inputSource;
            aimSolver = solver;
            muzzle = muzzleTransform;
            territorySurface = paintSurface;
            presentationConfig = reactions;
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
            projectileElements[projectileIndex] = CurrentElement;
            projectileRenderers[projectileIndex].material.color = GetElementColor(CurrentElement);
            projectile.SetActive(true);
            body.velocity = direction * projectileSpeed;
            expiryTimes[projectileIndex] = Time.unscaledTime + projectileLifetime;
            LastFiredVelocity = body.velocity;
            ActiveProjectileCount++;
            nextProjectileIndex = (projectileIndex + 1) % projectiles.Length;
            return true;
        }

        public void HandleProjectileImpact(
            int projectileIndex,
            Collider impactCollider,
            Vector3 point,
            Vector3 normal)
        {
            if (projectiles == null ||
                projectileIndex < 0 ||
                projectileIndex >= projectiles.Length ||
                !projectiles[projectileIndex].activeSelf)
            {
                return;
            }

            ElementId element = projectileElements[projectileIndex];

            if (TerritorySurfaceRegistry.TryGet(
                    impactCollider,
                    point,
                    normal,
                    out TerritorySurface impactSurface))
            {
                impactSurface.ApplyWorldStamp(
                    point,
                    splatSize,
                    element,
                    GetTeamId(element),
                    normal);
            }
            else if (territorySurface != null &&
                     territorySurface.TryWorldToCoordinate(point, out _))
            {
                territorySurface.ApplyWorldStamp(
                    point,
                    splatSize,
                    element,
                    GetTeamId(element),
                    normal);
            }

            PlaceSplat(point, normal, element);
            RecycleProjectile(projectileIndex);
        }

        public void SwitchElement()
        {
            CurrentElement = CurrentElement == ElementId.Ice ? ElementId.Fire : ElementId.Ice;
        }

        public void ResetTestPaint()
        {
            TerritorySurfaceRegistry.ResetAll();

            if (splats == null)
            {
                return;
            }

            for (int index = 0; index < splats.Length; index++)
            {
                splats[index].SetActive(false);
            }

            SplatCount = 0;
            nextSplatIndex = 0;
        }

        private void Awake()
        {
            CreatePool();
            CreateSplatPool();
        }

        private void LateUpdate()
        {
            RecycleExpiredProjectiles();

            if (input != null && input.WasSwitchElementPressedThisFrame)
            {
                SwitchElement();
            }

            if (input != null && input.WasResetTerritoryPressedThisFrame)
            {
                ResetTestPaint();
            }

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
            projectileRenderers = new Renderer[count];
            projectileElements = new ElementId[count];
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
                projectileRenderers[index] = projectile.GetComponent<Renderer>();
            }
        }

        private void CreateSplatPool()
        {
            int count = Mathf.Max(1, splatPoolSize);
            splats = new GameObject[count];
            splatRenderers = new Renderer[count];

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
                splatRenderers[index] = renderer;
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

        private void PlaceSplat(Vector3 point, Vector3 normal, ElementId element)
        {
            GameObject splat = splats[nextSplatIndex];
            splat.transform.SetPositionAndRotation(
                point + normal * 0.0125f,
                Quaternion.FromToRotation(Vector3.up, normal));
            splatRenderers[nextSplatIndex].material.color = GetElementColor(element);
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

        private static TeamId GetTeamId(ElementId element)
        {
            switch (element)
            {
                case ElementId.Ice:
                    return new TeamId(1);
                case ElementId.Fire:
                    return new TeamId(2);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(element), element, null);
            }
        }

        private Color GetElementColor(ElementId element)
        {
            if (presentationConfig != null)
            {
                return presentationConfig.GetElement(element).ProjectileColor;
            }

            return element == ElementId.Ice
                ? new Color(0.05f, 0.85f, 1f)
                : new Color(1f, 0.2f, 0.05f);
        }
    }
}
