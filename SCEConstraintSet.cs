using System;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using TaleWorlds.Localization;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.GauntletUI.PrefabSystem;
using System.Collections.Generic;


/*TODO / NOTES:
 * 
 * NOTES:
 * 
 * scaling DOES NOT affect ApplyLocalForceToDynamicBody arguments - both local position & force magnitude are unscaled
 * scaling DOES affect MatrixFrame and Mat3 transforms. If the frame/mat3 is based on a scaled entity, the transforms will consequently return a scaled vector/frame/mat3. Call .MakeUnit() on the Mat3 or MatrixFrame.rotation to avoid scaling on transforms
 * scaling DOES NOT apply to the center of mass (CoM) - why the fuck not TW. Auto-placing CoM in the scene editor inspector on a scaled entity will place it at the unscaled entity's bounding box center. CoM for scaled objects must be typed in manually into the scene editor inspector
 * rotations are heavily biased to the CoM. In fact, there's little point in trying to rotate about anything other than the CoM. It is possible to torque & pivot an entity away from the CoM, basing the pivot on the translational constraint location, but becomes unstable at high rotational velocities - not recommended
 * physics seem to break around 12 dynamic objects (2 parents, 10 hinged children)?
 * 
 * 
 * TODO:
 * 
 * Update weld constraint for optimized torque generation method
 * REFACTOR
 * 
*/
namespace ScenePhysicsImplementer
{
    class SCEConstraintSet
    {
        public GameEntity parent;
        public GameEntity child;
        public SCEObjectPhysLib childObjLib;

        public List<MatrixFrame> weldConstraints = new List<MatrixFrame>(); //inital local frame to weld to

        public List<Tuple<MatrixFrame>> hingeConstraints = new List<Tuple<MatrixFrame>>();
        public Vec3 hingeOrientation = Vec3.Zero;
        public Vec3 hingeRotation = Vec3.Zero;
        public float hingePower = 0;

        private MatrixFrame childPrevGlobalFrame;
        private MatrixFrame parentPrevFrame;

        bool firstTick = true;

        public enum ConstraintType
        {
            Weld,
            Spherical,
            Hinge
        }

        public SCEConstraintSet(GameEntity _parent, GameEntity _child)
        {
            parent = _parent;
            child = _child;
            childObjLib = new SCEObjectPhysLib(child);
        }

        public void AddWeld(MatrixFrame childInitialFrame)
        {
            weldConstraints.Add(childInitialFrame);
            childPrevGlobalFrame = child.GetGlobalFrame();
            parentPrevFrame = parent.GetFrame();
        }

        public void AddHinge(MatrixFrame childInitialFrame)
        {
            hingeConstraints.Add(Tuple.Create(childInitialFrame));
            childPrevGlobalFrame = child.GetGlobalFrame();
            parentPrevFrame = parent.GetFrame();
        }

