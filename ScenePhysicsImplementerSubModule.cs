
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using System.Collections.Generic;
using System;

namespace ScenePhysicsImplementer
{
    public class ScenePhysicsImplementerSubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();
            MathLib.DebugMessage("Scene Physics Implementer Submodule Loaded");
        }

        public override void OnMissionBehaviorInitialize(Mission mission)
        {
            base.OnMissionBehaviorInitialize(mission);

            if (mission.HasMissionBehavior<SimpleMountedPlayerMissionController>() | mission.HasMissionBehavior<CustomBattleAgentLogic>())
            {
                mission.AddMissionBehavior(new ScenePhysicsEditorMissionBehavior());
                InformationManager.DisplayMessage(new InformationMessage("Scene Physics Editor Helpers Enabled"));
            }
        }
    }

    public class ScenePhysicsEditorMissionBehavior : MissionLogic
    {
        //needs cleanup
        private bool togglePhysicsControl = false;
        private bool isAttemptingToManipulate = false;
        private Agent player;
        private Vec3 localForceOrigin;
        private bool hasTarget;
        private Vec3 playerEyePos, playerLookVector;
        private GameEntity targetEntity;
        private MatrixFrame unscaledTargetFrame;
        float collisionDistance;

        public override void OnMissionTick(float dt)
        {
            base.OnMissionTick(dt);
            if (Agent.Main == null) return;
            player = Agent.Main;

            if (Input.IsKeyPressed(InputKey.CapsLock))
            {
                togglePhysicsControl = !togglePhysicsControl;
                if (togglePhysicsControl) MathLib.DebugMessage("Physics Manipulation Enabled");
                else MathLib.DebugMessage("Physics Manipulation Disabled");
            }

            isAttemptingToManipulate = Input.IsKeyDown(InputKey.LeftMouseButton);

            if (!togglePhysicsControl) return;

            OnTogglePhysicsManipulate();
          
        }

        private void OnTogglePhysicsManipulate()
        {
            playerLookVector = player.LookDirection;
            playerEyePos = player.GetEyeGlobalPosition();
            if (!playerLookVector.IsValid | !playerEyePos.IsValid) return;


            if (!isAttemptingToManipulate) hasTarget = RaycastForTarget();

            if (hasTarget && isAttemptingToManipulate)
            {
                OnPhysicsManipulate();
                MBDebug.RenderDebugLine(playerEyePos, playerLookVector * collisionDistance, Colors.Cyan.ToUnsignedInteger());
            }
        }

        private void UpdateTargetFrame()
        {
            unscaledTargetFrame = targetEntity.GetGlobalFrame();
            unscaledTargetFrame = ObjectPropertiesLib.AdjustGlobalFrameForCOM(unscaledTargetFrame, Vec3.Zero);
        }

        private bool RaycastForTarget()
        {
            Ray ray = new Ray(playerEyePos, playerLookVector, 100f);
            Vec3 rayCollisionPoint = ray.EndPoint;

            Vec3 collidedPoint = Vec3.Zero;
            Mission.Scene.RayCastForClosestEntityOrTerrain(playerEyePos, rayCollisionPoint, out collisionDistance, out collidedPoint, out targetEntity);

            targetEntity = CheckForParentPhysObject(targetEntity);
            if (targetEntity == null || !collidedPoint.IsValid) return false;
            
            DisplayTargetHelpers(targetEntity, collidedPoint);

            UpdateTargetFrame();
            localForceOrigin = unscaledTargetFrame.TransformToLocal(collidedPoint);
            return true;
        }

        private void OnPhysicsManipulate()
        {
            UpdateTargetFrame();

            if (Input.IsKeyDown(InputKey.F1)) collisionDistance += 0.1f;
            if (Input.IsKeyDown(InputKey.F2)) collisionDistance -= 0.1f;

            Vec3 forceTargetPos = playerEyePos + playerLookVector * collisionDistance;
            Vec3 forceOriginGlobalPos = unscaledTargetFrame.TransformToParent(localForceOrigin);
            Vec3 forceDir = forceTargetPos - forceOriginGlobalPos;

            MBDebug.RenderDebugDirectionArrow(forceOriginGlobalPos, forceDir, Colors.Cyan.ToUnsignedInteger());

            targetEntity.ApplyLocalForceToDynamicBody(localForceOrigin, forceDir*targetEntity.Mass*5f);
        }

        private void DisplayTargetHelpers(GameEntity targetEntity, Vec3 targetPoint)
        {
            MBDebug.RenderDebugSphere(targetPoint, 0.1f, Colors.Cyan.ToUnsignedInteger());

        }

        private GameEntity CheckForParentPhysObject(GameEntity targetEntity)
        {
            
            if (targetEntity == null) return null;
            if (targetEntity.HasScriptOfType<PhysicsObject>()) return targetEntity;
            else
            {
                GameEntity parent = targetEntity.Parent;
                if (parent != null) return CheckForParentPhysObject(parent);
            }

            return null;
        }
    }
}
