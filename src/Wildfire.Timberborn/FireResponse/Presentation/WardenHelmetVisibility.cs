using Timberborn.TemplateAttachmentSystem;

namespace Wildfire.Timberborn.FireResponse.Presentation;

/// <summary>Transient presentation state; native executor ownership remains the only duty authority.</summary>
internal sealed class WardenHelmetVisibility
{
    private readonly Func<TemplateAttachmentVisibilityToggle> _create;
    private readonly Action<Exception> _reportFailure;
    private TemplateAttachmentVisibilityToggle? _toggle;
    private bool _visible;
    private bool _failed;

    internal WardenHelmetVisibility(Func<TemplateAttachmentVisibilityToggle> create, Action<Exception> reportFailure)
    {
        _create = create;
        _reportFailure = reportFailure;
    }

    internal void Observe(bool ownsExecutor, WardenPhase phase, bool alive)
    {
        if (_failed) return;
        bool visible = ownsExecutor && alive && phase is WardenPhase.Fetching or WardenPhase.Approaching or
            WardenPhase.Applying or WardenPhase.AwaitingApplication or WardenPhase.Returning;
        if (visible == _visible) return;
        try
        {
            _toggle ??= _create();
            if (visible) _toggle.Show();
            else _toggle.Hide();
            _visible = visible;
        }
        catch (Exception error)
        {
            _failed = true;
            // Rendering callbacks can fail after changing native visibility. Never retry an
            // uncertain creation every frame, and never let presentation abort worker logic.
            try { _toggle?.Hide(); }
            catch { /* Keep the original diagnostic; this component will remain disabled. */ }
            _visible = false;
            _reportFailure(error);
        }
    }
}
