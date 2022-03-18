using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.GauntletUI.PrefabSystem;
using System.Collections.Generic;
using System.Linq;

namespace ScenePhysicsImplementer
{
    /* NOTES

     */

    public class SCE_DynamicObjectBase : UsableMachine
    {

        public bool isParent = false;
        public bool isUsable = false;
        public bool enableGravity = false;
        public bool hingeConstraint = false;
        public bool isDriveHinge = false;
        public bool isSteerHinge = false;
        public bool testCase = false;

        private GameEntity physObject;
        private bool parentInitialized;
        private List<GameEntity> childPhysObjects = new List<GameEntity>();
        private bool firstTick = false;

        //child & parent fields
        List<SCEConstraintSet> constraintSets = new List<SCEConstraintSet>();

        //child fields
        GameEntity parentObject;
        SCE_DynamicObjectBase parentScript; 
        bool childToParentInitialized;
        MatrixFrame initialFrame;

        private Scene scene;

        //test vehicles
        private bool isVehicleValid = false;
        private bool hasActiveDriver = false;

        public override TickRequirement GetTickRequirement()
        {
            return ScriptComponentBehavior.TickRequirement.TickParallel;
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            return new TextObject("Object Base", null).ToString();
        }

        public override TextObject GetActionTextForStandingPoint(UsableMissionObject usableGameObject)
        {
            return new TextObject("Pilot", null);
        }
        //descriptor overrides

        protected override void OnEditorInit()
        {
            base.OnEditorInit();
            physObject = base.GameEntity;
            scene = physObject.Scene;
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            base.OnEditorVariableChanged(variableName);
    
        }

        protected override void OnEditorTick(float dt)
        {
            base.OnEditorTick(dt);
        }

        protected override void OnInit()
        {
            base.OnInit();
            base.SetScriptComponentToTick(this.GetTickRequirement());

            physObject = base.GameEntity;
            scene = physObject.Scene;

            if (NavMeshPrefabName.Length > 0) AttachDynamicNavmeshToEntity();

            if (isParent) ParentInit();
            if (!enableGravity) physObject.DisableGravity();
            if (isUsable) VehicleInit();

        }

        protected override void OnPhysicsCollision(ref PhysicsContact contact)
        {

        }

        protected override void OnTickParallel(float dt)
        {
            if (!firstTick && (isParent | (!isParent && parentInitialized)))
            {
                GameEntityPhysicsExtensions.SetDamping(physObject, 1.1f, 1.1f);
                SCEObjectPhysLib objLib = new SCEObjectPhysLib(physObject);
                Vec3 prinMoI = objLib.principalMomentsOfInertia;
                //prinMoI = Vec3.One * 500f;
                GameEntityPhysicsExtensions.SetMassSpaceInertia(physObject, prinMoI); //MoI axes: (x, y, z) = (s, f, u)

                firstTick = true;
            }
            //for child objects
            if (!isParent && childToParentInitialized)
            {
                ChildTickPositioning();
                foreach (SCEConstraintSet constraint in constraintSets)
                {
                    constraint.TickConstraint(dt);
                }
            }
            if (isParent && isVehicleValid) TickVehicle();

            if (testCase)
            {
                
                Vec3 cforce = new Vec3(0, 0, 0);
                Vec3 pForce = new Vec3(0, 1, 0);
                Vec3 tForce = new Vec3(0, 1, 0);

                Vec3 torque = new Vec3(0, 10, 0);
                MatrixFrame orgFrame = physObject.GetGlobalFrame();
                MatrixFrame adjGlobalFrame = physObject.GetGlobalFrame();
                Vec3 CoM = physObject.CenterOfMass * 1f;

                adjGlobalFrame = SCEConstraintLib.AdjustFrameForCOM(adjGlobalFrame, CoM);
                adjGlobalFrame.rotation.MakeUnit();
                
                Vec3 scale = physObject.GetGlobalScale();
                //CoM = SCEMath.VectorMultiplyComponents(CoM, physObject.GetGlobalScale());
                

                Tuple<Vec3, Vec3> forceCouple = SCEConstraintLib.GenerateGlobalForceCoupleFromLocalTorque(adjGlobalFrame, torque);
                Vec3 forcePos = forceCouple.Item1;
                Vec3 forceDir = forceCouple.Item2;

                forcePos = new Vec3(0, 1, 0);
                forceDir = orgFrame.rotation.u * 10f;
                Vec3 offset = new Vec3(0, 1f, 0);

                if (isParent) 
                {
                    SCEObjectPhysLib objLib = new SCEObjectPhysLib(physObject);
                    Vec3 MoI = SCEMath.VectorRound(objLib.principalMomentsOfInertia, 2);
                    SCEMath.DebugMessage("MoI: " + MoI);
                    /*
                    MBDebug.RenderDebugSphere(orgFrame.TransformToParent(forcePos), 0.2f);
                    MBDebug.RenderDebugDirectionArrow(orgFrame.TransformToParent(forcePos), forceDir);

                    MBDebug.RenderDebugSphere(orgFrame.TransformToParent(-forcePos), 0.2f);
                    MBDebug.RenderDebugDirectionArrow(orgFrame.TransformToParent(-forcePos), -forceDir);
                    */

                    //physObject.ApplyLocalForceToDynamicBody(-offset, -forceDir);
                    //physObject.ApplyLocalForceToDynamicBody(Vec3.Zero + offset, forceDir);

                    //physObject.ApplyLocalForceToDynamicBody(CoM, tForce);
                }
                if (!isParent)
                {
                    //physObject.ApplyLocalForceToDynamicBody(CoM, -tForce*1f);
                }

            }
        }

