using System;
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

        [SerializeField] private MonoBehaviour alternateInput;
        [SerializeField] private MonoBehaviour alternateAim;
        [SerializeField] private ElementId startingElement = ElementId.Ice;
        [SerializeField] private bool fixedElement;
        private IActorIntentSource actorInput;
        private IAimSource actorAim;
        private ElementId? selectedElement;
        private bool canSwitchElement = true;

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
        private Collider[] projectileColliders;
        private Vector3[] projectileDirections;
        private CharacterController ownerController;
        private Renderer[] projectileRenderers;
        private ElementId[] projectileElements;
        private float[] expiryTimes;
        private GameObject[] splats;
        private Renderer[] splatRenderers;
        private float nextShotTime;
        private int nextProjectileIndex;
        private int nextSplatIndex;
        private MatchFlowController match;

        public int ActiveProjectileCount { get; private set; }

        public Vector3 LastFiredVelocity { get; private set; }

        public int SplatCount { get; private set; }

        public bool CanFire { get; private set; } = true;

        public bool CanSwitchElement => canSwitchElement && !fixedElement;

        public bool CanResetPaint { get; private set; } = true;

        public ElementId CurrentElement => selectedElement ?? startingElement;

        public float ShotsPerSecond => shotsPerSecond;

        public float ProjectileSpeed => projectileSpeed;

        public Vector3 MuzzlePosition => muzzle.position;

        public int ShotsFired { get; private set; }

        public int EnemyHits { get; private set; }

        public MatchParticipant LastHitTarget { get; private set; }

        public bool ReadyToFire => CanFire && projectiles != null &&
                                   Time.time >= nextShotTime && ActiveProjectileCount < projectiles.Length;

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
            alternateInput = null;
            alternateAim = null;
            actorInput = input;
            actorAim = aimSolver;
        }

        public void ConfigureIntent(
            MonoBehaviour source, MonoBehaviour aim, Transform muzzleTransform, ElementId element,
            TerritorySurface paintSurface, ElementReactionPresentationConfig reactions)
        {
            if (!(source is IActorIntentSource intent) || !(aim is IAimSource aiming))
            {
                throw new ArgumentException("A shooter requires IActorIntentSource and IAimSource adapters.");
            }

            GetTeamId(element);
            input = null;
            aimSolver = null;
            alternateInput = source;
            alternateAim = aim;
            actorInput = intent;
            actorAim = aiming;
            muzzle = muzzleTransform != null ? muzzleTransform : throw new ArgumentNullException(nameof(muzzleTransform));
            territorySurface = paintSurface;
            presentationConfig = reactions;
            startingElement = element;
            selectedElement = null;
            fixedElement = true;
        }

        public bool TryFire()
        {
            if (!ReadyToFire || !FireOnce())
            {
                return false;
            }

            nextShotTime = Time.time + 1f / Mathf.Max(1f, shotsPerSecond);
            return true;
        }

        public bool FireOnce()
        {
            if (!CanFire || actorAim == null || muzzle == null || projectiles == null)
            {
                return false;
            }

            int projectileIndex = FindAvailableProjectile();

            if (projectileIndex < 0)
            {
                return false;
            }

            AimSolution aim = actorAim.Current;
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
            projectileDirections[projectileIndex] = direction;
            projectileRenderers[projectileIndex].material.color = GetElementColor(CurrentElement);
            projectile.SetActive(true);
            if (ownerController != null)
            {
                Physics.IgnoreCollision(projectileColliders[projectileIndex], ownerController);
            }

            body.velocity = direction * projectileSpeed;
            expiryTimes[projectileIndex] = Time.time + projectileLifetime;
            LastFiredVelocity = body.velocity;
            ActiveProjectileCount++;
            ShotsFired++;
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

            if (!CanFire || (match != null && !match.CanPaintAt(point)))
            {
                RecycleProjectile(projectileIndex);
                return;
            }

            CircularArenaBoundary? boundary = match != null ? match.PaintBoundary : null;
            MatchParticipant actor = impactCollider != null
                ? impactCollider.GetComponentInParent<MatchParticipant>()
                : null;
            if (actor != null)
            {
                if (actor.TryReceiveHit(GetTeamId(element), projectileDirections[projectileIndex]))
                {
                    EnemyHits++;
                    LastHitTarget = actor;
                }

                RecycleProjectile(projectileIndex);
                return;
            }

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
                    normal,
                    boundary);
            }
            else if (territorySurface != null &&
                     territorySurface.TryWorldToCoordinate(point, out _))
            {
                territorySurface.ApplyWorldStamp(
                    point,
                    splatSize,
                    element,
                    GetTeamId(element),
                    normal,
                    boundary);
            }

            PlaceSplat(point, normal, element);
            RecycleProjectile(projectileIndex);
        }

        public void SwitchElement()
        {
            if (!CanSwitchElement)
            {
                return;
            }

            selectedElement = CurrentElement == ElementId.Ice ? ElementId.Fire : ElementId.Ice;
        }

        public void AttachMatch(MatchFlowController controller)
        {
            match = controller;
        }

        public void SetControlPolicy(bool canFire, bool canSwitchElement, bool canResetPaint)
        {
            CanFire = canFire;
            this.canSwitchElement = canSwitchElement;
            CanResetPaint = canResetPaint;
        }

        public void ResetTestPaint()
        {
            if (!CanResetPaint)
            {
                return;
            }

            TerritorySurfaceRegistry.ResetAll();
            ClearTransientPaint();
        }

        public void ClearProjectiles()
        {
            if (projectiles == null)
            {
                return;
            }

            for (int index = 0; index < projectiles.Length; index++)
            {
                if (projectiles[index].activeSelf)
                {
                    RecycleProjectile(index);
                }
            }

            nextProjectileIndex = 0;
            nextShotTime = 0f;
            LastFiredVelocity = Vector3.zero;
        }

        public void ClearTransientPaint()
        {
            ClearProjectiles();

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

        public void ResetStatistics()
        {
            ShotsFired = 0;
            EnemyHits = 0;
            LastHitTarget = null;
        }

        private void Awake()
        {
            GetTeamId(startingElement);
            if ((alternateInput != null && !(alternateInput is IActorIntentSource)) ||
                (alternateAim != null && !(alternateAim is IAimSource)))
            {
                throw new InvalidOperationException($"{name}: invalid shooter intent or aim adapter.");
            }

            actorInput = alternateInput != null ? (IActorIntentSource)alternateInput : input;
            actorAim = alternateAim != null ? (IAimSource)alternateAim : aimSolver;
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

            if (CanResetPaint && input != null && input.WasResetTerritoryPressedThisFrame)
            {
                ResetTestPaint();
            }

            if (actorInput != null && actorInput.IsFiring)
            {
                TryFire();
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
            projectileColliders = new Collider[count];
            projectileDirections = new Vector3[count];
            projectileRenderers = new Renderer[count];
            projectileElements = new ElementId[count];
            expiryTimes = new float[count];
            ownerController = GetComponent<CharacterController>();

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
                projectileColliders[index] = projectile.GetComponent<Collider>();
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
            float currentTime = Time.time;

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
