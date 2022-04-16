using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace ScenePhysicsImplementer
{
    public class PhysicsObject : ScriptComponentBehavior    //NEEDS TO BE RENAMED TO SCE_PhysicsObject - old SCE_PhysicsObject class to be obliterated 
    {
        public bool SetPhysicsBodyAsSphere = false;
        public bool DisableGravity = false;
        public float LinearDamping = 1.0f;
        public float AngularDamping = 1.0f;

        public GameEntity physObject { get; private set; }
        public ObjectPropertiesLib physObjProperties { get; private set; }
        public Vec3 MoI { get; private set; }
        public float mass { get; private set; }
        public Vec3 physObjCoM { get; private set; }  //always local coordinates

        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.None;
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
        }

        public virtual void InitializeScene()
        {
            physObject = base.GameEntity;
        }

        public void UpdateCenterOfMass()
        {
            physObjCoM = physObject.CenterOfMass;  //does NOT return vector lengths scaled to child matrix frames - vector lengths always global
        }

        public virtual void InitializePhysics()
        {
            mass = physObject.Mass;
            UpdateCenterOfMass();

            physObject.EnableDynamicBody();
            physObject.SetBodyFlags(BodyFlags.Dynamic | BodyFlags.Moveable | BodyFlags.BodyOwnerEntity);
            physObject.SetDamping(LinearDamping, AngularDamping);

            physObjProperties = new ObjectPropertiesLib(physObject);
            MoI = physObjProperties.principalMomentsOfInertia;
            physObject.SetMassSpaceInertia(MoI);    //simplify mass moments of inertia to a cubic volume based on entity bounding box

            if (SetPhysicsBodyAsSphere) ObjectPropertiesLib.SetPhysicsAsSphereBody(physObject);
            if (DisableGravity) physObject.DisableGravity();
        }
    }
}
