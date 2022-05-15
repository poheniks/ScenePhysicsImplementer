
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

    public class SCE_MultiBodyNavMeshLoader : ScriptComponentBehavior
    {
        public string NavMeshPrefabName = "";
        public SimpleButton LoadNavMeshPrefab;
        public bool CreateChildEmptyEntityReference = false;

        private int dynamicNavMeshIDStart = 0;
        private Dictionary<GameEntity, GameEntity> parentChildEmptyEntityDict = new Dictionary<GameEntity,GameEntity>();
        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.TickOccasionally;
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            base.OnEditorVariableChanged(variableName);
            if (variableName == nameof(LoadNavMeshPrefab))
            {
                if (NavMeshPrefabName.Length > 0) Scene.ImportNavigationMeshPrefab(NavMeshPrefabName, 0);
            }
        }
        protected override void OnInit()
        {
            base.OnInit();
            SetScriptComponentToTick(GetTickRequirement());

            if (NavMeshPrefabName.Length > 0)
            {
                dynamicNavMeshIDStart = Mission.Current.GetNextDynamicNavMeshIdStart();
                Scene.ImportNavigationMeshPrefab(NavMeshPrefabName, dynamicNavMeshIDStart);
            }

        }

        protected override void OnTickOccasionally(float currentFrameDeltaTime)
        {
            base.OnTickOccasionally(currentFrameDeltaTime);
            AttachDynamicNavMesh();
            SetScriptComponentToTick(TickRequirement.None);
        }

        private void AttachDynamicNavMesh()
        {
            foreach (string tag in GameEntity.Tags)
            {
                //string format: (mesh face id)_(internal/connection/blocker)_(entity tag)
                string[] splitTag = tag.Split('_');
                int tagFirstHeader = tag.IndexOf('_');
                int tagLastHeader = tag.LastIndexOf('_');
                if (tagFirstHeader == 0 && tagLastHeader == 0 || tagFirstHeader == tagLastHeader)
                {
                    MathLib.DebugMessage("Error in SCE_MultiBodyNavMeshLoader. Missing _ header(s)|" + tag, isError: true);
                    continue;
                }

                //get face ID 
                int faceID;
                string subTagID = splitTag[0];
                if (!int.TryParse(subTagID, out faceID))
                {
                    MathLib.DebugMessage("Error in SCE_MultiBodyNavMeshLoader. Tag with non-numeric faceID|" + tag, isError: true);
                    continue;
                }
                else if (faceID < 0)
                {
                    MathLib.DebugMessage("Error in SCE_MultiBodyNavMeshLoader. Tag with negative faceID|" + tag, isError: true);
                    continue;
                }
                faceID += dynamicNavMeshIDStart;

                //get face type
                int faceType;
                string subTagType = splitTag[1];
                if (!int.TryParse(subTagType, out faceType) || (faceType < 0 || faceType > 2))
                {
                    MathLib.DebugMessage("Error in SCE_MultiBodyNavMeshLoader. Tag with non-valid face type. Use integers 0 - 2|" + tag, isError: true);
                    continue;
                }

                //get entity
                string subTagEntity = splitTag[2];
                GameEntity attachingEntity = Scene.FindEntityWithTag(subTagEntity);
                if (attachingEntity == null)
                {
                    MathLib.DebugMessage("Error in SCE_MultiBodyNavMeshLoader. No entity found|" + tag, isError: true);
                    continue;
                }

                //attach mesh
                if (CreateChildEmptyEntityReference)
                {
                    GameEntity parent = attachingEntity;
                    if (!parentChildEmptyEntityDict.TryGetValue(parent, out attachingEntity)) 
                    {
                        GameEntity emptyEntity = GameEntity.CreateEmptyDynamic(Scene); //create child empty entity to attach mesh prefab to
                        parent.AddChild(emptyEntity, false);
                        parentChildEmptyEntityDict.Add(parent, emptyEntity);
                        attachingEntity = emptyEntity;
                    }
                }

                attachingEntity.SetGlobalFrame(GameEntity.GetGlobalFrame());    //set empty entity frame to the scriptcomponent entity that the mesh prefab is localized about

                switch (faceType)
                {
                    case 0:
                        attachingEntity.AttachNavigationMeshFaces(faceID, false, false, false); //internal mesh face
                        break;
                    case 1:
                        attachingEntity.AttachNavigationMeshFaces(faceID, true, false, false);  //connecting mesh face
                        break;
                    case 2:
                        attachingEntity.AttachNavigationMeshFaces(faceID, false, true, false);  //blocking mesh face
                        break;
                }
                Scene.SetAbilityOfFacesWithId(faceID, true);

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

            if (Input.IsKeyPressed(InputKey.F3)) DebugNavMeshFaceID();

            if (!togglePhysicsControl) return;
            OnTogglePhysicsManipulate();
 
          
        }

        private void DebugNavMeshFaceID()
        {
            Vec3 playerPos = player.Position;
            int faceID;
            Mission.Scene.GetNavigationMeshForPosition(ref playerPos, out faceID);
            MathLib.DebugMessage("ID: " + faceID.ToString());
            //MathLib.DebugMessage(Mission.Scene.IsAnyFaceWithId(1000054).ToString());

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
            unscaledTargetFrame = ObjectPropertiesLib.LocalOffsetAndNormalizeGlobalFrame(unscaledTargetFrame, Vec3.Zero);
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
