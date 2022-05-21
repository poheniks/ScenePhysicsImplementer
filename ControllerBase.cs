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
    //probably needs work to be extendable-friendly
    public abstract class ControllerBase : UsableMissionObject
    {
        //editor fields
        public Vec3 UserLocationOffset = Vec3.Zero;
        public Vec3 UserLookDirection = Vec3.Zero;
        public bool ShowEditorHelpers = true;
        public string DescriptionText = "";
        public SimpleButton ShowHelpText;   //for formatting purposes in the scene editor, place buttons as the last fields

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

        protected override void OnEditorVariableChanged(string variableName)
        {
            base.OnEditorVariableChanged(variableName);
            if (variableName == nameof(ShowHelpText)) DisplayHelpText();
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
            UpdateUserTargetFrame(UserLocationOffset, UserLookDirection);
            agent.SetInitialFrame(targetUseLocation, targetUseLookDirection.AsVec2);
            //use SetInitialFrame instead of the inherited implementation SetTargetPositionAndDirection; AFAIK, SetInitialFrame is the only method that can change an agent's look direction
        }

        public void UpdateUserTargetFrame(Vec3 localOffsetPos, Vec3 localOffsetDir)
        {
            Mat3 parentEntityMat = GameEntity.GetGlobalFrame().rotation;
            parentEntityMat.MakeUnit();

            targetUseLocation = GameEntity.GlobalPosition + parentEntityMat.TransformToParent(localOffsetPos);  //convert to global coordinates

            parentEntityMat.ApplyEulerAngles(localOffsetDir * (float)MathLib.DegtoRad);
            targetUseLookDirection = parentEntityMat.f;
        }

        public virtual void RenderEditorHelpers()
        {
            UpdateUserTargetFrame(UserLocationOffset, UserLookDirection);
            MBDebug.RenderDebugDirectionArrow(targetUseLocation, targetUseLookDirection, Colors.Magenta.ToUnsignedInteger());
        }

        public virtual void DisplayHelpText()
        {
            MathLib.HelpText(nameof(DescriptionText), "Text displayed in-game when a player looks at this controller");
            MathLib.HelpText(nameof(ShowEditorHelpers), "Renders lines & arrows to the use location/direction, other constraint scripts this controller interacts with, etc. Only appears in the editor");
            MathLib.HelpText(nameof(UserLookDirection), "Changes the direction of the user");
            MathLib.HelpText(nameof(UserLocationOffset), "Changes the location of where the user is positioned");
        }
    }
}
