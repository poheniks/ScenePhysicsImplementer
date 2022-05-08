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
        public Vec3 UserLookDirection = Vec3.Zero;
        public bool ShowEditorHelpers = true;
        public string DescriptionText = "";
        public Vec2 movementInputVector { get; private set; }

        private Vec3 targetUserLocation;
        private Vec3 targetUserLookDirection;

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
                SetUserAgentFrame();
            }
        }

        public virtual void SetUserAgentFrame()
        {
            if (LockUserFrames)
            {
                UpdateUserTargetFrame();
                UserAgent.TeleportToPosition(targetUserLocation);
                UserAgent.SetTargetPositionAndDirection(GameEntity.GlobalPosition.AsVec2, targetUserLookDirection);
            }
        }

        private void UpdateUserTargetFrame()
        {
            Mat3 parentEntityMat = GameEntity.GetGlobalFrame().rotation;
            parentEntityMat.MakeUnit();

            targetUserLocation = GameEntity.GlobalPosition + parentEntityMat.TransformToParent(UseLocationOffset);

            parentEntityMat.ApplyEulerAngles(UserLookDirection * (float)MathLib.DegtoRad);
            targetUserLookDirection = parentEntityMat.f;
            
        }

        public virtual void RenderEditorHelpers()
        {
            UpdateUserTargetFrame();
            MBDebug.RenderDebugDirectionArrow(targetUserLocation, targetUserLookDirection, Colors.Magenta.ToUnsignedInteger());
        }
    }
}
