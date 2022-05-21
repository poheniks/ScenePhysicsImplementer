using System;
using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;

namespace ScenePhysicsImplementer
{
    //code is fairly messy for this scriptComponent. Could possibly abstract the class out to be used as a base for dynamically attaching & instantiating constraints - doesn't need to be horse/chariot related 
    class SCE_ChariotController : ControllerBase
    {
        //editor fields
        public string ChariotDrawBarTag = "";
        public float kP = 1f;
        public float kD = 1f;


        public GameEntity chariotDrawBarObj { get; private set; }
        public Agent horseAgent { get; private set; }
        public SCE_ConstraintSpherical chariotConstraint { get; private set; }

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
            
            agent.ClearTargetFrame();   //clear target frame after teleporting agent to give agent free movement again
        }

        public override void OnUse(Agent userAgent)
        {
            //base.OnUse(userAgent);
            userAgent.SetActionChannel(0, SetUserAnimation(), ignorePriority: true);
            LockUserFrames = false;
            LockUserPositions = false;
            if (userAgent.HasMount) horseAgent = userAgent.MountAgent;
            if (horseAgent != null & chariotDrawBarObj != null)
            {
                isFrameAfterAgentTeleport = false;
                SetUserAgentFrame(horseAgent);

                if (chariotConstraint == null) AttachConstraintToChariotDrawBar();
            }
            
        }

        public override void OnUseStopped(Agent userAgent, bool isSuccessful, int preferenceIndex)
        {
            base.OnUseStopped(userAgent, isSuccessful, preferenceIndex);
            horseAgent = null;
            if (chariotDrawBarObj != null && chariotConstraint != null)
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
            SetChariotDrawBarEntity();
        }

        private void SetChariotDrawBarEntity()
        {
            chariotDrawBarObj = Scene.FindEntityWithTag(ChariotDrawBarTag);
            if (chariotDrawBarObj == null) MathLib.DebugMessage($"No chariot drawbar entity found. Check {nameof(ChariotDrawBarTag)}: " + ChariotDrawBarTag, isError: true);
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            base.OnEditorVariableChanged(variableName);
            if (variableName == nameof(ChariotDrawBarTag)) SetChariotDrawBarEntity();
        }

        public override void RenderEditorHelpers()
        {
            base.RenderEditorHelpers();
            if (chariotDrawBarObj != null)
            {
                Vec3 basePos = base.GameEntity.GlobalPosition;
                Vec3 drawBarPos = chariotDrawBarObj.GlobalPosition;

                MBDebug.RenderDebugSphere(chariotDrawBarObj.GlobalPosition, 0.05f, Colors.Magenta.ToUnsignedInteger());
                MBDebug.RenderDebugLine(basePos, drawBarPos-basePos, Colors.Magenta.ToUnsignedInteger());
                MBDebug.RenderDebugDirectionArrow(targetUseLocation, targetUseLookDirection, Colors.Magenta.ToUnsignedInteger());
            }
        }

        private void SetChariotConstraint()
        {
            GameEntity horseEntity = horseAgent.AgentVisuals.GetEntity();
            MatrixFrame horseAgentUnscaledGlobalFrame = ObjectPropertiesLib.LocalOffsetAndNormalizeGlobalFrame(horseEntity.GetGlobalFrame(), Vec3.Zero);
            Mat3 horseUnscaledMat = horseAgentUnscaledGlobalFrame.rotation;


            Vec3 horseToDrawBarOffset = horseAgent.GetChestGlobalPosition() - targetUseLocation;
            horseToDrawBarOffset = horseUnscaledMat.TransformToLocal(horseToDrawBarOffset);

            chariotConstraint.ShowForceDebugging = true;

            chariotConstraint.kP = this.kP;
            chariotConstraint.kD = this.kD;

            chariotConstraint.ConstraintOffset = UserLocationOffset;
            chariotConstraint.ConstrainingObjectLocalOffset = horseToDrawBarOffset;

            chariotConstraint.DynamicallySetConstrainingObjectAsAgent(horseAgent);
        }

        private void AttachConstraintToChariotDrawBar()
        {

            chariotDrawBarObj.CreateAndAddScriptComponent(nameof(SCE_ConstraintSpherical));
            IEnumerable<SCE_ConstraintSpherical> constraints = chariotDrawBarObj.GetScriptComponents<SCE_ConstraintSpherical>();
            //loop through constraints to find the newly created, unset constraint
            foreach(SCE_ConstraintSpherical constraint in constraints)
            {
                if (constraint.ConstrainingObjectTag != "" && constraint.isValid) continue;
                chariotConstraint = constraint;
                return;
            }
        }

        public override void DisplayHelpText()
        {
            base.DisplayHelpText();
            MathLib.HelpText(nameof(kD), "Damping gain for constraint forces between horse & chariot draw bar. Higher values increase constraint stiffness. Recommend using similar values for kP and kD. See PID control systems for more info");
            MathLib.HelpText(nameof(kP), "Proportional gain for constraint forces between horse & chariot draw bar. Higher values increase constraint stiffness. Recommend using similar values for kP and kD. See PID control systems for more info");
            MathLib.HelpText(nameof(ChariotDrawBarTag), "Tag for finding the chariot draw bar entity, which attaches to the horse agent. The draw bar entity must be assigned this tag");
        }
    }
}
