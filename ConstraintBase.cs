using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.MountAndBlade;

namespace ScenePhysicsImplementer
{
    //DO EVERYTHING IN GLOBAL COORDINATES (but not actually everything)
    public abstract class ConstraintBase : SCE_PhysicsObject
    {
        //editor fields
        public string ConstrainingObjectTag = "";
        public bool ShowForceDebugging = false;
        public bool DisableParentReaction = false;
        public float ConstraintStiffness = 1f;
        public Vec3 PDGain = new Vec3(1f, 1f, 1f);


        public float kPStatic { get; set; } = 50f;  
        public float kDStatic { get; set; } = 2f;

        public Vec3 ConstraintOffset = Vec3.Zero;    //local coordinates, based on CoM

        [EditorVisibleScriptComponentVariable(false)]
        public Vec3 ConstrainingObjectLocalOffset { get; set; }  //for dynamic changes - local to constrainingObject coordinate system

        public virtual string constraintAdjective { get; set; } //for 3D editor help text

        //constraining object
        public GameEntity constrainingObject { get; private set; }
        public SCE_PhysicsObject constrainingObjectPhysics { get; private set; }
        public ObjectPropertiesLib constrainingObjProperties { get; private set; }

        public MatrixFrame targetInitialLocalFrame { get; private set; } //only local frame; used as a frame reference to the constraining object, initialized during scene init

        public MatrixFrame constrainingObjGlobalFrame { get; private set; }
        public MatrixFrame physObjGlobalFrame { get; private set; }
        public MatrixFrame targetGlobalFrame { get; private set; }
        public Mat3 physObjMat { get; private set; }
        public Mat3 targetMat { get; private set; }
        public Vec3 transformedOffsetForConstrainingObject { get; private set; }    //includes CoM and ForceOffset 

        public MatrixFrame physObjOriginGlobalFrame { get; private set; }
        public MatrixFrame constraintObjOriginGlobalFrame { get; private set; }
        public MatrixFrame editorConstrainingObjectGlobalFrame { get; private set; }

        public bool firstFrame { get; private set; } = true;
        
        public bool isValid { get; private set; }

        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.Tick;
        }

        protected override void OnEditorInit()
        {
            base.OnEditorInit();
        }

        protected override void OnInit()
        {
            base.OnInit();
        }

        public override void InitializeScene()
        {
            base.InitializeScene();
            FindConstrainingObject();
            if (!isValid) return;
            InitializeFrames(); //set MatrixFrame fields
        }

        public override void InitializePhysics()
        {
            base.InitializePhysics();
            if (!isValid) return;
            
            constrainingObjectPhysics = constrainingObject.GetFirstScriptOfType<SCE_PhysicsObject>();
            constrainingObjProperties = constrainingObjectPhysics.physObjProperties;

            if (!physObject.HasBody() | !constrainingObject.HasBody())
            {
                isValid = false;
                MathLib.DebugMessage("ERROR: No physics body on PhysicsObject", isError: true);
            }
        }

        private void InitializeFrames()
        {
            base.SetScriptComponentToTick(this.GetTickRequirement());
            UpdateCurrentFrames();

            firstFrame = false;

            MatrixFrame targetInitialLocalFrame = constrainingObjGlobalFrame.TransformToLocal(physObjGlobalFrame);  //get & set target frame local to constraint object here+
            targetInitialLocalFrame.origin += ConstrainingObjectLocalOffset;   //for dynamic offsets
            this.targetInitialLocalFrame = targetInitialLocalFrame;

            targetGlobalFrame = constrainingObjGlobalFrame.TransformToParent(targetInitialLocalFrame);
        }

        protected override void OnTick(float dt)
        {
            base.OnTick(dt);
            if (!isValid) return;
            UpdateCurrentFrames();

            TickForceReaction(Tuple.Create(Vec3.Zero, CalculateConstraintForce(dt)), DisableParentReaction);
            TickTorqueReaction(CalculateConstraintTorque(dt), DisableParentReaction);

            //TickTorque(new Vec3(0, 0, 0));
        }

