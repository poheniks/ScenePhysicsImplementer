using System;
using System.Collections.Generic;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.Engine;

namespace ScenePhysicsImplementer
{
    public class SCE_WheeledVehicleController : ControllerBase
    {
        //editor exposed variables
        public ControllerAnimations DriverAnimation = ControllerAnimations.None;
        public string SteerHingeTag = "";
        public string DriveHingeTag = "";

        public Vec3 SteerAngle = Vec3.Zero;
        public float DriveTorque = 1;

        public float SteerSmoothing = 1f;
        public float DriveTorqueSmoothing = 1f;
        //
        [EditorVisibleScriptComponentVariable(false)]
        public List<SCE_ConstraintHinge> steerHinges;
        [EditorVisibleScriptComponentVariable(false)]
        public List<SCE_ConstraintHinge> driveHinges;

        private float steerPercent;
        private float driveTorquePercent;

        private bool hasSteerHinges = false;
        private bool hasDriveHinges = false;

        private static readonly ActionIndexCache actionSitting = ActionIndexCache.Create("act_sit_1");
        private static readonly ActionIndexCache actionStanding = ActionIndexCache.Create("act_usage_ballista_idle_attacker");

        public enum ControllerAnimations
        {
            None = 0,
            Sitting = 1,
            Standing = 2
        }

        public override void Initialize()
        {
            base.Initialize();
            SetControllableHinges();
        }

        public override ActionIndexCache SetUserAnimation()
        {
            switch(DriverAnimation)
            {
                case ControllerAnimations.None:
                    return ActionIndexCache.act_none;
                case ControllerAnimations.Sitting:
                    return actionSitting;
                case ControllerAnimations.Standing:
                    return actionStanding;
                default:
                    return ActionIndexCache.act_none;
            }
        }

        protected override void OnEditorTick(float dt)
        {
            base.OnEditorTick(dt);
        }

        protected override void OnTickParallel(float dt)
        {
            base.OnTickParallel(dt);
            if (hasSteerHinges)
            {
                UpdateInputPercentage(ref steerPercent, movementInputVector.x, dt);
                foreach(SCE_ConstraintHinge hinge in steerHinges) hinge.HingeTurnDegrees = (steerPercent * SteerAngle);
                
            }
            if (hasDriveHinges)
            {
                UpdateInputPercentage(ref driveTorquePercent, movementInputVector.y, dt);
                foreach (SCE_ConstraintHinge hinge in driveHinges) hinge.TickTorqueReaction(hinge.targetFreeAxis * driveTorquePercent * DriveTorque);
            }
        }

        private void UpdateInputPercentage(ref float curPercent, float inputDir, float dt)
        {
            if (curPercent == 0 && inputDir == 0) return;
            if (SteerSmoothing == 0) SteerSmoothing = dt;

            float step = (dt / SteerSmoothing);
            if (step == 0) return;
            if (inputDir != 0)
            {
                step *= inputDir;
            }
            else
            {
                step *= Math.Sign(-curPercent);
            }
            float newPercent = curPercent + step;
            MathLib.ClampFloat(ref newPercent, -1f, 1f);

            if (inputDir == 0 && (Math.Sign(newPercent) != Math.Sign(curPercent))) newPercent = 0;  //prevent step from overshooting a zero setpoint
            curPercent = newPercent;
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            base.OnEditorVariableChanged(variableName);
            if (variableName == nameof(SteerHingeTag) | variableName == nameof(DriveHingeTag)) SetControllableHinges();
        }

        private void SetControllableHinges()
        {
            hasSteerHinges = FindControllableHinges(SteerHingeTag, out steerHinges);
            hasDriveHinges = FindControllableHinges(DriveHingeTag, out driveHinges);
        }

        private bool FindControllableHinges(string tag, out List<SCE_ConstraintHinge> list)
        {
            list = new List<SCE_ConstraintHinge>();
            IEnumerable<GameEntity> taggedEntities = Scene.FindEntitiesWithTag(tag);
            foreach(GameEntity entity in taggedEntities)
            {
                if (entity.HasScriptOfType<SCE_ConstraintHinge>()) list.Add(entity.GetFirstScriptOfType<SCE_ConstraintHinge>());
            }
            if (list.Count > 0) return true;
            return false;
        }

        public override void RenderEditorHelpers()
        {
            base.RenderEditorHelpers();
            if (hasSteerHinges) RenderControlledHinges(steerHinges, Colors.Green.ToUnsignedInteger());
            if (hasDriveHinges) RenderControlledHinges(driveHinges, Colors.Blue.ToUnsignedInteger());
        }

        private void RenderControlledHinges(List<SCE_ConstraintHinge> hinges, uint color)
        {
            foreach (SCE_ConstraintHinge hinge in hinges)
            {
                if (hinge.physObject == null) continue;
                Vec3 dir = hinge.physObject.GlobalPosition - GameEntity.GlobalPosition;
                MBDebug.RenderDebugLine(GameEntity.GlobalPosition, dir, color);
            }
        }
    }
}
