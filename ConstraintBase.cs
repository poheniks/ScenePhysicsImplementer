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
    //DO EVERYTHING IN GLOBAL COORDINATES (with some exceptions)
    public abstract class ConstraintBase : ScriptComponentBehavior
    {
        public string ConstrainingObjectTag = "";
        public bool ToggleEditorHelpers = true;
        public bool ToggleForceDebugging = false;
        public bool DisableParentReaction = false;
        public float kP = 1f;
        public float kD = 1f;

        public float kPStatic { get; set; } = 50f;
        public float kDStatic { get; set; } = 2f;

        public GameEntity constrainingObject { get; private set; }
        public GameEntity physObject { get; private set; }
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

        //frames from previous tick
        private MatrixFrame _prevPhysObjGlobalFrame;
        private MatrixFrame _prevConstrainingObjectGlobalFrame;
        private MatrixFrame _prevTargetGlobalFrame;
        public MatrixFrame prevPhysObjGlobalFrame { get { return _prevPhysObjGlobalFrame; } private set { _prevPhysObjGlobalFrame = value; } }
        public MatrixFrame prevConstrainingObjectGlobalFrame { get { return _prevConstrainingObjectGlobalFrame; } private set { _prevConstrainingObjectGlobalFrame = value; } }
        public MatrixFrame prevTargetGlobalFrame { get { return _prevTargetGlobalFrame; } private set { _prevTargetGlobalFrame = value; } }
        public Mat3 prevPhysObjMat { get; private set; }
        public Mat3 prevTargetMat { get; private set; }

        private bool firstFrame = true;
        Vec3 physObjCoM;  //always local coordinates
        private List<Tuple<Vec3, Vec3>> forces = new List<Tuple<Vec3, Vec3>>();

        public ObjectPropertiesLib physObjProperties { get; private set; }
        public ObjectPropertiesLib constrainingObjProperties { get; private set; }

        public bool isValid { get; private set; }

        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.TickParallel;
        }

        protected override void OnEditorInit()
        {
            InitializeConstraint();
        }

        protected override void OnInit()
        {
            InitializeConstraint();
        }

        public void InitializeConstraint()
        {
            physObject = base.GameEntity;
            FindConstrainingObject();

            if (physObject != null && constrainingObject != null) isValid = true;
            else return;

            physObjProperties = new ObjectPropertiesLib(physObject);
            constrainingObjProperties = new ObjectPropertiesLib(constrainingObject);

            //need to move these two calls to a different class later
            physObject.SetMassSpaceInertia(physObjProperties.principalMomentsOfInertia);
            constrainingObject.SetMassSpaceInertia(constrainingObjProperties.principalMomentsOfInertia);

            physObjCoM = physObject.CenterOfMass;  //does NOT return vector lengths scaled to child matrix frames - vector lengths always global

            InitializeFrames(); //set MatrixFrame fields
        }

        private void InitializeFrames()
        {
            base.SetScriptComponentToTick(this.GetTickRequirement());
            UpdateCurrentFrames();

            firstFrame = false;
            targetInitialLocalFrame = constrainingObjGlobalFrame.TransformToLocal(physObjGlobalFrame);  //get & set target frame local to constraint object here
            targetGlobalFrame = constrainingObjGlobalFrame.TransformToParent(targetInitialLocalFrame);

            //set previous frames to current, on first tick - since no previous frames exist yet
            prevConstrainingObjectGlobalFrame = constrainingObjGlobalFrame;
            prevPhysObjGlobalFrame = physObjGlobalFrame;
            prevTargetGlobalFrame = targetGlobalFrame;
            prevPhysObjMat = physObjMat;
            prevTargetMat = targetMat;
        }

        protected override void OnTickParallel(float dt)
        {
            if (!isValid) return;
            UpdateCurrentFrames();

            TickForce(Tuple.Create(Vec3.Zero, CalculateConstraintForce(dt)));
            TickTorque(CalculateConstraintTorque(dt));

            //TickTorque(new Vec3(0, 0, 0));

            UpdatePreviousFrames();
        }

        public void UpdateCurrentFrames()
        {
            //set current frames
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

        public void UpdatePreviousFrames()
        {
            //frames
            prevConstrainingObjectGlobalFrame = constrainingObjGlobalFrame;
            prevPhysObjGlobalFrame = physObjGlobalFrame;
            prevTargetGlobalFrame = targetGlobalFrame;
            //rotation matrices
            prevPhysObjMat = physObjMat;
            prevTargetMat = targetMat;
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
            Tuple<Vec3, Vec3> forceCouple = ConstraintLib.GenerateGlobalForceCoupleFromLocalTorque(physObjGlobalFrame, torque);
            Tuple<Vec3, Vec3> inversedForceCouple = Tuple.Create(-forceCouple.Item1, -forceCouple.Item2);
            TickForce(forceCouple);
            TickForce(inversedForceCouple);
        }
        
        private void TickForce(Tuple<Vec3,Vec3> forceTuple)
        {
            Vec3 forceLocalOffset = forceTuple.Item1;
            Vec3 forceDir = forceTuple.Item2;
            if (forceDir == Vec3.Zero) return;

            MatrixFrame physObjOriginGlobalFrame = physObject.GetGlobalFrame();
            MatrixFrame constraintObjOriginGlobalFrame = constrainingObject.GetGlobalFrame();

            physObjOriginGlobalFrame.rotation.MakeUnit();
            constraintObjOriginGlobalFrame.rotation.MakeUnit();

            Vec3 localPhysObjOrigin = physObjOriginGlobalFrame.TransformToLocal(physObjGlobalFrame.origin) + forceLocalOffset;  //get global frame adjusted for CoM and add any force offset
            Vec3 globalPhysObjOrigin = physObjOriginGlobalFrame.TransformToParent(localPhysObjOrigin);  //convert back to global coordinates for easier constraining object local transform
            Vec3 localConstraintObjOrigin = constraintObjOriginGlobalFrame.TransformToLocal(globalPhysObjOrigin);

            physObject.ApplyLocalForceToDynamicBody(localPhysObjOrigin, forceDir);
            if (!DisableParentReaction) constrainingObject.ApplyLocalForceToDynamicBody(localConstraintObjOrigin, -forceDir);

            //debug force locations & directions
            if (!ToggleForceDebugging) return;
            Vec3 debugPhysLoc = physObjOriginGlobalFrame.TransformToParent(localPhysObjOrigin);
            Vec3 debugConstraintLoc = constraintObjOriginGlobalFrame.TransformToParent(localConstraintObjOrigin);

            MBDebug.RenderDebugSphere(targetGlobalFrame.origin, 0.1f, Colors.Magenta.ToUnsignedInteger());

            MBDebug.RenderDebugSphere(debugPhysLoc, 0.1f, Colors.Green.ToUnsignedInteger());
            MBDebug.RenderDebugDirectionArrow(debugPhysLoc, forceDir.NormalizedCopy(), Colors.Green.ToUnsignedInteger());

            MBDebug.RenderDebugSphere(debugConstraintLoc, 0.1f, Colors.Blue.ToUnsignedInteger());
            MBDebug.RenderDebugDirectionArrow(debugConstraintLoc, -forceDir.NormalizedCopy(), Colors.Blue.ToUnsignedInteger());
            
        }

        private void FindConstrainingObject()
        {
            constrainingObject = physObject.Scene.FindEntityWithTag(ConstrainingObjectTag);
            if (constrainingObject == null)
            {
                isValid = false;
                return;
            }
            isValid = true;
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            if (nameof(ConstrainingObjectTag) == variableName)
            {
                FindConstrainingObject();
            }
        }

        protected override void OnEditorTick(float dt)
        {
            if (ToggleEditorHelpers && physObject.IsSelectedOnEditor()) ShowEditorHelpers();
        }

        private void ShowEditorHelpers()
        {
            if (constrainingObject != null && constrainingObject.Scene == null) FindConstrainingObject();
            if (!isValid) return;

            if (physObjCoM != physObject.CenterOfMass)
            {
                physObjCoM = physObject.CenterOfMass;
                InitializeFrames();
            }
            UpdateCurrentFrames();

            MatrixFrame editorConstrainingObjectGlobalFrame = ObjectPropertiesLib.AdjustFrameForCOM(constrainingObject.GetGlobalFrame(), constrainingObject.CenterOfMass);

            Vec3 thisOrigin = physObjGlobalFrame.origin;
            Vec3 constrainingObjOrigin = editorConstrainingObjectGlobalFrame.origin;
            Vec3 dir = constrainingObjOrigin - thisOrigin;

            MBDebug.RenderDebugSphere(thisOrigin, 0.05f, Colors.Red.ToUnsignedInteger());
            MBDebug.RenderDebugSphere(constrainingObjOrigin, 0.05f, Colors.Magenta.ToUnsignedInteger());
            MBDebug.RenderDebugLine(thisOrigin, dir, Colors.Magenta.ToUnsignedInteger());
            MBDebug.RenderDebugBoxObject(constrainingObject.GlobalBoxMin, constrainingObject.GlobalBoxMax, Colors.Magenta.ToUnsignedInteger());
        }
    }
    public class ConstraintPID
    {

    }
}