        public void UpdateCurrentFrames()
        {
            //set & normalize current frames for debug renders
            physObjOriginGlobalFrame = physObject.GetGlobalFrame();
            constraintObjOriginGlobalFrame = constrainingObject.GetGlobalFrame();
            //unscale original object frames - calling MakeUnit() directly doesn't seem to work??
            physObjOriginGlobalFrame = ConstraintLib.LocalOffsetAndNormalizeGlobalFrame(physObjOriginGlobalFrame, Vec3.Zero);
            constraintObjOriginGlobalFrame = ConstraintLib.LocalOffsetAndNormalizeGlobalFrame(constraintObjOriginGlobalFrame, Vec3.Zero);

            //set current frames
            //unscale object frames; everything is in terms of physObj, so adjust this & constraint object frames by this CoM and force offset

            Vec3 physObjGlobalFrameOffset = physObjCoM + ConstraintOffset;
            if (firstFrame | physObject.IsSelectedOnEditor())
            {
                Vec3 worldOffset = physObjOriginGlobalFrame.rotation.TransformToParent(physObjGlobalFrameOffset);
                transformedOffsetForConstrainingObject = constraintObjOriginGlobalFrame.rotation.TransformToLocal(worldOffset);
            }

            physObjGlobalFrame = ConstraintLib.LocalOffsetAndNormalizeGlobalFrame(physObjOriginGlobalFrame, physObjGlobalFrameOffset);
            constrainingObjGlobalFrame = ConstraintLib.LocalOffsetAndNormalizeGlobalFrame(constraintObjOriginGlobalFrame, transformedOffsetForConstrainingObject);

            if (firstFrame) return; //targetInitialLocalFrame not initalized yet
            targetGlobalFrame = constrainingObjGlobalFrame.TransformToParent(targetInitialLocalFrame);

            //set rotation matrices
            physObjMat = physObjGlobalFrame.rotation;
            targetMat = targetGlobalFrame.rotation;
        }

        public virtual Vec3 CalculateConstraintForce(float dt)
        {
            return Vec3.Zero;
        }
        public virtual Vec3 CalculateConstraintTorque(float dt)
        {
            return Vec3.Zero;
        }

        public void TickTorqueReaction(Vec3 torque, bool disableParentReaction = false)
        {
            Tuple<Vec3, Vec3> forceCouple = ConstraintLib.GenerateGlobalForceCoupleFromGlobalTorque(physObjGlobalFrame, torque);
            Tuple<Vec3, Vec3> inversedForceCouple = Tuple.Create(-forceCouple.Item1, -forceCouple.Item2);
            TickForceReaction(forceCouple, disableParentReaction);
            TickForceReaction(inversedForceCouple, disableParentReaction);
        }

        public void TickForceReaction(Tuple<Vec3, Vec3> forceTuple, bool disableParentReaction = false)
        {
            Vec3 forceLocalOffset = forceTuple.Item1;
            Vec3 forceDir = forceTuple.Item2;
            if (forceDir == Vec3.Zero) return;

            Vec3 physObjLocalForcePos = physObjOriginGlobalFrame.TransformToLocal(physObjGlobalFrame.origin) + forceLocalOffset;  //get global frame adjusted for CoM and add any force offset
            Vec3 physObjGlobalForcePos = physObjOriginGlobalFrame.TransformToParent(physObjLocalForcePos);  //convert back to global coordinates for easier constraining object local transform
            Vec3 constraintObjLocalForcePos = constraintObjOriginGlobalFrame.TransformToLocal(physObjGlobalForcePos);   //convert global force pos to constraining object local coordinate system

            physObject.ApplyLocalForceToDynamicBody(physObjLocalForcePos, forceDir);
            if (!disableParentReaction) constrainingObject.ApplyLocalForceToDynamicBody(constraintObjLocalForcePos, -forceDir);

            if (!ShowForceDebugging) return;
            RenderForceDebuggers(physObjLocalForcePos, constraintObjLocalForcePos, forceDir);
        }

        public void DynamicallySetConstrainingObjectAsAgent(Agent agent)
        {
            if (agent == null)
            {
                isValid = false;
                return;
            }
            constrainingObject = agent.AgentVisuals.GetEntity();
            DisableParentReaction = true;
            firstFrame = true;
            InitializeFrames();
            isValid = true;
        }

        private void FindConstrainingObject()
        {
            constrainingObject = ObjectPropertiesLib.FindClosestTaggedEntity(physObject, ConstrainingObjectTag);
            if (constrainingObject == null)
            {
                isValid = false;
                return;
            }

            if (!constrainingObject.HasScriptOfType<SCE_PhysicsObject>())
            {
                constrainingObject.CreateAndAddScriptComponent(nameof(SCE_PhysicsObject));
                MathLib.DebugMessage("Added PhysicsObject script to constraining object", isImportantInfo: true);
            }

            if (physObject != null && constrainingObject != null) isValid = true;
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            base.OnEditorVariableChanged(variableName);
            if (variableName == nameof(ConstrainingObjectTag))
            {
                FindConstrainingObject();
                if (isValid) InitializeFrames();
            }
        }

