namespace Wildfire.Timberborn.Consequences;

public sealed partial class TimberbornOwnedDeltaConsumer
{
    /// <summary>
    /// Initial witnessed construction only. The caller supplies a new body's chosen accounting and a
    /// settled native capture; this does not infer a material budget or migrate historical evidence.
    /// </summary>
    public static TimberbornOwnedDeltaConsumer CreateWithNativeDefinitions(TimberbornNativeMaterialRegistry origins,
        TimberbornBurnDamageService damage,TimberbornOwnedNativeEffects effects,
        Wildfire.Timberborn.Resources.INativeResourceMutationGuard guard,IReadOnlyList<TimberbornInitialMaterialBody> bodies,
        TimberbornResourceFuelCatalog? catalog=null)=>guard.CaptureAtRest(()=>
        {
            if(damage.States.Values.Any(state=>state.DamageTaken!=0 || state.LastDamagedTick!=0))
                throw new ArgumentException("Native formation cannot manufacture evidence for an already damaged body.");
            var consumer=new TimberbornOwnedDeltaConsumer(origins,damage,effects,guard,Array.Empty<TimberbornOwnedBodyRegistration>(),catalog);
            foreach(var body in bodies)
            {
                if(body.Family is not {} family || !effects.Bodies.IsLive(body.EntityId))
                    throw new ArgumentException("Witnessed formation requires an exact supported live native body.");
                var key=new TimberbornBurnDamageTargetKey(TimberbornBurnDamageIdentity.ForEntity(body.EntityId,family));
                if(!damage.TryGetState(key,out var state) || state.SpecId!=body.SpecId || state.TargetKind!=body.PhysicalBodyKind ||
                    !MatchesFamily(family,state)) throw new ArgumentException("Native facts do not match their chosen body accounting.");
                consumer._origins.Register(body.EntityId,family,key);
            }
            var definitions=new OwnedNativeDefinitionSet(bodies.Select(OwnedNativeDefinitionWitness.Capture));
            var owners=consumer._origins.Capture(damage);
            definitions.Validate(owners);
            if(owners.Length!=damage.States.Count) throw new ArgumentException("Witnessed formation must account for every body.");
            consumer._nativeDefinitions=definitions;
            return consumer;
        });
}
