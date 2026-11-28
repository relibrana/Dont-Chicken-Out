using UnityEngine;

/// <summary>
/// What a blast (bomb, hot potato) does to the items around it (GDD, nov
/// 2026): gift capsules break and their item is lost, tires break, and a
/// bomb still charging breaks without going off. Pieces and chickens are
/// handled by each explosion itself.
/// </summary>
public static class ExplosionBlast
{
    private static readonly Collider2D[] Hits = new Collider2D[64];

    public static void BreakItems(Vector2 centre, float radius, Object source)
    {
        var filter = new ContactFilter2D { useTriggers = true };
        filter.NoFilter();
        int count = Physics2D.OverlapCircle(centre, radius, filter, Hits);

        for (int i = 0; i < count; i++)
        {
            Collider2D col = Hits[i];
            if (col == null) continue;

            ItemCapsule capsule = col.GetComponentInParent<ItemCapsule>();
            if (capsule != null && capsule.gameObject.activeInHierarchy)
            {
                capsule.BreakLost();
                continue;
            }

            Tire tire = col.GetComponentInParent<Tire>();
            if (tire != null && tire.gameObject.activeInHierarchy && !ReferenceEquals(tire, source))
            {
                tire.Break();
                continue;
            }

            BombItem bomb = col.GetComponentInParent<BombItem>();
            if (bomb != null && !ReferenceEquals(bomb, source))
                bomb.Defuse();
        }
    }

    /// <summary>Pollo metálico: blasts and the anvil do not eliminate it (GDD).</summary>
    public static bool IsMetal(PlayerController player) =>
        player != null && player.TryGetComponent(out MetalChickenState metal) && metal.IsActive;
}
