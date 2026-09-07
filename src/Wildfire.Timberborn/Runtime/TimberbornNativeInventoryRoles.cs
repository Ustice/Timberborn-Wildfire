using Timberborn.BaseComponentSystem;
using Timberborn.EntitySystem;
using Timberborn.GoodStackSystem;
using Timberborn.InventorySystem;
using Timberborn.RecoveredGoodSystem;
using Timberborn.SimpleOutputBuildings;
using Timberborn.Stockpiles;
using Timberborn.Workshops;

namespace Wildfire.Timberborn.Runtime;

// References are transient, only for this settled native observation. Persist immutable declarations, never these bindings.
internal sealed record TimberbornDeclaredInventory(TimberbornInventoryDeclaration Declaration, Inventory Inventory);

internal static class TimberbornNativeInventoryRoles
{
    internal sealed record Claim(TimberbornNativeInventoryRole Role, Inventory Inventory);

    // Caller owns the world observation guard and exact entity liveness/revalidation boundary.
    // This method never reads quantities, filters, recipes, reservations or Enabled.
    internal static IReadOnlyList<TimberbornDeclaredInventory> Capture(EntityComponent entity)
    {
        if (entity is null || !entity) throw new InvalidOperationException("Native inventory discovery requires a live entity reference.");
        var inventories = entity.GetComponentsAllocating<Inventory>().ToArray();
        var claims = new List<Claim>();
        Add(entity.GetComponentsAllocating<Stockpile>(), TimberbornNativeInventoryRole.Stockpile, role => role.Inventory);
        Add(entity.GetComponentsAllocating<SimpleOutputInventory>(), TimberbornNativeInventoryRole.SimpleOutput, role => role.Inventory);
        Add(entity.GetComponentsAllocating<GoodStack>(), TimberbornNativeInventoryRole.GoodStack, role => role.Inventory);
        Add(entity.GetComponentsAllocating<Manufactory>(), TimberbornNativeInventoryRole.Manufactory, role => role.Inventory);
        Add(entity.GetComponentsAllocating<RecoveredGoodStack>(), TimberbornNativeInventoryRole.RecoveredGoodStack, role => role.Inventory);
        var result = ValidateClaims(inventories, claims);
        foreach (var inventory in inventories)
            if (!inventory || !ReferenceEquals(inventory.GetComponent<EntityComponent>(), entity))
                throw new InvalidOperationException("Native inventory does not belong to the exact live body.");
        return result;

        void Add<T>(IEnumerable<T> components, TimberbornNativeInventoryRole role, Func<T, Inventory> inventory) where T : BaseComponent
        {
            foreach (var component in components)
            {
                if (component is null || !component || !ReferenceEquals(component.GetComponent<EntityComponent>(), entity))
                    throw new InvalidOperationException("Native inventory role does not belong to the exact live body.");
                claims.Add(new(role, inventory(component)));
            }
        }
    }

    // Validate the entire declared topology before a consumer is allowed to sum any physical material.
    internal static IReadOnlyList<TimberbornDeclaredInventory> ValidateClaims(IReadOnlyList<Inventory> inventories, IReadOnlyList<Claim> claims)
    {
        var owned = inventories.ToArray(); var declared = claims.ToArray();
        for (int i = 0; i < owned.Length; ++i)
            if (owned[i] is null || owned.Take(i).Any(value => ReferenceEquals(value, owned[i])))
                throw new InvalidOperationException("Native body contains a missing or repeated inventory reference.");
        var roles = new HashSet<TimberbornNativeInventoryRole>();
        var references = new List<Inventory>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<TimberbornDeclaredInventory>();
        foreach (var claim in declared)
        {
            if (claim is null || !roles.Add(claim.Role)) throw new InvalidOperationException("Native body has an ambiguous inventory role.");
            var inventory = claim.Inventory;
            if (inventory is null || !owned.Any(value => ReferenceEquals(value, inventory)))
                throw new InvalidOperationException("Native role lacks an inventory in its body's declared components.");
            if (references.Any(value => ReferenceEquals(value, inventory)))
                throw new InvalidOperationException("Native inventory is aliased by multiple roles.");
            var identity = new TimberbornInventoryDeclaration(claim.Role, inventory.ComponentName);
            if (!names.Add(identity.ComponentName)) throw new InvalidOperationException("Native body has duplicate inventory component names.");
            references.Add(inventory); result.Add(new(identity, inventory));
        }
        if (references.Count != owned.Length) throw new InvalidOperationException("Native body has an inventory with no supported owning role.");
        return Array.AsReadOnly(result.OrderBy(binding => binding.Declaration.Role).ToArray());
    }
}
