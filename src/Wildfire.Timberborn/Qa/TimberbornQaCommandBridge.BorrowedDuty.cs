namespace Wildfire.Timberborn.Qa;

public sealed partial class TimberbornQaCommandBridge
{
    private readonly ITimberbornQaBorrowedDuty? _borrowedDuty;
    private TimberbornQaCommandResult ExecuteBorrowedDuty(string command, string commandText)
    {
        if (_borrowedDuty is null)
            return TimberbornQaCommandResult.CreateFailure(command, "borrowed_duty_unsupported", _stateProvider.GetState(), KnownCommands);
        var status = TimberbornQaBorrowedDutyCommands.Execute(commandText, _borrowedDuty);
        return TimberbornQaCommandResult.CreateSuccess(command, _stateProvider.GetState(), KnownCommands, status.Detail);
    }
}
