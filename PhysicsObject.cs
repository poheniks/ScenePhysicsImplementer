using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace ScenePhysicsImplementer
{
    public class PhysicsObject : ScriptComponentBehavior    //NEEDS TO BE RENAMED TO SCE_PhysicsObject - old SCE_PhysicsObject class to be obliterated 
    {
        public SimpleButton SetNoCollideFlagForStaticChildObjects;
        public bool SetPhysicsBodyAsSphere = false;
        public bool DisableGravity = false;
        public bool DisableAllCollisions = false;
        public float LinearDamping = 1.0f;
        public float AngularDamping = 1.0f;

        public GameEntity physObject { get; private set; }
        public ObjectPropertiesLib physObjProperties { get; private set; }
        public Vec3 MoI { get; private set; }
        public float mass { get; private set; }
        public Vec3 physObjCoM { get; private set; }  //always in object's local coordinate system, but never scaled with object
        public List<PhysicsMaterial> physicsMaterialsRemovedOnCollision { get; private set; }

        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.Tick;
        }
        protected override void OnEditorInit()
        {
            base.OnEditorInit();
            InitializeScene();
        }

        protected override void OnInit()
        {
            base.OnInit();
            InitializeScene();
            InitializePhysics();
            InitializePhysicsMaterialTypesOnCollision();
        }

        public virtual void InitializeScene()
        {
            physObject = base.GameEntity;
            mass = physObject.Mass;
            UpdateCenterOfMass();
        }

        public void UpdateCenterOfMass()
        {
            physObjCoM = physObject.CenterOfMass;  //does NOT return CoM vector lengths scaled to child matrix frames - CoM vector lengths always global
        }

        public virtual void InitializePhysics()
        {
            if (SetPhysicsBodyAsSphere) ObjectPropertiesLib.SetPhysicsAsSphereBody(physObject);

            physObject.EnableDynamicBody();
            physObject.SetBodyFlags(BodyFlags.Dynamic);
            physObject.SetDamping(LinearDamping, AngularDamping);

            physObjProperties = new ObjectPropertiesLib(physObject);
            MoI = physObjProperties.principalMomentsOfInertia;
            physObject.SetMassSpaceInertia(MoI);    //simplify mass moments of inertia to a cubic volume based on entity bounding box

            if (DisableGravity) physObject.DisableGravity();
            if (DisableAllCollisions) physObject.SetBodyFlags(BodyFlags.CommonCollisionExcludeFlagsForAgent & ~BodyFlags.Disabled);
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            base.OnEditorVariableChanged(variableName);
            if (variableName == nameof(SetNoCollideFlagForStaticChildObjects)) SetDynamicConvexFlags();
        }

        private void SetDynamicConvexFlags()
        {
            foreach (GameEntity child in physObject.GetChildren())
            {
                IEnumerable<ScriptComponentBehavior> scriptComponentBehaviors = child.GetScriptComponents();
                IEnumerator<ScriptComponentBehavior> script = scriptComponentBehaviors.GetEnumerator();
                script.MoveNext();
                if (script.Current != null) continue;   //only set static child objects to no-collide
                child.AddBodyFlags(BodyFlags.DynamicConvexHull);    
            }
        }

        protected override void OnPhysicsCollision(ref PhysicsContact contact)
        {
            base.OnPhysicsCollision(ref contact);
            for(int i = 0; i < contact.NumberOfContactPairs; i++)
            {
                PhysicsContactPair contactPair = contact[i];
                for (int j = 0; j < contactPair.NumberOfContacts; j++)
                {
                    CheckAndRemoveMissilesAfterCollision(contactPair[j]);
                }
            }
        }

        public virtual void InitializePhysicsMaterialTypesOnCollision()
        {
            physicsMaterialsRemovedOnCollision = new List<PhysicsMaterial>();
            physicsMaterialsRemovedOnCollision.Add(PhysicsMaterial.GetFromName("missile"));
            physicsMaterialsRemovedOnCollision.Add(PhysicsMaterial.GetFromName("wood_weapon"));
        }

        private void CheckAndRemoveMissilesAfterCollision(PhysicsContactInfo contact)
        {
            if (physicsMaterialsRemovedOnCollision.Contains(contact.PhysicsMaterial1))
            {
                float rayDistance;
                GameEntity missile;
                Scene.RayCastForClosestEntityOrTerrain(contact.Position, contact.Normal * 1f + contact.Position, out rayDistance, out missile, rayThickness: 0.1f, excludeBodyFlags: BodyFlags.Dynamic | BodyFlags.DynamicConvexHull);
                if (missile == null) return;
                if (!missile.BodyFlag.HasFlag(BodyFlags.DroppedItem)) return;
                missile.SetGlobalFrame(MatrixFrame.Zero);
            }
        }
    }
}
