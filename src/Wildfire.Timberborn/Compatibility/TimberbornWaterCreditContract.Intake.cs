using System.Reflection;
using Timberborn.WaterBuildings;
using Timberborn.WaterSystem;

namespace Wildfire.Timberborn.Compatibility;

internal sealed partial class TimberbornWaterCreditContract
{
    private readonly FieldInfo _inputWaterService, _inputMap, _inputCreditService, _serviceChanges;
    private readonly Type _waterServiceType, _mapType;
    internal float WaterUnit { get; }
    // Transient identities for one synchronous operation, never saved or used to claim a quantity.
    internal readonly record struct CleanIntake(WaterInput Input, IWaterService Service,
        IThreadSafeWaterMap Map, TimberbornWaterCreditContract Contract);

    private static float ReadNativeWaterUnit()
    {
        var converter = Type.GetType("Timberborn.WaterWorkshops.WaterGoodToWaterAmountConverter, Timberborn.WaterWorkshops", true)!;
        var method = converter.GetMethod("GetWaterAmount", BindingFlags.Public | BindingFlags.Static)!;
        var specType = method.GetParameters().Single().ParameterType.GenericTypeArguments.Single();
        if (specType.FullName != "Timberborn.Goods.GoodAmountSpec") throw new NotSupportedException("Unexpected native Water converter.");
        var spec = Activator.CreateInstance(specType)!;
        specType.GetProperty("Id")!.SetValue(spec, "Water");
        specType.GetProperty("Amount")!.SetValue(spec, 1);
        var amounts = Array.CreateInstance(specType, 1); amounts.SetValue(spec, 0);
        var quantum = (float)method.Invoke(null, new object[] { amounts })!;
        return float.IsFinite(quantum) && quantum > 0 ? quantum : throw new NotSupportedException("Invalid native Water quantum.");
    }
    internal (float Clean, float Dirty) Buffer(WaterInput input) => ((float)_clean.GetValue(input)!, (float)_dirty.GetValue(input)!);
    internal bool TryCaptureCleanIntake(WaterInput input, WaterInputService creditService, out CleanIntake intake)
    {
        intake = default;
        if (_inputWaterService.GetValue(input) is not IWaterService service || service.GetType() != _waterServiceType ||
            _inputMap.GetValue(input) is not IThreadSafeWaterMap map || map.GetType() != _mapType) return false;
        var candidate = new CleanIntake(input, service, map, this);
        if (!MatchesCleanIntake(candidate, creditService)) return false;
        intake = candidate;
        return true;
    }
    internal bool MatchesCleanIntake(CleanIntake intake, WaterInputService creditService)
    {
        if (!ReferenceEquals(intake.Contract, this) || !Fixed(intake.Input) || !KnownService(creditService) ||
            !ReferenceEquals(_inputCreditService.GetValue(intake.Input), creditService) ||
            !ReferenceEquals(_inputWaterService.GetValue(intake.Input), intake.Service) ||
            !ReferenceEquals(_inputMap.GetValue(intake.Input), intake.Map) ||
            !ReferenceEquals(_serviceChanges.GetValue(intake.Service), _removal.GetValue(creditService)) || !ValidBuffer(intake.Input)) return false;
        // Prototype common positive case only. This does not choose the shipping mixed-water policy.
        float contamination = intake.Input.ContaminationPercentage;
        return intake.Input.IsUnderwater && float.IsFinite(contamination) && contamination == 0;
    }
}
