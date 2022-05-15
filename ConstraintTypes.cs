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
        public override Vec3 CalculateConstraintForce(float dt)
        {
            //force to lock translational movement
            Vec3 displacement = targetGlobalFrame.origin - physObjGlobalFrame.origin;
            displacement *= physObject.Mass;

            kPStatic = 75f;
            kDStatic = 2f;

            Vec3 constraintForce = ConstraintLib.VectorPID(displacement, prevDisplacement, dt, kPStatic * kP, kDStatic * kD);

            prevDisplacement = displacement;
            return constraintForce;
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
                //MathLib.DebugMessage("flip");
                prevTorqueVector *= -1;
            }
            Vec3 constraintTorque = ConstraintLib.VectorPID(torqueVector, prevTorqueVector, dt, kPStatic * kP, kDStatic * kD);

            prevTorqueVector = torqueVector;
            prevTorqueSign = torqueSign;
            return constraintTorque;
        }

    }

    public class SCE_ConstraintHinge : SCE_ConstraintSpherical
    {
        public Vec3 HingeRotationAxis = Vec3.Zero;

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

            kPStatic = 55f;
            kDStatic = 1f;

            Vec3 constraintTorque = ConstraintLib.VectorPID(torqueVector, prevTorqueVector, dt, kPStatic * kP, kDStatic * kD);

            prevTorqueVector = torqueVector;
            return constraintTorque;
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
            //sets hinge orientation after scene init
            turnDegrees *= (float)MathLib.DegtoRad;

            targetRotatedMat.ApplyEulerAngles(turnDegrees);
            targetFreeAxis = targetRotatedMat.f;
        }

        public override void RenderEditorHelpers()
        {
            base.RenderEditorHelpers();
            SetHingeRotationAxis(HingeRotationAxis);
            //show rotation axis
            MBDebug.RenderDebugDirectionArrow(physObjGlobalFrame.origin, physObjFreeAxis, Colors.Green.ToUnsignedInteger());
            MBDebug.RenderDebugLine(physObjGlobalFrame.origin, -physObjFreeAxis, Colors.Green.ToUnsignedInteger());
        }

    }
}
