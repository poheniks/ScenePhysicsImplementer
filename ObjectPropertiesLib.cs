﻿using System;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using System.Collections.Generic;

namespace ScenePhysicsImplementer
{
    //force = mass * acceleration
    //torque = moment of inertia * rotational accel
    //KE = 1/2 * mass * velocity^2
    //PE = mass * gravity * height
    //X = latitude
    //Y = longitude
    //Z = altitude
    
    public class ObjectPropertiesLib
    {
        private GameEntity physObject;
        private float mass;

        public Vec3 principalMomentsOfInertia;   //(pitch, roll, yaw) in terms of object's local coordinate system

        public ObjectPropertiesLib(GameEntity physObject)
        {
            this.physObject = physObject;
            mass = this.physObject.Mass;
            FindPrincipalMoI();
        }

        private void FindPrincipalMoI()
        {
            //simplified principle moments of inertia (MoI) based on bounding box, about geometric center
            //http://mechanicsmap.psu.edu/websites/centroidtables/centroids3D/centroids3D.html

            //gets bounding box, including child entities - go off of scene editor bounding box; will result in unrealistically high MoI if the bounding box gets stretched by phantom child entities (like an emptyEntity)
            Vec3 max = physObject.GetBoundingBoxMax();
            Vec3 min = physObject.GetBoundingBoxMin();

            //adjust for scaling - local bounding box dimensions are not affected by scale
            max = MathLib.VectorMultiplyComponents(max, physObject.GetGlobalScale());
            min = MathLib.VectorMultiplyComponents(min, physObject.GetGlobalScale());

            float x = (max - min).x;
            float y = (max - min).y;
            float z = (max - min).z;
            float massFactor = (mass / 12f);

            float Ixx = massFactor * (float)(Math.Pow(y, 2) + Math.Pow(z, 2));
            float Iyy = massFactor * (float)(Math.Pow(x, 2) + Math.Pow(z, 2));
            float Izz = massFactor * (float)(Math.Pow(x, 2) + Math.Pow(y, 2));

            principalMomentsOfInertia = new Vec3(Ixx, Iyy, Izz);
        }

        public static void SetPhysicsAsSphereBody(GameEntity physObject)
        {
            float sphereRadius = CalculateSphereBodyForObject(physObject);
            Vec3 unscaledCenterOfMass = MathLib.VectorMultiplyComponents(physObject.CenterOfMass, MathLib.VectorInverseComponents(physObject.GetGlobalScale()));

            physObject.RemovePhysics();
            physObject.AddSphereAsBody(unscaledCenterOfMass, sphereRadius * 1f, BodyFlags.BodyOwnerEntity);
            physObject.EnableDynamicBody();
            physObject.SetBodyFlags(BodyFlags.BodyOwnerEntity);
        }

        public static float CalculateSphereBodyForObject(GameEntity physObject)
        {
            float sphereRadius = MathLib.AverageVectors(new List<Vec3>() { physObject.GetBoundingBoxMax(), physObject.GetBoundingBoxMin() }).Length;
            Vec3 objGlobalScale = physObject.GetGlobalScale();
            float scaleFactor = objGlobalScale[MathLib.IndexOfAbsMinVectorComponent(objGlobalScale)] / objGlobalScale[MathLib.IndexOfAbsMaxVectorComponent(objGlobalScale)];
            sphereRadius *= scaleFactor;
            return sphereRadius;
        }

        public static MatrixFrame LocalOffsetAndNormalizeGlobalFrame(MatrixFrame frame, Vec3 localOffset)
        {
            frame.rotation.MakeUnit();
            Mat3 rot = frame.rotation;
            frame.origin += rot.s * localOffset.x + rot.f * localOffset.y + rot.u * localOffset.z;  //may need to change to: frame.origin += rot.TransformToParent(localOffset);
            return frame;
        }



        /*
         * overly complicated MoI calculations - use a simplified MoI calc for now
         * current design plan is to hinge constraints about center of mass - a full inertia tensor may be required to stabilize the PID gain for uncentered hinges 
        private void FindInertiaTensorAboutCoM()
        {
            //https://ocw.mit.edu/courses/aeronautics-and-astronautics/16-07-dynamics-fall-2009/lecture-notes/MIT16_07F09_Lec26.pdf; https://www.youtube.com/watch?v=-chgCHuEI4Y

            Vec3 max = physObject.GetBoundingBoxMax();
            Vec3 min = physObject.GetBoundingBoxMin();

            float x = (max - min).x;
            float y = (max - min).y;
            float z = (max - min).z;

            float Ixx = (mass / 3) * (float)(Math.Pow(y, 2) + Math.Pow(z, 2));
            float Iyy = (mass / 3) * (float)(Math.Pow(x, 2) + Math.Pow(z, 2));
            float Izz = (mass / 3) * (float)(Math.Pow(x, 2) + Math.Pow(y, 2));
            float Ixy = (mass / 4) * x * y;
            float Ixz = (mass / 4) * x * z;
            float Iyz = (mass / 4) * y * z;

            //parallel axis theroem
            float xxMd = mass * (float)(Math.Pow(CoM.y, 2) + Math.Pow(CoM.z, 2));
            float yyMd = mass * (float)(Math.Pow(CoM.x, 2) + Math.Pow(CoM.z, 2));
            float zzMd = mass * (float)(Math.Pow(CoM.x, 2) + Math.Pow(CoM.y, 2));
            float xyMd = mass * CoM.x * CoM.y;
            float xzMd = mass * CoM.x * CoM.z;
            float yzMd = mass * CoM.y * CoM.z;

            Ixx -= xxMd;
            Iyy -= yyMd;
            Izz -= zzMd;
            Ixy -= xyMd;
            Ixz -= xzMd;
            Iyz -= yzMd;

            float[,] inertiaTensorArray = new float[3, 3]
            {
                {Ixx, Ixy, Ixz },
                {Ixy, Iyy, Iyz },
                {Ixz, Iyz, Izz }
            };

            principalMomentsOfInertia = new Vec3(Ixx, Iyy, Izz);
            inertiaTensor = new SCEMath.Matrix3x3(inertiaTensorArray);
            
        }
        */
    }
}
