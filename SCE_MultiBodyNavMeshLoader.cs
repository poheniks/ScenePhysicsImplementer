using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Source.Missions;
using TaleWorlds.Engine;
using TaleWorlds.InputSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;
using System.Collections.Generic;
using System;

namespace ScenePhysicsImplementer
{
    public class SCE_MultiBodyNavMeshLoader : ScriptComponentBehavior
    {
        public string NavMeshPrefabName = "";
        public SimpleButton LoadNavMeshPrefab;
        public bool CreateChildEmptyEntityReference = false;

        private int dynamicNavMeshIDStart = 0;
        private Dictionary<GameEntity, GameEntity> parentChildEmptyEntityDict = new Dictionary<GameEntity, GameEntity>();

        private bool editorInitialized = false;
        public override TickRequirement GetTickRequirement()
        {
            return TickRequirement.Tick;
        }

        protected override void OnEditorVariableChanged(string variableName)
        {
            if (!editorInitialized) return;
            base.OnEditorVariableChanged(variableName);
            if (variableName == nameof(LoadNavMeshPrefab))
            {
                if (NavMeshPrefabName.Length > 0) Scene.ImportNavigationMeshPrefab(NavMeshPrefabName, 0);
            }
        }

        protected override void OnEditorInit()
        {
            base.OnEditorInit();
            SetScriptComponentToTick(GetTickRequirement());
        }

        protected override void OnEditorTick(float dt)
        {
            base.OnEditorTick(dt);
            if (!editorInitialized) editorInitialized = true;
            SetScriptComponentToTick(TickRequirement.None);
        }

        protected override void OnInit()
        {
            base.OnInit();
            SetScriptComponentToTick(TickRequirement.None);

            if (NavMeshPrefabName.Length > 0)
            {
                dynamicNavMeshIDStart = Mission.Current.GetNextDynamicNavMeshIdStart();
                Scene.ImportNavigationMeshPrefab(NavMeshPrefabName, dynamicNavMeshIDStart);
                AttachDynamicNavMesh();
            }
        }
        
        private void AttachDynamicNavMesh()
        {
            foreach (string tag in GameEntity.Tags)
            {
                //string format: (mesh face id)_(internal/connection/blocker)_(entity tag)
                string[] splitTag = tag.Split('_');
                int tagFirstHeader = tag.IndexOf('_');
                int tagLastHeader = tag.LastIndexOf('_');
                if (tagFirstHeader == 0 && tagLastHeader == 0 || tagFirstHeader == tagLastHeader)
                {
                    MathLib.DebugMessage("Error in SCE_MultiBodyNavMeshLoader. Missing _ header(s)|" + tag, isError: true);
                    continue;
                }

                //get face ID 
                int faceID;
                string subTagID = splitTag[0];
                if (!int.TryParse(subTagID, out faceID))
                {
                    MathLib.DebugMessage("Error in SCE_MultiBodyNavMeshLoader. Tag with non-numeric faceID|" + tag, isError: true);
                    continue;
                }
                else if (faceID < 0)
                {
                    MathLib.DebugMessage("Error in SCE_MultiBodyNavMeshLoader. Tag with negative faceID|" + tag, isError: true);
                    continue;
                }
                faceID += dynamicNavMeshIDStart;

                //get face type
                int faceType;
                string subTagType = splitTag[1];
                if (!int.TryParse(subTagType, out faceType) || (faceType < 0 || faceType > 2))
                {
                    MathLib.DebugMessage("Error in SCE_MultiBodyNavMeshLoader. Tag with non-valid face type. Use integers 0 - 2|" + tag, isError: true);
                    continue;
                }

                //get entity
                string subTagEntity = splitTag[2];
                GameEntity attachingEntity = ObjectPropertiesLib.FindClosestTaggedEntity(this.GameEntity, subTagEntity);
                if (attachingEntity == null)
                {
                    MathLib.DebugMessage("Error in SCE_MultiBodyNavMeshLoader. No entity found|" + tag, isError: true);
                    continue;
                }

                //attach mesh
                if (CreateChildEmptyEntityReference)
                {
                    GameEntity parent = attachingEntity;
                    if (!parentChildEmptyEntityDict.TryGetValue(parent, out attachingEntity))
                    {
                        GameEntity emptyEntity = GameEntity.CreateEmptyDynamic(Scene); //create child empty entity to attach mesh prefab to
                        parent.AddChild(emptyEntity, false);
                        parentChildEmptyEntityDict.Add(parent, emptyEntity);
                        attachingEntity = emptyEntity;
                    }
                }

                attachingEntity.SetGlobalFrame(GameEntity.GetGlobalFrame());    //set empty entity frame to the scriptcomponent entity that the mesh prefab is localized about

                switch (faceType)
                {
                    case 0:
                        attachingEntity.AttachNavigationMeshFaces(faceID, false, false, false); //internal mesh face
                        break;
                    case 1:
                        attachingEntity.AttachNavigationMeshFaces(faceID, true, false, false);  //connecting mesh face
                        break;
                    case 2:
                        attachingEntity.AttachNavigationMeshFaces(faceID, false, true, false);  //blocking mesh face
                        break;
                }
                Scene.SetAbilityOfFacesWithId(faceID, true);

            }
        }
    }
}