        public virtual void RenderForceDebuggers(Vec3 physObjLocalForcePos, Vec3 constraintObjLocalForcePos, Vec3 forceDir)
        {
            //debug force locations & directions
            Vec3 debugPhysLoc = physObjOriginGlobalFrame.TransformToParent(physObjLocalForcePos);
            Vec3 debugConstraintLoc = constraintObjOriginGlobalFrame.TransformToParent(constraintObjLocalForcePos);

            MBDebug.RenderDebugSphere(targetGlobalFrame.origin, 0.1f, Colors.Magenta.ToUnsignedInteger());

            MBDebug.RenderDebugSphere(debugPhysLoc, 0.1f, Colors.White.ToUnsignedInteger());
            MBDebug.RenderDebugDirectionArrow(debugPhysLoc, forceDir.NormalizedCopy(), Colors.White.ToUnsignedInteger());

            if (DisableParentReaction) return;
            MBDebug.RenderDebugSphere(debugConstraintLoc, 0.1f, Colors.Black.ToUnsignedInteger());
            MBDebug.RenderDebugDirectionArrow(debugConstraintLoc, -forceDir.NormalizedCopy(), Colors.Black.ToUnsignedInteger());
        }

        public override void RenderEditorHelpers()
        {
            base.RenderEditorHelpers();
            if (constrainingObject == null || (constrainingObject != null && !constrainingObject.IsVisibleIncludeParents())) FindConstrainingObject();    //check for phantom entities - see ObjectPropertiesLib.FindClosestTaggedEntity()
            if (!isValid) return;

            if (physObjCoM != physObject.CenterOfMass) InitializeFrames();
            UpdateCurrentFrames();

            editorConstrainingObjectGlobalFrame = ConstraintLib.LocalOffsetAndNormalizeGlobalFrame(constrainingObject.GetGlobalFrame(), constrainingObject.CenterOfMass);

            Vec3 thisCoM = physObjOriginGlobalFrame.TransformToParent(physObjCoM);
            Vec3 thisConstraintCenter = physObjGlobalFrame.origin;
            Vec3 constrainingObjOrigin = editorConstrainingObjectGlobalFrame.origin;
            Vec3 dir = constrainingObjOrigin - thisConstraintCenter;

            MBDebug.RenderDebugSphere(thisConstraintCenter, 0.05f, Colors.Red.ToUnsignedInteger());
            MBDebug.RenderDebugSphere(constrainingObjOrigin, 0.05f, Colors.Magenta.ToUnsignedInteger());
            MBDebug.RenderDebugLine(thisConstraintCenter, dir, Colors.Magenta.ToUnsignedInteger());
            MBDebug.RenderDebugBoxObject(constrainingObject.GlobalBoxMin, constrainingObject.GlobalBoxMax, Colors.Magenta.ToUnsignedInteger());

            MBDebug.RenderDebugText3D(editorConstrainingObjectGlobalFrame.origin, $"{constraintAdjective} to: {constrainingObject.Name}", screenPosOffsetX: 15, screenPosOffsetY: -10);
            MBDebug.RenderDebugText3D(editorConstrainingObjectGlobalFrame.origin, $"Stiffness: {ConstraintStiffness}", screenPosOffsetX: 15, screenPosOffsetY: 10);
        }

        public override void DisplayHelpText()
        {
            base.DisplayHelpText();
            MathLib.HelpText(nameof(ConstraintOffset), "Changes the location where the constraint is attached and where constraint forces are applied");
            MathLib.HelpText(nameof(PDGain), "kP, kI, kD gain for PID tuning. Recommend leaving as 1.0. Note kI not implemented, use X and Z");
            MathLib.HelpText(nameof(ConstraintStiffness), "Controls how rigid the constraint is. Higher values decrease wobbling, but can be unstable if increased too high");
            MathLib.HelpText(nameof(DisableParentReaction), "Disables forces to the constraining object");
            MathLib.HelpText(nameof(ShowForceDebugging), "Renders arrows representing forces and force directions. Only appears in game");
            MathLib.HelpText(nameof(ConstrainingObjectTag), $"Tag for the target entity that this entity is constrained to. The target entity must be assigned this tag and the script component {nameof(SCE_PhysicsObject)}");
        }
    }
}
