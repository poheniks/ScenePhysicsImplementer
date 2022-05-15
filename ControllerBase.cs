using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Localization;

namespace ScenePhysicsImplementer
{
    public abstract class ControllerBase : UsableMissionObject
    {
        public Vec3 UseLocationOffset = Vec3.Zero;
        public Vec3 UseLookDirection = Vec3.Zero;
        public bool ShowEditorHelpers = true;
        public string DescriptionText = "";
        public Vec2 movementInputVector { get; private set; }

        public Vec3 targetUseLocation { get; private set; }
        public Vec3 targetUseLookDirection { get; private set; }

        public abstract ActionIndexCache SetUserAnimation();

        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.TickParallel;
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            return DescriptionText;
        }

        protected override void OnEditorInit()
        {
            base.OnEditorInit();
            Initialize();
        }

        protected override void OnInit()
        {
            base.OnInit();
            Initialize();

        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            LockUserFrames = true;
            userAgent.SetActionChannel(0, SetUserAnimation(), ignorePriority: true);
        }

        public override void OnUseStopped(Agent userAgent, bool isSuccessful, int preferenceIndex)
        {
            userAgent.SetActionChannel(0, ActionIndexCache.act_none, ignorePriority: true);
            userAgent.ClearTargetFrame();
            base.OnUseStopped(userAgent, isSuccessful, preferenceIndex);
        }

        public virtual void Initialize()
        {
            base.SetScriptComponentToTick(this.GetTickRequirement());
        }

        protected override void OnEditorTick(float dt)
        {
            base.OnEditorTick(dt);
            if (GameEntity.IsSelectedOnEditor() && ShowEditorHelpers) RenderEditorHelpers(); 
        }

        protected override void OnTickParallel(float dt)
        {
            base.OnTickParallel(dt);
            if (UserAgent != null)
            {
                movementInputVector = UserAgent.MovementInputVector;
                if (LockUserFrames) SetUserAgentFrame(UserAgent);
            }
        }

        public virtual void SetUserAgentFrame(Agent agent)
        {
            UpdateUserTargetFrame(UseLocationOffset, UseLookDirection);
            agent.TeleportToPosition(targetUseLocation);
            agent.SetTargetPositionAndDirection(GameEntity.GlobalPosition.AsVec2, targetUseLookDirection);
        }

        public void UpdateUserTargetFrame(Vec3 localOffsetPos, Vec3 localOffsetDir)
        {
            Mat3 parentEntityMat = GameEntity.GetGlobalFrame().rotation;
            parentEntityMat.MakeUnit();

            targetUseLocation = GameEntity.GlobalPosition + parentEntityMat.TransformToParent(localOffsetPos);

            parentEntityMat.ApplyEulerAngles(localOffsetDir * (float)MathLib.DegtoRad);
            targetUseLookDirection = parentEntityMat.f;
        }

        public virtual void RenderEditorHelpers()
        {
            UpdateUserTargetFrame(UseLocationOffset, UseLookDirection);
            MBDebug.RenderDebugDirectionArrow(targetUseLocation, targetUseLookDirection, Colors.Magenta.ToUnsignedInteger());
        }
    }
}