        public void TickConstraint(float dt)
        {
            if (firstTick)
            {
                firstTick = false;
                return;
            }

            MatrixFrame childFrame = child.GetFrame();
            MatrixFrame childGlobalFrame = child.GetGlobalFrame();
            Mat3 childMat = childFrame.rotation;
            Mat3 childGlobalMat = childGlobalFrame.rotation;


            Vec3 childCoM = child.CenterOfMass;
            Vec3 childLocal = child.GetFrame().origin;


            MatrixFrame parentGlobalFrame = parent.GetGlobalFrame();
            MatrixFrame parentFrame = parent.GetFrame();

            
            childGlobalFrame = SCEConstraintLib.AdjustFrameForCOM(childGlobalFrame, childCoM);
            childGlobalFrame.rotation.MakeUnit();
            Vec3 childGlobalForceCenter = childGlobalFrame.origin;
            

            
            parentGlobalFrame.rotation.MakeUnit();
            Vec3 parentForceCenter = parentGlobalFrame.TransformToLocal(childGlobalForceCenter);


            //forces use global frame
            //(s, f, u) = (x, y, z)
            float p1 = 0f;
            float p2 = 0f;
            float p3 = 0f;
            float p4 = 0f;
            Vec3 localPos = new Vec3(0, 1, 0);
            child.ApplyLocalForceToDynamicBody(childCoM + localPos, child.GetGlobalFrame().rotation.u * p1);
            child.ApplyLocalForceToDynamicBody(childCoM - localPos, -child.GetGlobalFrame().rotation.u * p1);

            Vec3 localPos2 = new Vec3(0, 1, 0);
            child.ApplyLocalForceToDynamicBody(childCoM + localPos2, child.GetGlobalFrame().rotation.s * p2);
            child.ApplyLocalForceToDynamicBody(childCoM - localPos2, -child.GetGlobalFrame().rotation.s * p2);

            Vec3 localPos3 = new Vec3(1, 0, 0);
            child.ApplyLocalForceToDynamicBody(childCoM + localPos3, child.GetGlobalFrame().rotation.u * p3);
            child.ApplyLocalForceToDynamicBody(childCoM - localPos3, -child.GetGlobalFrame().rotation.u * p3);

            child.ApplyLocalForceToDynamicBody(childCoM, -Vec3.Up * p4);
            //

            //hinge constraints
            foreach (Tuple<MatrixFrame> hinge in hingeConstraints)
            {
                MatrixFrame childInitialFrame = hinge.Item1;

                Vec3 force = SCEConstraintLib.CalculateForceForTranslation(parent, child, childInitialFrame, childPrevGlobalFrame, parentPrevFrame, dt);
                Vec3 childForce = force; 
                Vec3 parentForce = -force;


                //MBDebug.RenderDebugSphere(childGlobalFrame.TransformToParent(Vec3.Zero), 0.1f, Colors.Cyan.ToUnsignedInteger());
                //MBDebug.RenderDebugSphere(parentGlobalFrame.TransformToParent(parentForceCenter), 0.15f, Colors.Green.ToUnsignedInteger());

                child.ApplyLocalForceToDynamicBody(childCoM, childForce);
                parent.ApplyLocalForceToDynamicBody(parentForceCenter, parentForce);

                List<Tuple<Vec3, Vec3>> forceCouples = SCEConstraintLib.CalculateForceCouplesForHinge(parent, child, childObjLib, childInitialFrame, childPrevGlobalFrame, parentPrevFrame, hingeOrientation, hingeRotation, hingePower, dt);
                foreach (Tuple<Vec3, Vec3> forceCouple in forceCouples)
                {
                    Vec3 forceLocalPos = forceCouple.Item1;
                    Vec3 forceDir = forceCouple.Item2;
                    Vec3 childForceCouple = forceDir;
                    Vec3 parentForceCouple = -forceDir;

                    Vec3 childGlobalForceCouplePos = childGlobalFrame.TransformToParent(forceLocalPos);
                    Vec3 childGlobalForceCoupleNeg = childGlobalFrame.TransformToParent(-forceLocalPos);

                    Vec3 parentLocalForceCouplePos = parentGlobalFrame.TransformToLocal(childGlobalForceCouplePos);
                    Vec3 parentLocalForceCoupleNeg = parentGlobalFrame.TransformToLocal(childGlobalForceCoupleNeg);

                    Vec3 childGlobalForceCoupleCenter = childGlobalFrame.TransformToParent(forceLocalPos);
                    Vec3 parentLocalCouple = parentGlobalFrame.TransformToLocal(childGlobalForceCoupleCenter);
                    Vec3 childLocalCouple = forceLocalPos;

                    child.ApplyLocalForceToDynamicBody(childCoM + childLocalCouple, childForceCouple); child.ApplyLocalForceToDynamicBody(childCoM - childLocalCouple, -childForceCouple);
                    parent.ApplyLocalForceToDynamicBody(parentLocalForceCouplePos, parentForceCouple); parent.ApplyLocalForceToDynamicBody(parentLocalForceCoupleNeg, -parentForceCouple);



                    /*
                    MBDebug.RenderDebugSphere(childGlobalFrame.TransformToParent(forceLocalPos), 0.2f, Colors.Magenta.ToUnsignedInteger());

                    MBDebug.RenderDebugSphere(parentGlobalFrame.TransformToParent(parentLocalForceCouplePos), 0.12f, Colors.Black.ToUnsignedInteger());
                    MBDebug.RenderDebugDirectionArrow(parentGlobalFrame.TransformToParent(parentLocalForceCouplePos), parentForceCouple.NormalizedCopy(), Colors.Black.ToUnsignedInteger());

                    MBDebug.RenderDebugSphere(parentGlobalFrame.TransformToParent(parentLocalForceCoupleNeg), 0.12f);
                    MBDebug.RenderDebugDirectionArrow(parentGlobalFrame.TransformToParent(parentLocalForceCoupleNeg), -parentForceCouple.NormalizedCopy());
                    
                    MBDebug.RenderDebugSphere(childGlobalFrame.TransformToParent(childLocalCouple), 0.1f, Colors.Magenta.ToUnsignedInteger());
                    MBDebug.RenderDebugDirectionArrow(childGlobalFrame.TransformToParent(childLocalCouple), childForceCouple.NormalizedCopy() * 0.9f, Colors.Magenta.ToUnsignedInteger());

                    MBDebug.RenderDebugSphere(childGlobalFrame.TransformToParent(childLocalCouple), 0.1f, Colors.Magenta.ToUnsignedInteger());
                    MBDebug.RenderDebugDirectionArrow(childGlobalFrame.TransformToParent(childLocalCouple), -childForceCouple.NormalizedCopy() * 0.9f, Colors.Magenta.ToUnsignedInteger());

                    */
                }
            }
            //

            //welded constraints
            foreach (MatrixFrame childInitialFrame in weldConstraints)
            {
                Vec3 force = SCEConstraintLib.CalculateForceForTranslation(parent, child, childInitialFrame, childPrevGlobalFrame, parentPrevFrame, dt);
                child.ApplyLocalForceToDynamicBody(childCoM, force);
                parent.ApplyLocalForceToDynamicBody(parentForceCenter, -force*1f);

                //MBDebug.RenderDebugSphere(parentFrame.TransformToParent(parentLocal), 0.1f, Colors.Black.ToUnsignedInteger());

                List<Tuple<Vec3,Vec3>> forceCouples = SCEConstraintLib.CalculateForceCouplesForLockedRotation(parent, child, childObjLib, childInitialFrame, childPrevGlobalFrame, dt);
                foreach (Tuple<Vec3,Vec3> forceCouple in forceCouples)
                {
                    Vec3 parentCouple = child.GetGlobalFrame().rotation.TransformToParent(forceCouple.Item1);
                    parentCouple = parent.GetFrame().rotation.TransformToLocal(parentCouple);

                    //child.ApplyLocalForceToDynamicBody(childCOM + forceCouple.Item1, forceCouple.Item2); child.ApplyLocalForceToDynamicBody(childCOM - forceCouple.Item1, -forceCouple.Item2);
                    //parent.ApplyLocalForceToDynamicBody(parentForceCenter - parentCouple, forceCouple.Item2); parent.ApplyLocalForceToDynamicBody(parentForceCenter + parentCouple, -forceCouple.Item2);

                    //MBDebug.RenderDebugSphere(parentFrame.TransformToParent(parentLocal + parentCouple), 0.1f, Colors.Black.ToUnsignedInteger());
                    //MBDebug.RenderDebugDirectionArrow(parentFrame.TransformToParent(parentLocal + parentCouple), forceCouple.Item2, Colors.Black.ToUnsignedInteger());

                    //MBDebug.RenderDebugSphere(parentFrame.TransformToParent(parentLocal - parentCouple), 0.1f);
                    //MBDebug.RenderDebugDirectionArrow(parentFrame.TransformToParent(parentLocal - parentCouple), -forceCouple.Item2);
                    
                }
            }
            //
            childPrevGlobalFrame = child.GetGlobalFrame();
            parentPrevFrame = parent.GetFrame();
        }
    }
}   
