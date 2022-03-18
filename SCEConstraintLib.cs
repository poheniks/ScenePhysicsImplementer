using System;
using System.Linq;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using System.Collections.Generic;

namespace ScenePhysicsImplementer
{
    public static class SCEConstraintLib
    {
        public static Vec3 CalculateForceForTranslation(GameEntity parent, GameEntity child, MatrixFrame childInitialFrame, MatrixFrame prevChildGlobalFrame, MatrixFrame prevParentFrame, float dt)
        {
            //dt = Math.Min(Math.Max(dt, 0.006f),0.2f);

            MatrixFrame parentFrame = parent.GetFrame();
            MatrixFrame childFrame = child.GetGlobalFrame();
            MatrixFrame targetFrame = parentFrame.TransformToParent(childInitialFrame); //to global coordinates 
            MatrixFrame prevTargetFrame = prevParentFrame.TransformToParent(childInitialFrame); //to global coordinates 

            Vec3 childCOM = child.CenterOfMass;
            childFrame = AdjustFrameForCOM(childFrame, childCOM);
            prevChildGlobalFrame = AdjustFrameForCOM(prevChildGlobalFrame, childCOM);
            targetFrame = AdjustFrameForCOM(targetFrame, childCOM);
            prevTargetFrame = AdjustFrameForCOM(prevTargetFrame, childCOM);

            //normalize frame rotations, since any vector transforms will carry rotations scaling

            parentFrame.rotation.MakeUnit();
            childFrame.rotation.MakeUnit();
            prevChildGlobalFrame.rotation.MakeUnit();
            targetFrame.rotation.MakeUnit();
            prevTargetFrame.rotation.MakeUnit();

            Vec3 childCurPos = childFrame.origin;
            Vec3 prevChildCurPos = prevChildGlobalFrame.origin;
            Vec3 childInitialPos = targetFrame.origin;
            Vec3 prevChildInitialPos = prevTargetFrame.origin;
            
            Vec3 prevChildDisplacement = prevChildInitialPos - prevChildCurPos; //derivatives must account for parent motion - otherwise jitters occur, especially noticeable at rest
            Vec3 displacement = childInitialPos - childCurPos;

            
            Vec3 deltaDisplacement = (displacement - prevChildDisplacement) / dt;

            float kP = 100f;
            float kD = 2.5f;
            float mass = child.Mass;

            Vec3 dampingForce = deltaDisplacement * mass;
            Vec3 springForce = displacement * mass;

            Vec3 force = springForce * kP + dampingForce * kD;

            //debug force vectors
            //MBDebug.RenderDebugDirectionArrow(childCurPos, force*0.001f);
            //MBDebug.RenderDebugSphere(childInitialPos, 0.1f, Colors.Blue.ToUnsignedInteger());
            //MBDebug.RenderDebugSphere(childCurPos, 0.05f, Colors.Red.ToUnsignedInteger());
            

            //debug matrix rotation vectors
            MBDebug.RenderDebugSphere(childFrame.origin + childFrame.rotation.s, 0.05f, Colors.Red.ToUnsignedInteger());
            MBDebug.RenderDebugSphere(childFrame.origin + childFrame.rotation.f, 0.05f, Colors.Green.ToUnsignedInteger());
            MBDebug.RenderDebugSphere(childFrame.origin + childFrame.rotation.u, 0.05f, Colors.Blue.ToUnsignedInteger());

            MBDebug.RenderDebugSphere(childFrame.origin + targetFrame.rotation.s, 0.025f, Colors.Red.ToUnsignedInteger());
            MBDebug.RenderDebugSphere(childFrame.origin + targetFrame.rotation.f, 0.025f, Colors.Green.ToUnsignedInteger());
            MBDebug.RenderDebugSphere(childFrame.origin + targetFrame.rotation.u, 0.025f, Colors.Blue.ToUnsignedInteger());
            

            return force;
            
        }

