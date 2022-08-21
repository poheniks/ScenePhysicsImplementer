using System;
using System.Collections.Generic;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.Engine;

namespace ScenePhysicsImplementer
{
    //
    public class SCE_WheeledVehicleController : ControllerBase
    {
        //editor fields
        public ControllerAnimations DriverAnimation = ControllerAnimations.None;
        public float SearchDistanceForHinges = 10f;
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

        public enum ControllerAnimations
        {
            None = 0,
            Sitting = 1,
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
                default:
                    return ActionIndexCache.act_none;
            }
        }

        protected override void OnEditorTick(float dt)
        {
            base.OnEditorTick(dt);
        }

        protected override void OnTick(float dt)
        {
            base.OnTick(dt);
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

            if (inputDir != 0) step *= inputDir;
            else step *= Math.Sign(-curPercent);
            
            float newPercent = curPercent + step;
            MathLib.ClampFloat(ref newPercent, -1f, 1f);

            if (inputDir == 0 && (Math.Sign(newPercent) != Math.Sign(curPercent))) newPercent = 0;  //prevent step from overshooting a zero setpoint
            curPercent = newPercent;
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            base.OnEditorVariableChanged(variableName);
            if (variableName == nameof(SteerHingeTag) | variableName == nameof(DriveHingeTag) | variableName == nameof(SearchDistanceForHinges)) SetControllableHinges();
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
                if (SearchDistanceForHinges < entity.GlobalPosition.Distance(this.GameEntity.GlobalPosition)) continue; //if entity is too far away, ignore 
                if (!entity.IsVisibleIncludeParents()) continue;    //FindEntitiesWithTag seems to pick up phantom copies of objects when placing prefabs

                if (entity.HasScriptOfType<SCE_ConstraintHinge>()) list.Add(entity.GetFirstScriptOfType<SCE_ConstraintHinge>());
            }
            if (list.Count > 0) return true;
            return false;
        }

        public override void RenderEditorHelpers()
        {
            base.RenderEditorHelpers();
            SetControllableHinges();
            if (hasSteerHinges) RenderControlledHinges(steerHinges, Colors.Green.ToUnsignedInteger(), "Steerable wheel");
            if (hasDriveHinges) RenderControlledHinges(driveHinges, Colors.Blue.ToUnsignedInteger(), "Driving wheel");

            MBDebug.RenderDebugSphere(this.GameEntity.GlobalPosition, SearchDistanceForHinges, Colors.Black.ToUnsignedInteger(), true);
            MBDebug.RenderDebugText3D(this.GameEntity.GlobalPosition + Vec3.Up*SearchDistanceForHinges, "Hinge search envelope");
        }

        private void RenderControlledHinges(List<SCE_ConstraintHinge> hinges, uint color, string adjectiveText)
        {
            foreach (SCE_ConstraintHinge hinge in hinges)
            {
                if (hinge.physObject == null) continue;
                Vec3 dir = hinge.physObject.GlobalPosition - GameEntity.GlobalPosition;
                MBDebug.RenderDebugLine(this.GameEntity.GlobalPosition, dir, color);

                int yOffset = -10;
                if (hinges == driveHinges) yOffset = 10;
                MBDebug.RenderDebugText3D(hinge.GameEntity.GlobalPosition, adjectiveText, screenPosOffsetX: 15, screenPosOffsetY: yOffset);
            }
        }

        public override void DisplayHelpText()
        {
            base.DisplayHelpText();
            MathLib.HelpText(nameof(DriveTorqueSmoothing), "Adjusts how quickly or slowly torque is increased. Value is the time, in seconds, to apply max torque");
            MathLib.HelpText(nameof(SteerSmoothing), "Adjusts how quickly or slowly steer angle changes. Value is the time, in seconds, to achieve max steer angle");
            MathLib.HelpText(nameof(DriveTorque), "Changes the amount of power applied to drive wheels");
            MathLib.HelpText(nameof(SteerAngle), "Changes the max steer angle of steerable wheels");
            MathLib.HelpText(nameof(DriveHingeTag), $"Tag for finding drive wheel entities. Drive wheel entities must be assigned this tag and the script component {nameof(SCE_ConstraintHinge)}");
            MathLib.HelpText(nameof(SteerHingeTag), $"Tag for finding steerable wheel entities. Steerable wheel entities must be assigned this tag and the script component { nameof(SCE_ConstraintHinge)}");
            MathLib.HelpText(nameof(SearchDistanceForHinges), "Max distance that steerable and drive wheel entities can be located. Any entity exceeding this distance will not be controlled");
            MathLib.HelpText(nameof(DriverAnimation), "Sets the animation for the user");
        }
    }
}
