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
    public abstract class ConstraintBase : PhysicsObject
    {
        //scene editor exposed fields
        public string ConstrainingObjectTag = "";
        public bool ToggleEditorHelpers = true;
        public bool ToggleForceDebugging = false;
        public bool DisableParentReaction = false;
        public float kP = 1f;
        public float kD = 1f;

        public float kPStatic { get; set; } = 50f;
        public float kDStatic { get; set; } = 2f;

        //constraining object
        public GameEntity constrainingObject { get; private set; }
        public PhysicsObject constrainingObjectPhysics { get; private set; }
        public ObjectPropertiesLib constrainingObjProperties { get; private set; }

        public MatrixFrame targetInitialLocalFrame { get; private set; } //only local frame; used as a frame reference to the constraining object, initialized during scene init

        //frames for current tick
        private MatrixFrame _constrainingObjGlobalFrame;
        private MatrixFrame _physObjGlobalFrame;
        private MatrixFrame _targetGlobalFrame;
        public MatrixFrame constrainingObjGlobalFrame { get { return _constrainingObjGlobalFrame; } private set { _constrainingObjGlobalFrame = value; } }
        public MatrixFrame physObjGlobalFrame { get { return _physObjGlobalFrame; } private set { _physObjGlobalFrame = value; } }
        public MatrixFrame targetGlobalFrame { get { return _targetGlobalFrame; } private set { _targetGlobalFrame = value; } }
        public Mat3 physObjMat { get; private set; }
        public Mat3 targetMat { get; private set; }

        public MatrixFrame physObjOriginGlobalFrame { get; private set; }
        public MatrixFrame constraintObjOriginGlobalFrame { get; private set; }
        public MatrixFrame editorConstrainingObjectGlobalFrame { get; private set; }

        public bool firstFrame { get; private set; } = true;
        
        public bool isValid { get; private set; }


        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.TickParallel;
        }

        protected override void OnEditorInit()
        {
            base.OnEditorInit();
        }

        protected override void OnInit()
        {
            base.OnInit();
            InitializePhysics();
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
            
            constrainingObjectPhysics = constrainingObject.GetFirstScriptOfType<PhysicsObject>();
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
            targetInitialLocalFrame = constrainingObjGlobalFrame.TransformToLocal(physObjGlobalFrame);  //get & set target frame local to constraint object here
            targetGlobalFrame = constrainingObjGlobalFrame.TransformToParent(targetInitialLocalFrame);
        }

        protected override void OnTickParallel(float dt)
        {
            base.OnTickParallel(dt);
            if (!isValid) return;
            UpdateCurrentFrames();

            TickForce(Tuple.Create(Vec3.Zero, CalculateConstraintForce(dt)));
            TickTorque(CalculateConstraintTorque(dt));

            //TickTorque(new Vec3(0, 0, 0));
        }

        public void UpdateCurrentFrames()
        {
            //set & normalize current frames for debugging
            physObjOriginGlobalFrame = physObject.GetGlobalFrame();
            constraintObjOriginGlobalFrame = constrainingObject.GetGlobalFrame();
            //unscale original object frames - calling MakeUnit() directly doesn't seem to work??
            physObjOriginGlobalFrame = ObjectPropertiesLib.AdjustFrameForCOM(physObjOriginGlobalFrame, Vec3.Zero);
            constraintObjOriginGlobalFrame = ObjectPropertiesLib.AdjustFrameForCOM(constraintObjOriginGlobalFrame, Vec3.Zero);

            //set & normalize current frames, adjust for center of mass
            physObjGlobalFrame = physObject.GetGlobalFrame();
            constrainingObjGlobalFrame = constrainingObject.GetGlobalFrame();

            //unscale object frames; everything is in terms of physObj, so adjust this & constraint object frames by this CoM
            physObjGlobalFrame = ObjectPropertiesLib.AdjustFrameForCOM(physObjGlobalFrame, physObjCoM);
            constrainingObjGlobalFrame = ObjectPropertiesLib.AdjustFrameForCOM(constrainingObjGlobalFrame, physObjCoM);

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

        private void TickTorque(Vec3 torque)
        {
            Tuple<Vec3, Vec3> forceCouple = ConstraintLib.GenerateGlobalForceCoupleFromGlobalTorque(physObjGlobalFrame, torque);
            Tuple<Vec3, Vec3> inversedForceCouple = Tuple.Create(-forceCouple.Item1, -forceCouple.Item2);
            TickForce(forceCouple);
            TickForce(inversedForceCouple);
        }
        
        private void TickForce(Tuple<Vec3,Vec3> forceTuple)
        {
            Vec3 forceLocalOffset = forceTuple.Item1;
            Vec3 forceDir = forceTuple.Item2;
            if (forceDir == Vec3.Zero) return;

            Vec3 physObjLocalForcePos = physObjOriginGlobalFrame.TransformToLocal(physObjGlobalFrame.origin) + forceLocalOffset;  //get global frame adjusted for CoM and add any force offset
            Vec3 physObjGlobalForcePos = physObjOriginGlobalFrame.TransformToParent(physObjLocalForcePos);  //convert back to global coordinates for easier constraining object local transform
            Vec3 constraintObjLocalForcePos = constraintObjOriginGlobalFrame.TransformToLocal(physObjGlobalForcePos);   //convert global force pos to constraining object local coordinate system

            physObject.ApplyLocalForceToDynamicBody(physObjLocalForcePos, forceDir);
            if (!DisableParentReaction) constrainingObject.ApplyLocalForceToDynamicBody(constraintObjLocalForcePos, -forceDir);

            if (!ToggleForceDebugging) return;
            ShowForceDebuggers(physObjLocalForcePos, constraintObjLocalForcePos, forceDir);
        }

        private void FindConstrainingObject()
        {
            constrainingObject = physObject.Scene.FindEntityWithTag(ConstrainingObjectTag);
            if (constrainingObject == null)
            {
                isValid = false;
                return;
            }

            if (!constrainingObject.HasScriptOfType<PhysicsObject>())
            {
                constrainingObject.CreateAndAddScriptComponent(nameof(PhysicsObject));
                MathLib.DebugMessage("Added PhysicsObject script to constraining object", isImportantInfo: true);
            }

            if (physObject != null && constrainingObject != null) isValid = true;
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            if (nameof(ConstrainingObjectTag) == variableName) FindConstrainingObject();
        }

        protected override void OnEditorTick(float dt)
        {
            if (ToggleEditorHelpers && physObject.IsSelectedOnEditor()) ShowEditorHelpers();
        }

        public virtual void ShowForceDebuggers(Vec3 physObjLocalForcePos, Vec3 constraintObjLocalForcePos, Vec3 forceDir)
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

        public virtual void ShowEditorHelpers()
        {
            if (constrainingObject != null && constrainingObject.Scene == null) FindConstrainingObject();
            if (!isValid) return;

            if (physObjCoM != physObject.CenterOfMass)
            {
                UpdateCenterOfMass();
                InitializeFrames();
            }
            UpdateCurrentFrames();

            editorConstrainingObjectGlobalFrame = ObjectPropertiesLib.AdjustFrameForCOM(constrainingObject.GetGlobalFrame(), constrainingObject.CenterOfMass);

            Vec3 thisOrigin = physObjGlobalFrame.origin;
            Vec3 constrainingObjOrigin = editorConstrainingObjectGlobalFrame.origin;
            Vec3 dir = constrainingObjOrigin - thisOrigin;

            MBDebug.RenderDebugSphere(thisOrigin, 0.05f, Colors.Red.ToUnsignedInteger());
            MBDebug.RenderDebugSphere(constrainingObjOrigin, 0.05f, Colors.Magenta.ToUnsignedInteger());
            MBDebug.RenderDebugLine(thisOrigin, dir, Colors.Magenta.ToUnsignedInteger());
            MBDebug.RenderDebugBoxObject(constrainingObject.GlobalBoxMin, constrainingObject.GlobalBoxMax, Colors.Magenta.ToUnsignedInteger());
        }
    }
}
