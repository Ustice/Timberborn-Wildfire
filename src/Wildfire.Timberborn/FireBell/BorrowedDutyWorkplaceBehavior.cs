using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.WorkSystem;

namespace Wildfire.Timberborn.FireBell;

public sealed class BorrowedDutyWorkplaceBehavior : WorkplaceBehavior, IAwakableComponent, IUpdatableComponent
{
    private readonly BorrowedDutyFixture _fixture;
    private readonly BorrowedDutyWorkplaceOrder _order = new();
    private Workplace _workplace = null!;
    public BorrowedDutyWorkplaceBehavior(BorrowedDutyFixture fixture) => _fixture = fixture;
    public void Awake() => _workplace = GetComponent<Workplace>();
    public void Update() => _order.Update(_workplace, this, _fixture.IsArmedAt(_workplace));
    public override Decision Decide(BehaviorAgent agent)
    {
        var executor = agent.GetComponent<BorrowedDutyExecutor>();
        if (executor is null || !_fixture.TryOffer(_workplace, executor)) return Decision.ReleaseNow();
        _order.Update(_workplace, this, false);
        return agent.GetComponent<BorrowedDutyBehavior>().Own(executor);
    }
}