        public static List<Tuple<Vec3, Vec3>> CalculateForceCouplesForHinge(GameEntity parent, GameEntity child, SCEObjectPhysLib objLib, MatrixFrame childInitialFrame, MatrixFrame prevChildGlobalFrame, MatrixFrame prevParentFrame, Vec3 hingeOrientation, Vec3 hingeRotation, float hingePower, float dt)
        {
            //dt = Math.Min(Math.Max(dt, 0.006f), 0.2f);

            MatrixFrame parentFrame = parent.GetFrame();
            MatrixFrame childGlobalFrame = child.GetGlobalFrame();
            MatrixFrame targetFrame = parentFrame.TransformToParent(childInitialFrame); //global
            MatrixFrame prevTargetFrame = prevParentFrame.TransformToParent(childInitialFrame); //global

            Vec3 childCOM = child.CenterOfMass;
            childGlobalFrame = AdjustFrameForCOM(childGlobalFrame, childCOM);
            prevChildGlobalFrame = AdjustFrameForCOM(prevChildGlobalFrame, childCOM);
            targetFrame = AdjustFrameForCOM(targetFrame, childCOM);
            prevTargetFrame = AdjustFrameForCOM(prevTargetFrame, childCOM);


            //normalize frame rotations, since any vector transforms will carry rotations scaling
            parentFrame.rotation.MakeUnit();
            childGlobalFrame.rotation.MakeUnit();
            prevChildGlobalFrame.rotation.MakeUnit();
            targetFrame.rotation.MakeUnit();
            prevTargetFrame.rotation.MakeUnit();


            Mat3 childMat = childGlobalFrame.rotation;
            Mat3 prevChildMat = prevChildGlobalFrame.rotation;
            Mat3 targetMat = targetFrame.rotation;
            Mat3 prevTargetMat = prevTargetFrame.rotation;

            //hinge orientation changes rotation axis
            //hinge rotation changes facing axis
            childMat.ApplyEulerAngles(hingeOrientation);
            prevChildMat.ApplyEulerAngles(hingeOrientation);
            targetMat.ApplyEulerAngles(hingeRotation);
            prevTargetMat.ApplyEulerAngles(hingeRotation);

            //local to parent axes; use forward axis as default
            Vec3 childFreeAxis = childMat.f; 
            Vec3 prevChildFreeAxis = prevChildMat.f;
            Vec3 targetFreeAxis = targetMat.f;
            Vec3 prevTargetFreeAxis = prevTargetMat.f;

            childFreeAxis = CheckGetInverseFreeAxis(childFreeAxis, targetFreeAxis);
            prevChildFreeAxis = CheckGetInverseFreeAxis(prevChildFreeAxis, prevTargetFreeAxis);

            Quaternion torqueQuat = Quaternion.FindShortestArcAsQuaternion(childFreeAxis, targetFreeAxis);
            Quaternion prevTorqueQuat = Quaternion.FindShortestArcAsQuaternion(prevChildFreeAxis, prevTargetFreeAxis);

            torqueQuat.SafeNormalize();
            prevTorqueQuat.SafeNormalize();

            Vec3 curTorque, prevTorque;
            float curAng, prevAng;
            Quaternion.AxisAngleFromQuaternion(out curTorque, out curAng, torqueQuat);
            Quaternion.AxisAngleFromQuaternion(out prevTorque, out prevAng, prevTorqueQuat);

            float kP = 55f;
            float kD = 5f;

            Vec3 MoI = objLib.principalMomentsOfInertia;
            //MoI = Vec3.One;

            Vec3 proportion = curTorque*curAng;
            Vec3 derivative = (curTorque*curAng - prevTorque*prevAng)/dt;

            Tuple<Vec3, Vec3> forceCouple = GenerateGlobalForceCoupleFromLocalTorque(childGlobalFrame, SCEMath.VectorMultiplyComponents(proportion*kP + derivative*kD + childMat.TransformToParent(new Vec3(0,hingePower)), MoI));
            Vec3 forcePos = forceCouple.Item1;
            Vec3 forceDir = forceCouple.Item2;
            Tuple<Vec3, Vec3> curUnitForceCouple = forceCouple;
            Tuple<Vec3, Vec3> invForceCouple = Tuple.Create(-forcePos, -forceDir);

            //debug block
            /*
            Vec3 debugTorque = curTorque;
            //float error = (float)Math.Round(Vec3.DotProduct(curTorque, Vec3.CrossProduct(curUnitForceCouple.Item1,curForce)),2);
            float error = (float)Math.Round(Vec3.DotProduct(forceDir, childFreeAxis),2);
            float debugAngleOut = curAng;

            Vec3 debugVec = SCEMath.VectorRound(debugTorque, 2);
            float debugAng = (float)Math.Round(debugAngleOut * (float)SCEMath.RadtoDeg, 2);
            SCEMath.DebugMessage("vec: " + debugVec + "|ang: " + debugAng + "|error: " + error);

            MBDebug.RenderDebugDirectionArrow(childFrame.origin, curTorque);
            //MBDebug.RenderDebugDirectionArrow(childFrame.TransformToParent(curUnitForceCouple.Item1), propForce.NormalizedCopy(), color: Colors.Green.ToUnsignedInteger());
            //MBDebug.RenderDebugDirectionArrow(childFrame.TransformToParent(curUnitForceCouple.Item1), derivativeForce.NormalizedCopy(), color: Colors.Magenta.ToUnsignedInteger());
            
            */
            //
            MBDebug.RenderDebugDirectionArrow(childGlobalFrame.TransformToParent(curUnitForceCouple.Item1), forceDir.NormalizedCopy(), color: Colors.Blue.ToUnsignedInteger());
            List<Tuple<Vec3, Vec3>> forceCouples = new List<Tuple<Vec3, Vec3>>();
            forceCouples.Add(forceCouple);
            forceCouples.Add(invForceCouple);

            return forceCouples;
        }


