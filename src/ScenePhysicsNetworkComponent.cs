using System;
using System.Collections.Generic;
using NetworkMessages.FromClient;
using NetworkMessages.FromServer;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;
using TaleWorlds.MountAndBlade.Network.Messages;
using TaleWorlds.ObjectSystem;
using TaleWorlds.Library;
using TaleWorlds.Engine;

namespace ScenePhysicsImplementer
{
    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class SyncPhysicsObject : GameNetworkMessage
    {
        
        public MatrixFrame GlobalFrame { get; private set; }
        public MissionObject PhysicsObject { get; private set; }


        public SyncPhysicsObject() { }
        public SyncPhysicsObject(MissionObject physicsObject)
        {
            PhysicsObject = physicsObject;
            GlobalFrame = PhysicsObject.GameEntity.GetGlobalFrame();
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.MissionObjects;
        }

        protected override string OnGetLogFormat()
        {
            return $"Syncing physics object: {PhysicsObject.GameEntity.GetPrefabName()}";
        }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            PhysicsObject = GameNetworkMessage.ReadMissionObjectReferenceFromPacket(ref bufferReadValid);
            GlobalFrame = GameNetworkMessage.ReadMatrixFrameFromPacket(ref bufferReadValid);
            return bufferReadValid;
        }

        protected override void OnWrite()
        {
            GameNetworkMessage.WriteMissionObjectReferenceToPacket(PhysicsObject);
            GameNetworkMessage.WriteMatrixFrameToPacket(GlobalFrame);
        }
    }

    [DefineGameNetworkMessageTypeForMod(GameNetworkMessageSendType.FromServer)]
    public sealed class SyncPhysicsObjects : GameNetworkMessage
    {

        public List<Tuple<MissionObject, MatrixFrame>> PhysicsObjects = new List<Tuple<MissionObject, MatrixFrame>>();
        private int objectCount;
        private static CompressionInfo.Integer compression = new CompressionInfo.Integer(0,500);

        public SyncPhysicsObjects() { }
        public SyncPhysicsObjects(List<Tuple<MissionObject, MatrixFrame>> physicsObjects)
        {
            PhysicsObjects = physicsObjects;
            objectCount = PhysicsObjects.Count;
        }

        protected override MultiplayerMessageFilter OnGetLogFilter()
        {
            return MultiplayerMessageFilter.MissionObjects;
        }

        protected override string OnGetLogFormat()
        {
            return $"Syncing physics objects: {PhysicsObjects.Count}";
        }

        protected override bool OnRead()
        {
            bool bufferReadValid = true;
            
            objectCount = GameNetworkMessage.ReadIntFromPacket(compression, ref bufferReadValid);
            PhysicsObjects = new List<Tuple<MissionObject, MatrixFrame>>();
            for (int i = 0; i < objectCount; i++)
            {
                MissionObject physicsObject = GameNetworkMessage.ReadMissionObjectReferenceFromPacket(ref bufferReadValid);
                MatrixFrame globalFrame = GameNetworkMessage.ReadMatrixFrameFromPacket(ref bufferReadValid);

                PhysicsObjects.Add(Tuple.Create(physicsObject, globalFrame));
            }

            return true;
        }

        protected override void OnWrite()
        {
            GameNetworkMessage.WriteIntToPacket(objectCount, compression);
            for (int i = 0; i < objectCount; i++)
            {
                GameNetworkMessage.WriteMissionObjectReferenceToPacket(PhysicsObjects[i].Item1);
                GameNetworkMessage.WriteMatrixFrameToPacket(PhysicsObjects[i].Item2);
            }
        }
    }


    public class ScenePhysicsNetworkComponent : MissionNetwork
    {
        protected override void AddRemoveMessageHandlers(GameNetwork.NetworkMessageHandlerRegistererContainer registerer)
        {
            if (GameNetwork.IsClient)
            {
                registerer.Register<SyncPhysicsObjects>(new GameNetworkMessage.ServerMessageHandlerDelegate<SyncPhysicsObjects>(HandleServerEventSyncPhysicsObject));
            }

            void HandleServerEventSyncPhysicsObject(SyncPhysicsObjects message)
            {
                foreach(Tuple<MissionObject,MatrixFrame> objectInfo in message.PhysicsObjects)
                {
                    MatrixFrame frame = objectInfo.Item2;
                    objectInfo.Item1.GameEntity.SetGlobalFrame(frame);
                }
            }
        }

        public override void OnMissionTick(float dt)
        {
            
            if (GameNetwork.IsClient) return;

            IEnumerable<SCE_PhysicsObject> physicsObjects = Mission.MissionObjects.FindAllWithType<SCE_PhysicsObject>();
            List<Tuple<MissionObject, MatrixFrame>> physicsObjectsToSync = new List<Tuple<MissionObject, MatrixFrame>>();

            foreach (SCE_PhysicsObject physicObject in physicsObjects)
            {
                physicsObjectsToSync.Add(Tuple.Create((MissionObject)physicObject, physicObject.GameEntity.GetGlobalFrame()));
            }
            GameNetwork.BeginBroadcastModuleEvent();
            GameNetwork.WriteMessage(new SyncPhysicsObjects(physicsObjectsToSync));
            GameNetwork.EndBroadcastModuleEvent(GameNetwork.EventBroadcastFlags.None);
            
        }
    }
}