        private void ParentInit()
        {
            IEnumerable<GameEntity> children = physObject.GetChildren();
            foreach (GameEntity child in children)
            {
                SCE_DynamicObjectBase childBase = (SCE_DynamicObjectBase)child.GetFirstScriptOfType(this.GetType());
                if (childBase == null) continue;
                if (childBase != null)
                {
                    if (childBase.isParent) continue;
                }

                SCEMath.DebugMessage("has type " + child.GetOldPrefabName());

                float sphereRadius = SCEObjectPhysLib.CalculateSphereBodyForObject(child);
                Vec3 unscaledCenterOfMass = SCEMath.VectorMultiplyComponents(child.CenterOfMass, SCEMath.VectorInverseComponents(child.GetGlobalScale()));

                child.RemovePhysics();
                child.AddSphereAsBody(unscaledCenterOfMass, sphereRadius*1f, BodyFlags.BodyOwnerEntity);
                child.EnableDynamicBody();
                child.SetBodyFlags(BodyFlags.BodyOwnerEntity);

                MatrixFrame childFrame = child.GetFrame();

                childBase.parentObject = physObject;
                childBase.initialFrame = childFrame;
                childBase.parentScript = this;
                childBase.childToParentInitialized = true;
                childPhysObjects.Add(child);

                if (childBase.testCase) continue;

                SCEConstraintSet baseConstraint = new SCEConstraintSet(physObject, child);
                if (!childBase.hingeConstraint) baseConstraint.AddWeld(childFrame);
                else baseConstraint.AddHinge(childFrame);

                constraintSets.Add(baseConstraint);
                childBase.constraintSets.Add(baseConstraint);
            }

            //ghost = GameEntity.CreateEmpty(scene);
            //ghost.AddPhysics(physObject.Mass, physObject.CenterOfMass, physObject.GetBodyShape(), Vec3.Zero, Vec3.Zero, physObject.GetBodyShape().GetDominantMaterialForTriangleMesh(0), true, 0);
            //physObject.AddChild(ghost);

            physObject.EnableDynamicBody();
            physObject.SetBodyFlags(BodyFlags.Dynamic | BodyFlags.Moveable | BodyFlags.BodyOwnerEntity);

            parentInitialized = true;
        }

        private void ChildInit()
        {
            

        }
        
        private void ChildTickPositioning()
        {
            MatrixFrame testFrame = initialFrame;
            MatrixFrame globalFrame = parentObject.GetFrame().TransformToParent(testFrame);

            Quaternion frameQuat = globalFrame.rotation.ToQuaternion();
            Quaternion rotQuat = SCEMath.CreateQuaternionFromTWEulerAngles(new Vec3(0f,0f,0f) * (float)SCEMath.DegtoRad);
            Quaternion newRotation = SCEMath.QuaternionMultiply(rotQuat, frameQuat);

            Mat3 frameRot = newRotation.ToMat3;
            globalFrame.rotation = frameRot;

            int setAng = 2;

            if (setAng == 1)
            {
                physObject.SetGlobalFrame(globalFrame);
                //physObject.DisableDynamicBodySimulation();
            }
            //else if (setAng == 2) physObject.EnableDynamicBody();
        }

        private void VehicleInit()
        {
            isVehicleValid = PilotStandingPoint != null;
            if (isVehicleValid) Activate();
        }
        private void TickVehicle()
        {
            if (PilotAgent == null) return;

            List<SCE_DynamicObjectBase> driveHinges = new List<SCE_DynamicObjectBase>();
            List<SCE_DynamicObjectBase> steerHinges = new List<SCE_DynamicObjectBase>();
            if (childPhysObjects.Count > 0)
            {
                foreach (GameEntity childObj in childPhysObjects)
                {
                    SCE_DynamicObjectBase objBase = childObj.GetFirstScriptOfType<SCE_DynamicObjectBase>();
                    if (objBase != null)
                    {
                        if (objBase.isDriveHinge) driveHinges.Add(objBase);
                        if (objBase.isSteerHinge) steerHinges.Add(objBase);
                    }
                }
            }
            Vec2 control = PilotAgent.MovementInputVector;

            foreach (SCE_DynamicObjectBase drive in driveHinges)
            {
                drive.constraintSets.FirstOrDefault().hingePower = -control.y * 3f;
                physObject.ApplyLocalForceToDynamicBody(physObject.CenterOfMass, Vec3.Up * -Math.Abs(control.y) * 1f);
            }
            foreach (SCE_DynamicObjectBase steer in steerHinges)
            {
                steer.constraintSets.FirstOrDefault().hingeRotation = new Vec3(0,0,35f*(float)SCEMath.DegtoRad)*-control.x;
            }
            
        }

    }
}