        public static List<Tuple<Vec3, Vec3>> CalculateForceCouplesForLockedRotation(GameEntity parent, GameEntity child, SCEObjectPhysLib objLib, MatrixFrame childInitialFrame, MatrixFrame prevChildGlobalFrame, float dt)
        {
            MatrixFrame parentFrame = parent.GetFrame();
            MatrixFrame childFrame = child.GetGlobalFrame();
            childFrame = AdjustFrameForCOM(childFrame, child.CenterOfMass);
            prevChildGlobalFrame = AdjustFrameForCOM(prevChildGlobalFrame, child.CenterOfMass);

            MatrixFrame targetFrame = parentFrame.TransformToParent(childInitialFrame); //global
            targetFrame.origin += child.CenterOfMass;

            Mat3 childMat = childFrame.rotation;
            Mat3 prevChildMat = prevChildGlobalFrame.rotation;
            Mat3 targetMat = targetFrame.rotation;

            Quaternion childQuat = childMat.ToQuaternion();
            Quaternion prevChildQuat = prevChildMat.ToQuaternion();
            Quaternion targetQuat = targetMat.ToQuaternion();

            childQuat.SafeNormalize();
            prevChildQuat.SafeNormalize();
            targetQuat.SafeNormalize();

            Quaternion curQuat = targetQuat.TransformToLocal(childQuat);
            Quaternion prevQuat = targetQuat.TransformToLocal(prevChildQuat);

            List<Tuple<Vec3, Vec3>> forceCouples = new List<Tuple<Vec3, Vec3>>();
            if (curQuat == prevQuat) return forceCouples;   //investigate whether this causes instabilities with the derivative loop

            List<Quaternion> quaternions = new List<Quaternion>() {curQuat, prevQuat};
            Dictionary<Quaternion, Tuple<Vec3, float, int>> torqueVectors = new Dictionary<Quaternion, Tuple<Vec3, float, int>>();
            foreach (Quaternion quat in quaternions)
            {

                Vec3 torque;
                float rotationAngle;
                Quaternion.AxisAngleFromQuaternion(out torque, out rotationAngle, quat);
                int torqueDir = GetSignForAxisAngleRotation(rotationAngle);
                rotationAngle = GetAngleBetween180(rotationAngle);
                torque *= torqueDir;

                torqueVectors.Add(quat, Tuple.Create(torque, rotationAngle, torqueDir));
            }

            Vec3 curTorque = torqueVectors[curQuat].Item1;
            Vec3 prevTorque = torqueVectors[prevQuat].Item1;

            float curRotation = torqueVectors[curQuat].Item2;
            float prevRotation = torqueVectors[prevQuat].Item2;

            float iX = objLib.principalMomentsOfInertia.x;
            float iY = objLib.principalMomentsOfInertia.y;
            float iZ = objLib.principalMomentsOfInertia.z;

            List<Tuple<Vec3, Vec3, Vec3, float>> forcingAxes = new List<Tuple<Vec3, Vec3, Vec3, float>>()
            {
                Tuple.Create(Vec3.Up, childFrame.rotation.f, Vec3.Side, iY),    //inertia components ordered based on how force couple directions are set up against obj orientation (rotation matrix vecs) 
                Tuple.Create(Vec3.Side, childFrame.rotation.u, Vec3.Forward, iX),
                Tuple.Create(Vec3.Forward, childFrame.rotation.s, Vec3.Up, iZ)
            };
            
            foreach (Tuple<Vec3,Vec3, Vec3, float> forceAxis in forcingAxes)
            {
                Vec3 loadPos = forceAxis.Item1; //may be simpler to find the position in terms of principal axes
                Vec3 loadDir = forceAxis.Item2;
                Vec3 torqueAxis = forceAxis.Item3;
                float inertia = forceAxis.Item4;

                float curTorqueComponent = Vec3.DotProduct(curTorque, torqueAxis);
                float prevTorqueComponent = Vec3.DotProduct(prevTorque, torqueAxis);

                int curTorqueSign = Math.Sign(curTorqueComponent);
                int prevTorqueSign = Math.Sign(prevTorqueComponent);

                float changeTorqueAngle = Vec3.AngleBetweenTwoVectors(curTorque, prevTorque);

                if (changeTorqueAngle > (float)Math.PI * 0.25f)
                {
                    curRotation = 0; prevRotation = 0;
                }

                Vec3 curProp = loadDir * curTorqueComponent * curRotation;
                Vec3 prevProp = loadDir * prevTorqueComponent * prevRotation;
                Vec3 diffProp = curProp - prevProp;

                float kP = 35f;
                float kD = 2f;
                
                Vec3 proportion = loadDir * curTorqueComponent * curRotation;
                Vec3 derivative = (diffProp / dt);

                if (changeTorqueAngle > (float)Math.PI * 0.95f | (curRotation > (float)Math.PI * 0.95f & prevRotation > (float)Math.PI * 0.95f)) derivative = Vec3.Zero;

                Vec3 force = proportion * kP + derivative * kD;
                force *= inertia;

                //debug force couple vectors
                //MBDebug.RenderDebugDirectionArrow(childFrame.origin + childMat.TransformToParent(loadPos), proportion * kP * 2, Colors.Magenta.ToUnsignedInteger());
                //MBDebug.RenderDebugDirectionArrow(childFrame.origin + childMat.TransformToParent(loadPos), derivative * kD * 2, Colors.Green.ToUnsignedInteger());

                forceCouples.Add(Tuple.Create(loadPos, force));
            }

            //debug torque vector, local to child orientation
            //MBDebug.RenderDebugDirectionArrow(childFrame.origin, childMat.TransformToParent(curTorque));
            //MBDebug.RenderDebugDirectionArrow(childFrame.origin, childMat.TransformToParent(prevTorque), Colors.Black.ToUnsignedInteger());

            return forceCouples;
        }

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

