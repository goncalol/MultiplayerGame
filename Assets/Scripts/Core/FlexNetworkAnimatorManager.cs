using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace FirstGearGames.Mirrors.Assets.FlexNetworkAnimators
{
    public struct AnimatorUpdateMessage : NetworkMessage
    {
        public List<AnimatorUpdate> Data;
    }

    public class FlexNetworkAnimatorManager : MonoBehaviour
    {
        #region Private.
        /// <summary>
        /// Active FlexNetworkTransform components.
        /// </summary>
        private static List<FlexNetworkAnimator> _activeFlexNetworkAnimators = new List<FlexNetworkAnimator>();
        /// <summary>
        /// Reliable SyncDatas to send to all.
        /// </summary>
        private static List<AnimatorUpdate> _toAllReliableAnimatorUpdate = new List<AnimatorUpdate>();
        /// <summary>
        /// Reliable SyncDatas to send send to server.
        /// </summary>
        private static List<AnimatorUpdate> _toServerReliableAnimatorUpdate = new List<AnimatorUpdate>();
        /// <summary>
        /// Reliable SyncDatas sent to specific observers.
        /// </summary>
        private static Dictionary<NetworkConnection, List<AnimatorUpdate>> _observerReliableAnimatorUpdate = new Dictionary<NetworkConnection, List<AnimatorUpdate>>();
        /// <summary>
        /// Last NetworkClient.active state.
        /// </summary>
        private bool _lastClientActive = false;
        /// <summary>
        /// Last NetworkServer.active state.
        /// </summary>
        private bool _lastServerActive = false;
        /// <summary>
        /// How much data can be bundled per reliable message.
        /// </summary>
        private int _reliableDataBundleCount = -1;
        #endregion

        #region Const.
        /// <summary>
        /// Maximum possible size for an animator update. This value is guessed and will need to be optimized later based on actual data size.
        /// </summary>
        private const int MAXIMUM_DATA_SIZE = 40;
        /// <summary>
        /// Maximum packet size by default. This is used when packet size is unknown.
        /// </summary>
        private const int MAXIMUM_PACKET_SIZE = 1200;
        #endregion

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void FirstInitialize()
        {
            GameObject go = new GameObject();
            go.name = "FlexNetworkAnimatorManager";
            go.AddComponent<FlexNetworkAnimatorManager>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            StartCoroutine(__SetDataBundleCount());
        }

        private void Update()
        {
            CheckRegisterHandlers();

            //Run updates on FlexNetworkTransforms.
            for (int i = 0; i < _activeFlexNetworkAnimators.Count; i++)
                _activeFlexNetworkAnimators[i].ManualUpdate();

            //Send any queued messages.
            SendMessages();
        }

        /// <summary>
        /// Calculates data bundle count.
        /// </summary>
        private IEnumerator __SetDataBundleCount()
        {
            //Immediately set using default packet size.
            CalculateDataBundleCount(MAXIMUM_PACKET_SIZE);

            //Give up after 10 seconds of trying.
            float timeout = Time.unscaledTime + 10f;
            while (Transport.activeTransport == null)
            {
                //If timed out then exit coroutine.
                if (Time.unscaledTime > timeout)
                {
                    Debug.LogWarning("Could not locate transport being used, unable to calculate DataBundleCount. If client only you may ignore this message.");
                    yield break;
                }

                yield return null;
            }

            int reliableSize = Mathf.Min(MAXIMUM_PACKET_SIZE, Transport.activeTransport.GetMaxPacketSize(0));
            CalculateDataBundleCount(reliableSize);
        }

        /// <summary>
        /// Sets roughly how many datas can send per bundle.
        /// </summary>
        private void CalculateDataBundleCount(int reliableMaxPacketSize)
        {
            //High value since it's unknown.
            int headerSize = 20;

            _reliableDataBundleCount = (reliableMaxPacketSize - headerSize) / MAXIMUM_DATA_SIZE;
        }

        /// <summary>
        /// Registers handlers for the client.
        /// </summary>
        private void CheckRegisterHandlers()
        {
            bool changed = (_lastClientActive != NetworkClient.active || _lastServerActive != NetworkServer.active);
            //If wasn't active previously but is now then get handlers again.
            if (changed && NetworkClient.active)
                NetworkClient.ReplaceHandler<AnimatorUpdateMessage>(OnServerAnimatorUpdate);
            if (changed && NetworkServer.active)
                NetworkServer.ReplaceHandler<AnimatorUpdateMessage>(OnClientAnimatorUpdate);

            _lastServerActive = NetworkServer.active;
            _lastClientActive = NetworkClient.active;
        }

        /// <summary>
        /// Adds to ActiveFlexNetworkTransforms.
        /// </summary>
        /// <param name="fntBase"></param>
        public static void AddToActive(FlexNetworkAnimator fna)
        {
            _activeFlexNetworkAnimators.Add(fna);
        }
        /// <summary>
        /// Removes from ActiveFlexNetworkTransforms.
        /// </summary>
        /// <param name="fntBase"></param>
        public static void RemoveFromActive(FlexNetworkAnimator fntBase)
        {
            _activeFlexNetworkAnimators.Remove(fntBase);
        }

        /// <summary>
        /// Sends data to server.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="reliable"></param>
        [Client]
        public static void SendToServer(AnimatorUpdate data)
        {
            _toServerReliableAnimatorUpdate.Add(data);
        }

        /// <summary>
        /// Sends data to all.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="reliable"></param>
        [Server]
        public static void SendToAll(AnimatorUpdate data)
        {
            _toAllReliableAnimatorUpdate.Add(data);
        }

        /// <summary>
        /// Sends data to observers.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="data"></param>
        /// <param name="reliable"></param>
        [Server]
        public static void SendToObserver(NetworkConnection conn, AnimatorUpdate data)
        {
            List<AnimatorUpdate> datas;
            //If doesn't have datas for connection yet then make new datas.
            if (!_observerReliableAnimatorUpdate.TryGetValue(conn, out datas))
            {
                datas = new List<AnimatorUpdate>();
                _observerReliableAnimatorUpdate[conn] = datas;
            }

            datas.Add(data);
        }

        /// <summary>
        /// Sends queued messages.
        /// </summary>
        private void SendMessages()
        {
            //Server.
            if (NetworkServer.active)
            {
                //Reliable to all.
                SendAnimatorUpdates(false, null, _toAllReliableAnimatorUpdate);

                //Reliable to observers.
                foreach (KeyValuePair<NetworkConnection, List<AnimatorUpdate>> item in _observerReliableAnimatorUpdate)
                {
                    //Null or unready network connection.
                    if (item.Key == null || !item.Key.isReady)
                        continue;

                    SendAnimatorUpdates(false, item.Key, item.Value);
                }
            }
            //Client.
            if (NetworkClient.active)
            {
                //Reliable to server.
                SendAnimatorUpdates(true, null, _toServerReliableAnimatorUpdate);
            }

            _toServerReliableAnimatorUpdate.Clear();
            _toAllReliableAnimatorUpdate.Clear();
            _observerReliableAnimatorUpdate.Clear();            
        }

        /// <summary>
        /// Sends data to all or specified connection.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="datas"></param>
        /// <param name="reliable"></param>
        private void SendAnimatorUpdates(bool toServer, NetworkConnection conn, List<AnimatorUpdate> datas)
        {
            int index = 0;
            int bundleCount = _reliableDataBundleCount;
            int channel = 0;
            while (index < datas.Count)
            {
                int count = Mathf.Min(bundleCount, datas.Count - index);
                AnimatorUpdateMessage msg = new AnimatorUpdateMessage()
                {
                    Data = datas.GetRange(index, count)
                };

                if (toServer)
                {
                    NetworkClient.Send(msg, channel);
                }
                else
                {
                    //If no connection then send to all.
                    if (conn == null)
                        NetworkServer.SendToAll(msg, channel);
                    //Otherwise send to connection.
                    else
                        conn.Send(msg, channel);
                }
                index += count;
            }
        }

        /// <summary>
        /// Received on clients when server sends data.
        /// </summary>
        /// <param name="msg"></param>
        private void OnServerAnimatorUpdate(AnimatorUpdateMessage msg)
        {
            AnimatorUpdateMessageReceived(msg, true);
        }

        /// <summary>
        /// Received on server when client sends data.
        /// </summary>
        /// <param name="msg"></param>
        private void OnClientAnimatorUpdate(AnimatorUpdateMessage msg)
        {
            AnimatorUpdateMessageReceived(msg, false);
        }

        /// <summary>
        /// Called when an AnimatorUpdateMessage is received.
        /// </summary>
        /// <param name="msg"></param>
        /// <param name="receivedOnClient"></param>
        private void AnimatorUpdateMessageReceived(AnimatorUpdateMessage msg, bool receivedOnClient)
        {
            //Have to check sequence id against the FNT sending.
            int count = msg.Data.Count;
            for (int i = 0; i < count; i++)
            {
                /* Initially I tried caching the getcomponent calls but the performance difference
                * couldn't be registered. At this time it's not worth creating the extra complexity
                * for what might be a 1% fps difference. */
                if (NetworkIdentity.spawned.TryGetValue(msg.Data[i].NetworkIdentity, out NetworkIdentity ni))
                {
                    if (ni != null)
                    {
                        FlexNetworkAnimator fna = ReturnFNAOnNetworkIdentity(ni, msg.Data[i].ComponentIndex);
                        if (fna != null)
                        {
                            if (receivedOnClient)
                                fna.ServerDataReceived(msg.Data[i]);
                            else
                                fna.ClientDataReceived(msg.Data[i]);
                        }
                    }
                }
            }
        }
        /// <summary>
        /// Returns a FlexNetworkTransformBase on a networkIdentity using a componentIndex.
        /// </summary>
        /// <param name="componentIndex"></param>
        /// <returns></returns>
        private FlexNetworkAnimator ReturnFNAOnNetworkIdentity(NetworkIdentity ni, byte componentIndex)
        {
            /* Networkbehaviours within the collection are the same order as compenent indexes.
            * I can save several iterations by simply grabbing the index from the networkbehaviours collection rather than iterating
            * it. */
            //A network behaviour was removed or added at runtime, component counts don't match up.
            if (componentIndex >= ni.NetworkBehaviours.Length)
                return null;

            FlexNetworkAnimator[] fnas = ni.NetworkBehaviours[componentIndex].GetComponents<FlexNetworkAnimator>();
            /* Now find the FNTBase which matches the component index. There is probably only one FNT
             * but if the user were using FNT + FNT Child there could be more so it's important to get all FNT
             * on the object. */
            for (int i = 0; i < fnas.Length; i++)
            {
                //Match found.
                if (fnas[i].CachedComponentIndex == componentIndex)
                    return fnas[i];
            }

            /* If here then the component index was found but the fnt with the component index
             * was not. This should never happen. */
            Debug.LogWarning("ComponentIndex found but FlexNetworkAnimator was not.");
            return null;
        }

    }


}