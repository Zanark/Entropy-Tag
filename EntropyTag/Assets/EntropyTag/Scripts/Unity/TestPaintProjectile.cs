using UnityEngine;

namespace EntropyTag.UnityAdapters
{
    public sealed class TestPaintProjectile : MonoBehaviour
    {
        private TestProjectileShooter owner;
        private int projectileIndex;

        public void Configure(TestProjectileShooter projectileOwner, int index)
        {
            owner = projectileOwner;
            projectileIndex = index;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (owner == null || collision.contactCount == 0)
            {
                return;
            }

            ContactPoint contact = collision.GetContact(0);
            owner.HandleProjectileImpact(projectileIndex, collision.collider, contact.point, contact.normal);
        }
    }
}
