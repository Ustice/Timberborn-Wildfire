using Timberborn.BaseComponentSystem;
using Timberborn.BehaviorSystem;
using Timberborn.CharacterModelSystem;
using Timberborn.Characters;
using Timberborn.EntitySystem;
using Timberborn.MortalSystem;
using Timberborn.TemplateAttachmentSystem;
using UnityEngine;

namespace Wildfire.Timberborn.FireResponse.Presentation;

/// <summary>One additive helmet. Pose is provisional pending native adult fit validation.</summary>
public sealed class WardenHelmetPresentation : BaseComponent, IAwakableComponent,
    IPostInitializableEntity, ILateUpdatableComponent, IDeletableEntity
{
    public const string AttachmentId = "Wildfire.WardenHelmet.IronTeeth";
    public const string ParentName = "#Head";
    private WardenHelmetVisibility? _visibility;
    private TemplateAttachments _attachments = null!;
    private CharacterModel _model = null!;
    private BehaviorManager _manager = null!;
    private WardenExecutor _warden = null!;
    private Mortal _mortal = null!;
    private Character? _character;
    private Guid _entityId;
    private bool _settled;
    private bool _exited;
    private bool _failed;

    public void Awake()
    {
        try
        {
            _entityId = GetComponent<EntityComponent>().EntityId;
            _attachments = GetComponent<TemplateAttachments>();
            _model = GetComponent<CharacterModel>();
            _manager = GetComponent<BehaviorManager>();
            _warden = GetComponent<WardenExecutor>();
            _mortal = GetComponent<Mortal>();
            _character = GetComponent<Character>();
            if (!_attachments || !_model || !_manager || !_warden || !_mortal || !_character)
                throw new InvalidOperationException("Required native adult presentation components are absent.");
            _visibility = new WardenHelmetVisibility(CreateHelmet, ReportFailure);
            _character.Died += OnDied;
        }
        catch (Exception error) { ReportFailure(error); }
    }

    public void PostInitializeEntity() => _settled = true;

    public void LateUpdate()
    {
        if (!_settled || _failed || _exited) return;
        // Frame updates also run in a paused loaded world. An interrupted Warden need
        // not Tick or call Finish again, so phase alone cannot keep equipment visible.
        try
        {
            bool alive = _manager && _warden && _mortal && !_mortal.Dead && !_mortal.ShouldDie;
            _visibility?.Observe(alive && _manager.IsRunningExecutor<WardenExecutor>(), _warden ? _warden.Phase : WardenPhase.Idle, alive);
        }
        catch (Exception error)
        {
            _visibility?.Observe(false, WardenPhase.Idle, false);
            ReportFailure(error);
        }
    }

    private TemplateAttachmentVisibilityToggle CreateHelmet()
    {
        var heads = GameObject.GetComponentsInChildren<Transform>(true)
            .Where(transform => transform.name == ParentName).ToArray();
        if (heads.Length != 1 || !_model.Model || !heads[0].IsChildOf(_model.Model))
            throw new InvalidOperationException("Expected exactly one #Head below the native character model.");
        var attachment = _attachments.GetOrCreateAttachment(AttachmentId);
        if (attachment.Transform.parent != heads[0])
            throw new InvalidOperationException("Native helmet attachment did not bind to the verified #Head.");
        if (!attachment.GameObject.GetComponentsInChildren<Renderer>(true).Any())
            throw new InvalidOperationException("Native helmet import produced no renderers.");
        var toggle = attachment.GetVisibilityToggle();
        toggle.Hide();
        return toggle;
    }

    private void OnDied(object sender, EventArgs args) => HideForExit();

    public void DeleteEntity()
    {
        if (_character is not null) _character.Died -= OnDied;
        HideForExit();
    }

    private void HideForExit()
    {
        _exited = true;
        _visibility?.Observe(false, WardenPhase.Idle, false);
    }

    private void ReportFailure(Exception error)
    {
        if (_failed) return;
        _failed = true;
        Debug.LogWarning($"wildfire_warden_helmet status=unavailable beaver={_entityId} attachment={AttachmentId} parent={ParentName} error={error}");
    }
}
