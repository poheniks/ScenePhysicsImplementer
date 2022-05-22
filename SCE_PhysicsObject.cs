using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.MountAndBlade;
using System.Collections.Generic;
using System.Reflection;
using System;

namespace ScenePhysicsImplementer
{
    public class SCE_PhysicsObject : ScriptComponentBehavior  
    {
        //editor fields
        public bool ShowEditorHelpers = true;
        public bool SetPhysicsBodyAsSphere = false;
        public bool RemoveMissilesOnCollision = true;
        public bool DisableGravity = false;
        public bool ApplyRealisticGravity = false;
        public float LinearDamping = 1.0f;
        public float AngularDamping = 1.0f;
        public SimpleButton SetNoCollideFlagForStaticChildObjects;
        public SimpleButton ShowHelpText;   //for formatting purposes in the scene editor, place buttons as the last fields

        public GameEntity physObject { get; private set; }
        public ObjectPropertiesLib physObjProperties { get; private set; }
        public Vec3 MoI { get; private set; }
        public float mass { get; private set; }
        public Vec3 physObjCoM { get; private set; }  //always in object's local coordinate system, but never scaled with object

        [EditorVisibleScriptComponentVariable(false)]
        public List<PhysicsMaterial> physicsMaterialsRemovedOnCollision;

        private bool editorInitialized = false;

        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.Tick;
        }
        protected override void OnEditorInit()
        {
            base.OnEditorInit();
            base.SetScriptComponentToTick(GetTickRequirement());
            InitializeScene();
        }

        protected override void OnInit()
        {
            base.OnInit();
            InitializeScene();
            InitializePhysics();
            InitializePhysicsMaterialTypesOnCollision();
        }

        protected override void OnEditorTick(float dt)
        {
            base.OnEditorTick(dt);
            if (!editorInitialized) editorInitialized = true;
            if (ShowEditorHelpers && physObject.IsSelectedOnEditor()) RenderEditorHelpers();
        }

        protected override void OnTick(float dt)
        {
            base.OnTick(dt);
            if (ApplyRealisticGravity) ApplyGravitionalForce();
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
            physObject.SetDamping(LinearDamping, AngularDamping);   //damping set through physics engine api - should be tuned last since objects will begin to appear unrealistically heavy

            physObjProperties = new ObjectPropertiesLib(physObject);
            MoI = physObjProperties.principalMomentsOfInertia;
            physObject.SetMassSpaceInertia(MoI);    //simplify mass moments of inertia to a cubic volume based on entity bounding box

            if (DisableGravity | ApplyRealisticGravity) physObject.DisableGravity();
        }

        private void ApplyGravitionalForce()
        {
            Vec3 gravity = new Vec3(0,0,-9.81f/1f) * mass;
            physObject.ApplyLocalForceToDynamicBody(physObjCoM, gravity);
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            if (!editorInitialized) return;
            base.OnEditorVariableChanged(variableName);
            if (variableName == nameof(SetNoCollideFlagForStaticChildObjects)) SetDynamicConvexFlags();
            if (variableName == nameof(ShowHelpText)) DisplayHelpText();
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
                    if (RemoveMissilesOnCollision) CheckAndRemoveMissilesAfterCollision(contactPair[j]);
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
                if (!missile.BodyFlag.HasFlag(BodyFlags.DroppedItem)) return;   //additional check to verify raycasted entity is a missile object
                missile.SetGlobalFrame(MatrixFrame.Zero);   //deleting or removing physics from missiles crashes game - suspect it is related to how the Mission class handles missiles
            }
        }

        public virtual void RenderEditorHelpers()
        {
            UpdateCenterOfMass();
            MatrixFrame CoMAdjustedGlobalFrame = physObject.GetGlobalFrame();
            CoMAdjustedGlobalFrame = ConstraintLib.LocalOffsetAndNormalizeGlobalFrame(CoMAdjustedGlobalFrame, physObjCoM);
            MBDebug.RenderDebugSphere(CoMAdjustedGlobalFrame.origin, 0.05f, Colors.Blue.ToUnsignedInteger());
            MBDebug.RenderDebugText3D(CoMAdjustedGlobalFrame.origin, $"Mass: {mass}", screenPosOffsetX: 15, screenPosOffsetY: -10);

            if (SetPhysicsBodyAsSphere)
            {
                Vec3 max = MathLib.VectorMultiplyComponents(physObject.GetBoundingBoxMax(), physObject.GetGlobalScale());
                Vec3 min = MathLib.VectorMultiplyComponents(physObject.GetBoundingBoxMin(), physObject.GetGlobalScale());
                float sphereRadius = MathLib.AverageVectors(new List<Vec3>() { max, min }).Length;
                MBDebug.RenderDebugSphere(CoMAdjustedGlobalFrame.origin, sphereRadius, Colors.Blue.ToUnsignedInteger(), true);
            }
        }

        public virtual void DisplayHelpText()
        {
            MathLib.HelpText(nameof(SetNoCollideFlagForStaticChildObjects), "Adds the body flag Convex to all child entities without a script component. " +
    "Physics objects with the Convex body flag will still collide with Convex objects");
            MathLib.HelpText(nameof(AngularDamping), "Sets engine-level damping for rotational motion. Can stablize constraints, but will lead to unrealistic motion");
            MathLib.HelpText(nameof(LinearDamping), "Sets engine-level damping for translational motion. Can stablize constraints, but will lead to unrealistic motion");
            MathLib.HelpText(nameof(ApplyRealisticGravity), "Disables regular gravitational forces and applys a more realistic (stronger) gravity");
            MathLib.HelpText(nameof(DisableGravity), "Disables gravitational forces from the physics object");
            MathLib.HelpText(nameof(RemoveMissilesOnCollision), "Removes arrows, javelins, etc that stick to the physics object and cause unrealistic collision physics");
            MathLib.HelpText(nameof(SetPhysicsBodyAsSphere), "Changes physics collision body to a spherical body, with a diameter encompassing the object bounding box. Use for wheels");
            MathLib.HelpText(nameof(ShowEditorHelpers), "Renders lines & arrows to the constraining object, objects' center of mass, hinge axis, etc. Only appears in editor");
        }
    }
}
