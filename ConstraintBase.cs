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
    //DO EVERYTHING IN GLOBAL COORDINATES
    public abstract class ConstraintBase : ScriptComponentBehavior
    {
        public string ConstrainingObjectTag = "";
        public bool ToggleEditorHelpers = true;
        public bool DisableParentReaction = false;
        public float kP = 1f;
        public float kD = 1f;

        public float kPStatic { get; private set; } = 100f;
        public float kDStatic { get; private set; } = 2.5f;

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

        //frames from previous tick
        private MatrixFrame _prevPhysObjGlobalFrame;
        private MatrixFrame _prevConstrainingObjectGlobalFrame;
        private MatrixFrame _prevTargetGlobalFrame;
        public MatrixFrame prevPhysObjGlobalFrame { get { return _prevPhysObjGlobalFrame; } private set { _prevPhysObjGlobalFrame = value; } }
        public MatrixFrame prevConstrainingObjectGlobalFrame { get { return _prevConstrainingObjectGlobalFrame; } private set { _prevConstrainingObjectGlobalFrame = value; } }
        public MatrixFrame prevTargetGlobalFrame { get { return _prevTargetGlobalFrame; } private set { _prevTargetGlobalFrame = value; } }

        private bool firstFrame = true;
        Vec3 physObjCoM;  //always local coordinates
        private List<Tuple<Vec3,Vec3>> forces = new List<Tuple<Vec3, Vec3>>();

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

            physObjCoM = physObject.CenterOfMass;  //does NOT return vector lengths scaled to child matrix frames - vector lengths always global

            InitializeFrames(); //set MatrixFrame fields
            
            kPStatic *= physObject.Mass;
            kDStatic *= physObject.Mass;
        }

        private void InitializeFrames()
        {
            base.SetScriptComponentToTick(this.GetTickRequirement());
            UpdateCurrentFrames();

            firstFrame = false;
            targetInitialLocalFrame = constrainingObjGlobalFrame.TransformToLocal(physObjGlobalFrame);
            targetGlobalFrame = constrainingObjGlobalFrame.TransformToParent(targetInitialLocalFrame);
            //targetGlobalFrame = ObjectPropertiesLib.AdjustFrameForCOM(targetGlobalFrame, physObjCoM);

            //set previous frames to current, on first tick - since no previous frames exist yet
            prevConstrainingObjectGlobalFrame = constrainingObjGlobalFrame;
            prevPhysObjGlobalFrame = physObjGlobalFrame;
            prevTargetGlobalFrame = targetGlobalFrame;
        }

        protected override void OnTickParallel(float dt)
        {
            if (!isValid) return;
            UpdateCurrentFrames();
            TickForce(ApplyForce(dt));
            ApplyTorque(dt);
            UpdatePreviousFrames();
        }

        public void UpdateCurrentFrames()
        {
            //set current frames
            physObjGlobalFrame = physObject.GetGlobalFrame();
            constrainingObjGlobalFrame = constrainingObject.GetGlobalFrame();
            
            //unscale frames; everything is in terms of physObj, so adjust target & this entity's frames by this entity's CoM
            physObjGlobalFrame = ObjectPropertiesLib.AdjustFrameForCOM(physObjGlobalFrame, physObjCoM);
            constrainingObjGlobalFrame = ObjectPropertiesLib.AdjustFrameForCOM(constrainingObjGlobalFrame, physObjCoM);

            if (firstFrame) return;
            targetGlobalFrame = constrainingObjGlobalFrame.TransformToParent(targetInitialLocalFrame);
            //targetGlobalFrame = ObjectPropertiesLib.AdjustFrameForCOM(targetGlobalFrame, physObjCoM);
        }

        public void UpdatePreviousFrames()
        {
            prevConstrainingObjectGlobalFrame = constrainingObjGlobalFrame;
            prevPhysObjGlobalFrame = physObjGlobalFrame;
            prevTargetGlobalFrame = targetGlobalFrame;
        }

        public abstract Vec3 ApplyForce(float dt);
        public abstract Vec3 ApplyTorque(float dt);
        
        private void TickForce(Vec3 force)
        {
            MatrixFrame physObjTrueGlobalFrame = physObject.GetGlobalFrame();
            MatrixFrame constraintObjTrueGlobalFrame = constrainingObject.GetGlobalFrame();

            physObjTrueGlobalFrame.rotation.MakeUnit();
            constraintObjTrueGlobalFrame.rotation.MakeUnit();

            Vec3 localPhysObjOrigin = physObjTrueGlobalFrame.TransformToLocal(physObjGlobalFrame.origin);
            Vec3 localConstraintObjOrigin = constraintObjTrueGlobalFrame.TransformToLocal(physObjGlobalFrame.origin);

            physObject.ApplyLocalForceToDynamicBody(localPhysObjOrigin, force);
            if (!DisableParentReaction) constrainingObject.ApplyLocalForceToDynamicBody(localConstraintObjOrigin, -force);

            /*
            Vec3 debugPhysLoc = physObjTrueGlobalFrame.TransformToParent(localPhysObjOrigin);
            Vec3 debugConstraintLoc = constraintObjTrueGlobalFrame.TransformToParent(localConstraintObjOrigin);

            MBDebug.RenderDebugSphere(targetGlobalFrame.origin, 0.1f, Colors.Magenta.ToUnsignedInteger());

            MBDebug.RenderDebugSphere(debugPhysLoc, 0.1f, Colors.Green.ToUnsignedInteger());
            MBDebug.RenderDebugDirectionArrow(debugPhysLoc, force, Colors.Green.ToUnsignedInteger());

            MBDebug.RenderDebugSphere(debugConstraintLoc, 0.1f, Colors.Blue.ToUnsignedInteger());
            MBDebug.RenderDebugDirectionArrow(debugConstraintLoc, -force, Colors.Blue.ToUnsignedInteger());
            */
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

    public class SCE_ConstraintSpherical : ConstraintBase
    {

        public override Vec3 ApplyForce(float dt)
        {
            Vec3 displacement = targetGlobalFrame.origin - physObjGlobalFrame.origin;
            Vec3 prevDisplacement = prevTargetGlobalFrame.origin - prevPhysObjGlobalFrame.origin;
            Vec3 velocity = (displacement - prevDisplacement) / dt;

            return ConstraintLib.VectorPID(displacement, velocity, kPStatic*kP, kDStatic*kD);
        }
        public override Vec3 ApplyTorque(float dt)
        {
            return Vec3.Zero;
        }
    }
}
