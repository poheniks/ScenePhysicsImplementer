using System;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using System.Collections.Generic;

namespace ScenePhysicsImplementer
{
    public static class ConstraintLib
    {
        public static int GetSignForAxisAngleRotation(float angle)
        {
            if (angle > (float)Math.PI | angle < 0) return -1;
            return 1;
        }

        public static float GetAngleBetween180(float angle)
        {
            float pi = (float)Math.PI;
            if (angle > pi) return Math.Abs(angle - 2*pi);
            if (angle < -pi) return Math.Abs(angle + 2*pi);
            return angle;
        }

        public static Vec3 CheckForInverseFreeAxis(Vec3 curAxis, Vec3 targetAxis)
        {
            //prevents the quaternion solution from generating a perpendicular off-axis torque when child and target axes are 180-degrees apart
            //occurs at approximately child.f = -target.f 
            Vec3 invCurAxis = -curAxis;
            float cur = Vec3.DotProduct(curAxis, targetAxis);

            if ( cur < -1 ) return invCurAxis;
            return curAxis;
        }

        //i am very proud of this, even if it is mathematically simple :)
        public static Tuple<Vec3,Vec3> GenerateGlobalForceCoupleFromGlobalTorque(MatrixFrame globalFrame, Vec3 torque)
        {
            Vec3 forcePos = Vec3.Zero;
            Vec3 forceDir = Vec3.Zero;
            if (!torque.IsNonZero) return Tuple.Create(forcePos, forceDir); //physics break without a zero-check

            //globalFrame.rotation.MakeUnit();
            torque = globalFrame.rotation.TransformToLocal(torque);

            //generate torque about entity local rotation (s, f, u)
            //(x, y, z) = (s, f, u)
            /*
             *T = F x L
             *T(x) = F(z)*L(y) - F(y)*L(z)
             *T(y) = F(x)*L(z) - F(z)*L(x)
             *T(z) = F(y)*L(x) - F(x)*L(y)
            */

            //constrain torque composition equation by setting max moment arm to 1, max dir to max input torque component
            //torque = new Vec3(2, 7, 4);
            //T(y) = F(x)*L(z) + F(z)*L(x), where F(x) = T(max); L(z) = 1
            int indexOfMaxTorqueComponent = MathLib.IndexOfAbsMaxVectorComponent(torque);
            int indexShiftRight = indexOfMaxTorqueComponent + 1;   
            if (indexShiftRight > 2) indexShiftRight = 0;
            int indexShiftLeft = indexOfMaxTorqueComponent - 1;
            if (indexShiftLeft < 0) indexShiftLeft = 2;

            forcePos[indexShiftRight] = 1;
            forceDir[indexShiftLeft] = torque[indexOfMaxTorqueComponent];

            //solve T(max) component equation by setting the other force & length term to zeros (because first set of constraints satisfy the torque component equation)
            //F(z) = 0; L(x) = 0; T(y) = F(x)*L(z); F(x) = T(y); L(z) = 1
            forcePos[indexShiftLeft] = 0;
            forceDir[indexShiftRight] = 0;

            //solve for remaining force & moment arm
            //F(y) = T(x); L(y) = -T(z)/T(y)
            forceDir[indexOfMaxTorqueComponent] = -torque[indexShiftLeft];
            forcePos[indexOfMaxTorqueComponent] = -torque[indexShiftRight] / torque[indexOfMaxTorqueComponent];

            /*debug math checks
            float xError = torque[0] - (forceDir[2] * forcePos[1] - forceDir[1] * forcePos[2]);
            float yError = torque[1] - (forceDir[0] * forcePos[2] - forceDir[2] * forcePos[0]);
            float zError = torque[2] - (forceDir[1] * forcePos[0] - forceDir[0] * forcePos[1]);
             */

            //transform force dir from local to global; keep force pos local
            forceDir = globalFrame.rotation.TransformToParent(forceDir);

            return Tuple.Create(forcePos, forceDir);
        }

        public static Vec3 VectorPID(Vec3 curError, Vec3 prevError, float dt, float kP, float kD)
        {
            //integral term not implemented
            Vec3 proportional = curError;
            Vec3 derivative = (curError - prevError) / dt;
            return proportional * kP + derivative * kD;

        }
    }
}

