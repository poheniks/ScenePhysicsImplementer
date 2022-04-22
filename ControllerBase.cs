using System;
using System.Collections.Generic;
using TaleWorlds.MountAndBlade;
using TaleWorlds.Engine;
using TaleWorlds.Library;
using TaleWorlds.Core;
using TaleWorlds.InputSystem;
using TaleWorlds.Localization;

namespace ScenePhysicsImplementer
{
    public abstract class ControllerBase : StandingPoint
    {
        public bool ShowEditorHelpers = true;
        public string DescriptionText = "";
        public Vec2 movementInputVector { get; private set; }

        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.TickParallel;
        }

        public override string GetDescriptionText(GameEntity gameEntity = null)
        {
            return DescriptionText;
        }

        protected override void OnEditorInit()
        {
            base.OnEditorInit();
            Initialize();
        }

        protected override void OnInit()
        {
            base.OnInit();
            Initialize();

        }

        public virtual void Initialize()
        {
            base.SetScriptComponentToTick(this.GetTickRequirement());
        }

        protected override void OnTickParallel(float dt)
        {
            base.OnTickParallel(dt);
            if (UserAgent != null) movementInputVector = UserAgent.MovementInputVector;
            
        }
    }
}
