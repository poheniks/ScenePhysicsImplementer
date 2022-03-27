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

        public override Vec3 CalculateConstraintForce(float dt)
        {
            //force to lock translational movement
            Vec3 displacement = targetGlobalFrame.origin - physObjGlobalFrame.origin;
            Vec3 prevDisplacement = prevTargetGlobalFrame.origin - prevPhysObjGlobalFrame.origin;

            displacement *= physObject.Mass;
            prevDisplacement *= physObject.Mass;

            kPStatic = 100f;
            kDStatic = 2.5f;
            Vec3 constraintForce = ConstraintLib.VectorPID(displacement, prevDisplacement, dt, kPStatic * kP, kDStatic * kD);
            return constraintForce;
        }
    }

    public class SCE_ConstraintHinge : SCE_ConstraintSpherical
    {
        public Vec3 HingeOrientation = Vec3.Zero;
        public Vec3 HingeRotationAxis = Vec3.Zero;
        public override Vec3 CalculateConstraintTorque(float dt)
        {
            //rotation changes the free-rotation axis
            //orientation changes this object's facing direction
            physObjMat.ApplyEulerAngles(HingeRotationAxis);
            targetMat.ApplyEulerAngles(HingeOrientation);

            //use forward axis as default free-rotation axis & facing direction
            Vec3 physObjFreeAxis = physObjMat.f;
            Vec3 prevPhysObjFreeAxis = prevPhysObjMat.f;
            Vec3 targetFreeAxis = targetMat.f;
            Vec3 prevTargetFreeAxis = prevTargetMat.f;

            physObjFreeAxis = ConstraintLib.CheckForInverseFreeAxis(physObjFreeAxis, targetFreeAxis);
            prevPhysObjFreeAxis = ConstraintLib.CheckForInverseFreeAxis(prevPhysObjFreeAxis, prevTargetFreeAxis);

            Quaternion rotationQuat = Quaternion.FindShortestArcAsQuaternion(physObjFreeAxis, targetFreeAxis);
            Quaternion prevRotationQuat = Quaternion.FindShortestArcAsQuaternion(prevPhysObjFreeAxis, prevTargetFreeAxis);

            rotationQuat.SafeNormalize();
            prevRotationQuat.SafeNormalize();

            Vec3 torqueVector, prevTorqueVector;
            float angularDisplacement, prevAngularDisplacement;

            Quaternion.AxisAngleFromQuaternion(out torqueVector, out angularDisplacement, rotationQuat);
            Quaternion.AxisAngleFromQuaternion(out prevTorqueVector, out prevAngularDisplacement, prevRotationQuat);

            torqueVector *= angularDisplacement;
            prevTorqueVector *= prevAngularDisplacement;

            Vec3 MoI = physObjProperties.principalMomentsOfInertia;
            torqueVector = MathLib.VectorMultiplyComponents(torqueVector, MoI);
            prevTorqueVector = MathLib.VectorMultiplyComponents(prevTorqueVector, MoI);

            kPStatic = 55f;
            kDStatic = 5f;
            return ConstraintLib.VectorPID(torqueVector, prevTorqueVector, dt, kPStatic * kP, kDStatic * kD);
        }
    }
}