        public static Vec3 GetVectorAngularVelocity(Vec3 cur, Vec3 prev, float dt)
        {
            Vec3 velocity = (cur - prev) / dt;

            return velocity;
        }

        public static MatrixFrame AdjustFrameForCOM(MatrixFrame frame, Vec3 centerOfMass)
        {
            Mat3 rot = frame.rotation;
            rot.MakeUnit();
            frame.origin += rot.s * centerOfMass.x + rot.f * centerOfMass.y + rot.u * centerOfMass.z;
            return frame;
        }

        public static Vec3 CheckGetInverseFreeAxis(Vec3 curAxis, Vec3 targetAxis)
        {
            //prevents the quaternion solution from generating a perpendicular off-axis torque when child and target axes are 180-degrees apart
            //occurs at approximately child.f = -target.f 
            Vec3 invCurAxis = -curAxis;
            float cur = Vec3.DotProduct(curAxis, targetAxis);
            if ( cur < -1 )
            {
                SCEMath.DebugMessage("inversed");
                return invCurAxis;
            }
            
            return curAxis;
        }

        public static Tuple<Vec3,Vec3> GenerateGlobalForceCoupleFromLocalTorque(MatrixFrame globalFrame, Vec3 torque)
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

            //constrain torque composition equation by setting max moment arm to 1, max dir to max torque component
            //torque = new Vec3(2, 7, 4);
            //T(y) = F(x)*L(z) + F(z)*L(x), where F(x) = T(max); L(z) = 1
            int indexOfMaxTorqueComponent = SCEMath.IndexOfAbsMaxVectorComponent(torque);
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
    }
}

