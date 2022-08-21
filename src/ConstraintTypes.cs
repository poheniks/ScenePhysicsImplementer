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

    public class SCE_ConstraintSpherical : ConstraintBase
    {
        private Vec3 prevDisplacement;
        public override string constraintAdjective
        {
            get { return "Spherical constrained"; }
            set { }
        }


        public override Vec3 CalculateConstraintForce(float dt)
        {
            //force to lock translational movement
            Vec3 displacement = targetGlobalFrame.origin - physObjGlobalFrame.origin;
            displacement *= physObject.Mass;

            kPStatic = 75f;
            kDStatic = 2f;

            Vec3 constraintForce = ConstraintLib.VectorPID(displacement, prevDisplacement, dt, kPStatic * PDGain.x, kDStatic * PDGain.z);

            prevDisplacement = displacement;
            return constraintForce * ConstraintStiffness;
        }

        public override void RenderForceDebuggers(Vec3 physObjLocalForcePos, Vec3 constraintObjLocalForcePos, Vec3 forceDir)
        {
            base.RenderForceDebuggers(physObjLocalForcePos, constraintObjLocalForcePos, forceDir);
            MBDebug.RenderDebugSphere(physObjGlobalFrame.origin + physObjGlobalFrame.rotation.s, 0.05f, Colors.Red.ToUnsignedInteger());
            MBDebug.RenderDebugSphere(physObjGlobalFrame.origin + physObjGlobalFrame.rotation.f, 0.05f, Colors.Green.ToUnsignedInteger());
            MBDebug.RenderDebugSphere(physObjGlobalFrame.origin + physObjGlobalFrame.rotation.u, 0.05f, Colors.Blue.ToUnsignedInteger());

            MBDebug.RenderDebugSphere(physObjGlobalFrame.origin + targetGlobalFrame.rotation.s, 0.025f, Colors.Red.ToUnsignedInteger());
            MBDebug.RenderDebugSphere(physObjGlobalFrame.origin + targetGlobalFrame.rotation.f, 0.025f, Colors.Green.ToUnsignedInteger());
            MBDebug.RenderDebugSphere(physObjGlobalFrame.origin + targetGlobalFrame.rotation.u, 0.025f, Colors.Blue.ToUnsignedInteger());
        }
    }

    public class SCE_ConstraintWeld : SCE_ConstraintSpherical
    {
        public override string constraintAdjective
        {
            get { return "Welded"; }
            set { }
        }

        private Vec3 prevTorqueVector;
        private int prevTorqueSign;
        public override Vec3 CalculateConstraintTorque(float dt)
        {
            Quaternion physObjQuat = Quaternion.QuaternionFromMat3(physObjMat);
            Quaternion targetQuat = Quaternion.QuaternionFromMat3(targetMat);

            physObjQuat.SafeNormalize();
            targetQuat.SafeNormalize();

            Quaternion torqueQuat = targetQuat.TransformToLocal(physObjQuat);

            Vec3 torqueVector;
            float angularDisplacement;
            Quaternion.AxisAngleFromQuaternion(out torqueVector, out angularDisplacement, torqueQuat);

            int torqueSign = ConstraintLib.GetSignForAxisAngleRotation(angularDisplacement);
            angularDisplacement = ConstraintLib.GetAngleBetween180(angularDisplacement);

            torqueVector *= -torqueSign;
            torqueVector *= angularDisplacement;
            torqueVector = MathLib.VectorMultiplyComponents(torqueVector, MoI);

            torqueVector = physObjMat.TransformToParent(torqueVector);

            kPStatic = 25f;
            kDStatic = 1f;

            if (torqueSign != prevTorqueSign && (angularDisplacement > (float)Math.PI*0.95f | angularDisplacement < -(float)Math.PI*0.95f))
            {
                //this is possibly unstable (in terms of constraint stability, not code execution), but have not run into issues yet
                prevTorqueVector *= -1;
            }
            Vec3 constraintTorque = ConstraintLib.VectorPID(torqueVector, prevTorqueVector, dt, kPStatic * PDGain.x, kDStatic * PDGain.z);

            prevTorqueVector = torqueVector;
            prevTorqueSign = torqueSign;
            Console.WriteLine($"{physObject.Name} + {physObjGlobalFrame.origin}");
            return constraintTorque * ConstraintStiffness;
        }
    }

    public class SCE_ConstraintHinge : SCE_ConstraintSpherical
    {
        //editor fields
        public Vec3 HingeRotationAxis = Vec3.Zero;

        public override string constraintAdjective
        {
            get { return "Hinged"; }
            set { }
        }

        [EditorVisibleScriptComponentVariable(false)]
        public Vec3 HingeTurnDegrees { get; set; }

        public Vec3 initialHingeRotation { get; private set; }
        public Vec3 physObjFreeAxis { get; private set; }
        public Vec3 targetFreeAxis { get; private set; }

        private Vec3 prevTorqueVector;
        private Mat3 physObjRotatedMat;
        private Mat3 targetRotatedMat;

        public override void InitializePhysics()
        {
            base.InitializePhysics();
            initialHingeRotation = HingeRotationAxis;
            SetHingeRotationAxis(HingeRotationAxis);
        }

        public override Vec3 CalculateConstraintTorque(float dt)
        {
            SetHingeRotationAxis(HingeRotationAxis);
            TurnHinge(HingeTurnDegrees);
            physObjFreeAxis = ConstraintLib.CheckForInverseFreeAxis(physObjFreeAxis, targetFreeAxis);

            Quaternion rotationQuat = Quaternion.FindShortestArcAsQuaternion(physObjFreeAxis, targetFreeAxis);
            rotationQuat.SafeNormalize();

            Vec3 torqueVector;
            float angularDisplacement;

            Quaternion.AxisAngleFromQuaternion(out torqueVector, out angularDisplacement, rotationQuat);

            torqueVector *= angularDisplacement;
            torqueVector = MathLib.VectorMultiplyComponents(torqueVector, MoI);

            kPStatic = 35f;
            kDStatic = 1f;

            Vec3 constraintTorque = ConstraintLib.VectorPID(torqueVector, prevTorqueVector, dt, kPStatic * PDGain.x, kDStatic * PDGain.z);

            prevTorqueVector = torqueVector;
            Console.WriteLine($"{physObject.Name} + {physObjGlobalFrame.origin}");
            return constraintTorque * ConstraintStiffness;
        }
        public void SetHingeRotationAxis(Vec3 hingeRotationAxis)
        {
            //sets base hinge orientation at scene init
            hingeRotationAxis *= (float)MathLib.DegtoRad;

            physObjRotatedMat = physObjMat;
            targetRotatedMat = targetMat;
            physObjRotatedMat.ApplyEulerAngles(hingeRotationAxis);
            targetRotatedMat.ApplyEulerAngles(hingeRotationAxis);

            //use forward axis as default free-rotation axis & facing direction
            physObjFreeAxis = physObjRotatedMat.f;
            targetFreeAxis = physObjRotatedMat.f;
        }

        private void TurnHinge(Vec3 turnDegrees)
        {
            //dynamically sets hinge direction for controller scripts
            turnDegrees *= (float)MathLib.DegtoRad;

            targetRotatedMat.ApplyEulerAngles(turnDegrees);
            targetFreeAxis = targetRotatedMat.f;
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            base.OnEditorVariableChanged(variableName);
        }

        public override void RenderEditorHelpers()
        {
            base.RenderEditorHelpers();

            SetHingeRotationAxis(HingeRotationAxis);
            //show rotation axis direction and position
            MBDebug.RenderDebugDirectionArrow(physObjGlobalFrame.origin, physObjFreeAxis, Colors.Green.ToUnsignedInteger());
            MBDebug.RenderDebugLine(physObjGlobalFrame.origin, -physObjFreeAxis, Colors.Green.ToUnsignedInteger());
            
            MBDebug.RenderDebugText3D(physObjGlobalFrame.origin + physObjFreeAxis, "Rotation axis", screenPosOffsetX: 15, screenPosOffsetY: -10);
        }

        public override void DisplayHelpText()
        {
            base.DisplayHelpText();
            MathLib.HelpText(nameof(HingeRotationAxis), "Sets the rotation axis of the hinge");
        }
    }
}
