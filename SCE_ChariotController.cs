using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ScenePhysicsImplementer
{
    class SCE_ChariotController : ControllerBase
    {
        public string ChariotLeadTag = "";
        public string ChariotStandingPosTag = "";
        public float kP = 1f;
        public float kD = 1f;

        public GameEntity chariotLeadObj { get; private set; }
        public Agent horseAgent { get; private set; }
        public SCE_ConstraintSpherical chariotConstraint { get; private set; }

        private static readonly ActionIndexCache actionSitting = ActionIndexCache.Create("act_sit_1");

        public override bool DisableCombatActionsOnUse => false;

        private bool isFrameAfterAgentTeleport = true;

        public Vec3 horseUseLocation { get; private set; }
        public Vec3 horseLookDirection { get; private set; }

        public override ActionIndexCache SetUserAnimation()
        {
            return ActionIndexCache.act_none;

        }
        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.Tick;
        }

        public override void SetUserAgentFrame(Agent agent)
        {
            base.SetUserAgentFrame(agent);
            agent.ClearTargetFrame();
        }

        public override void OnUse(Agent userAgent)
        {
            base.OnUse(userAgent);
            LockUserFrames = false;
            if (userAgent.HasMount) horseAgent = userAgent.MountAgent;
            if (horseAgent != null & chariotLeadObj != null)
            {
                isFrameAfterAgentTeleport = false;
                SetUserAgentFrame(horseAgent);

                if (chariotConstraint == null) AttachConstraintToChariotLead();
            }
            
        }

        public override void OnUseStopped(Agent userAgent, bool isSuccessful, int preferenceIndex)
        {
            base.OnUseStopped(userAgent, isSuccessful, preferenceIndex);
            horseAgent = null;
            if (chariotLeadObj != null && chariotConstraint != null)
            {
                chariotConstraint.DynamicallySetConstrainingObjectAsAgent(null);
            }
        }

        protected override void OnTick(float dt)
        {
            base.OnTick(dt);
            if (UserAgent != null)
            {
                if (!UserAgent.HasMount) this.OnUseStopped(UserAgent, true, 0);
            }
            if (!isFrameAfterAgentTeleport)
            {
                isFrameAfterAgentTeleport = true;
                SetChariotConstraint();
            }
        }

        protected override void OnInit()
        {
            base.OnInit();
            SetChariotLeadEntity();
        }

        private void SetChariotLeadEntity()
        {
            chariotLeadObj = Scene.FindEntityWithTag(ChariotLeadTag);
            if (chariotLeadObj == null) MathLib.DebugMessage("No chariot lead entity found. Check ChariotLeadTag: " + ChariotLeadTag, isError: true);
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            base.OnEditorVariableChanged(variableName);
            if (variableName == nameof(ChariotLeadTag)) SetChariotLeadEntity();
        }

        public override void RenderEditorHelpers()
        {
            base.RenderEditorHelpers();
            if (chariotLeadObj != null)
            {
                Vec3 basePos = base.GameEntity.GlobalPosition;
                Vec3 leadPos = chariotLeadObj.GlobalPosition;

                MBDebug.RenderDebugSphere(chariotLeadObj.GlobalPosition, 0.05f, Colors.Magenta.ToUnsignedInteger());
                MBDebug.RenderDebugLine(basePos, leadPos-basePos, Colors.Magenta.ToUnsignedInteger());
                MBDebug.RenderDebugDirectionArrow(targetUseLocation, targetUseLookDirection, Colors.Magenta.ToUnsignedInteger());
            }
        }

        private void SetChariotConstraint()
        {
            GameEntity horseEntity = horseAgent.AgentVisuals.GetEntity();
            MatrixFrame horseAgentUnscaledGlobalFrame = ObjectPropertiesLib.LocalOffsetAndNormalizeGlobalFrame(horseEntity.GetGlobalFrame(), Vec3.Zero);
            Mat3 horseUnscaledMat = horseAgentUnscaledGlobalFrame.rotation;


            Vec3 horseToLeadOffset = horseAgent.GetChestGlobalPosition() - targetUseLocation;
            horseToLeadOffset = horseUnscaledMat.TransformToLocal(horseToLeadOffset);

            chariotConstraint.ShowForceDebugging = true;

            chariotConstraint.kP = this.kP;
            chariotConstraint.kD = this.kD;

            chariotConstraint.ConstraintOffset = UseLocationOffset;
            chariotConstraint.ConstrainingObjectLocalOffset = horseToLeadOffset;

            chariotConstraint.DynamicallySetConstrainingObjectAsAgent(horseAgent);
        }

        private void AttachConstraintToChariotLead()
        {

            chariotLeadObj.CreateAndAddScriptComponent(nameof(SCE_ConstraintSpherical));
            IEnumerable<SCE_ConstraintSpherical> constraints = chariotLeadObj.GetScriptComponents<SCE_ConstraintSpherical>();
            //loop through constraints to find the newly created, unset constraint
            foreach(SCE_ConstraintSpherical constraint in constraints)
            {
                if (constraint.ConstrainingObjectTag != "" && constraint.isValid) continue;
                chariotConstraint = constraint;
                return;
            }
        }

    }
}
