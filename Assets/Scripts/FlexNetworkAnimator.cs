using Mirror;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FirstGearGames.Mirrors.Assets.FlexNetworkAnimators
{
    public class FlexNetworkAnimator : NetworkBehaviour
    {
        #region Types.
        /// <summary>
        /// Information on how to smooth to a float value.
        /// </summary>
        private struct SmoothedFloat
        {
            public SmoothedFloat(float rate, float target)
            {
                Rate = rate;
                Target = target;
            }

            public readonly float Rate;
            public readonly float Target;
        }
        /// <summary>
        /// A parameter which has changed since it's last value. Contains parameter details index and new value data.
        /// </summary>
        private struct ChangedParameter
        {
            public byte ParameterIndex;
            public byte[] Data;

            public ChangedParameter(byte parameterIndex, byte[] data)
            {
                ParameterIndex = parameterIndex;
                Data = data;
            }
        }

        /// <summary>
        /// Details about an animator parameter.
        /// </summary>
        private class ParameterDetail
        {
            /// <summary>
            /// Parameter information.
            /// </summary>
            public readonly AnimatorControllerParameter ControllerParameter = null;
            /// <summary>
            /// Index within the types collection for this parameters value. The exception is with triggers; if the parameter type is a trigger then a value of 1 is set, 0 is unset.
            /// </summary>
            public readonly byte TypeIndex = 0;
            /// <summary>
            /// Hash for the animator string.
            /// </summary>
            public readonly int Hash;

            public ParameterDetail(AnimatorControllerParameter controllerParameter, byte typeIndex)
            {
                ControllerParameter = controllerParameter;
                TypeIndex = typeIndex;
                Hash = controllerParameter.nameHash;
            }
        }
        #endregion

        #region Serialized.
        /// <summary>
        /// The animator component to synchronize.
        /// </summary>
        [Tooltip("The animator component to synchronize.")]
        [SerializeField]
        private Animator _animator;
        /// <summary>
        /// The animator component to synchronize.
        /// </summary>
        public Animator Animator { get { return _animator; } }
        /// <summary>
        /// True to smooth float value changes for spectators.
        /// </summary>
        [Tooltip("True to smooth float value changes for spectators.")]
        [SerializeField]
        private bool _smoothFloats = true;
        /// <summary>
        /// How much time to fall behind when using smoothing. Only increase value if the smoothing is sometimes jittery. Recommended values are between 0 and 0.04.
        /// </summary>
        [Tooltip("How much time to fall behind when using smoothing. Only increase value if the smoothing is sometimes jittery. Recommended values are between 0 and 0.04.")]
        [Range(0f, 0.1f)]
        [SerializeField]
        private float _interpolationFallbehind = 0.02f;
        /// <summary>
        /// How often to synchronize this animator.
        /// </summary>
        [Tooltip("How often to synchronize this animator.")]
        [Range(0.01f, 0.5f)]
        [SerializeField]
        private float _synchronizeInterval = 0.1f;
        /// <summary>
        /// True if using client authoritative animations.
        /// </summary>
        [Tooltip("True if using client authoritative animations.")]
        [SerializeField]
        private bool _clientAuthoritative = true;
        /// <summary>
        /// True to synchronize server results back to owner. Typically used when you are changing animations on the server and are relying on the server response to update the clients animations.
        /// </summary>
        [Tooltip("True to synchronize server results back to owner. Typically used when you are changing animations on the server and are relying on the server response to update the clients animations.")]
        [SerializeField]
        private bool _synchronizeToOwner = false;
        #endregion

        #region Private.
        /// <summary>
        /// All parameter values, excluding triggers.
        /// </summary>
        private List<ParameterDetail> _parameterDetails = new List<ParameterDetail>();
        /// <summary>
        /// Last int values.
        /// </summary>
        private List<int> _ints = new List<int>();
        /// <summary>
        /// Last float values.
        /// </summary>
        private List<float> _floats = new List<float>();
        /// <summary>
        /// Last bool values.
        /// </summary>
        private List<bool> _bools = new List<bool>();
        /// <summary>
        /// Last layer weights.
        /// </summary>
        private float[] _layerWeights = null;
        /// <summary>
        /// Last speed.
        /// </summary>
        private float _speed = 0f;
        /// <summary>
        /// Next time client may send parameter updates.
        /// </summary>
        private float _nextClientSendTime = -1f;
        /// <summary>
        /// Next time server may send parameter updates.
        /// </summary>
        private float _nextServerSendTime = -1f;
        /// <summary>
        /// Bytes to send to clients, which were received from authoritative clients.
        /// </summary>
        private List<AnimatorUpdate> _updatesFromClients = new List<AnimatorUpdate>();
        /// <summary>
        /// Trigger values set by using SetTrigger and ResetTrigger.
        /// </summary>
        private List<ChangedParameter> _triggerUpdates = new List<ChangedParameter>();
        /// <summary>
        /// Returns if the animator is exist and is active.
        /// </summary>
        private bool _isActive
        {
            get { return (_animator != null && _animator.enabled); }
        }
        /// <summary>
        /// Float valeus to smooth towards.
        /// </summary>
        private Dictionary<int, SmoothedFloat> _smoothedFloats = new Dictionary<int, SmoothedFloat>();
        /// <summary>
        /// Returns if floats can be smoothed for this client.
        /// </summary>
        private bool _canSmoothFloats
        {
            get
            {
                //Don't smooth on server only.
                if (!base.isClient)
                    return false;
                //Smoothing is disabled.
                if (!_smoothFloats)
                    return false;
                //No reason to smooth for self.
                if (base.hasAuthority && _clientAuthoritative)
                    return false;

                //Fall through.
                return true;
            }
        }
        /// <summary>
        /// 
        /// </summary>
        private byte? _cachedComponentIndex = null;
        /// <summary>
        /// Cached ComponentIndex for the NetworkBehaviour this FNA is on. This is because Mirror codes bad.
        /// </summary>
        public byte CachedComponentIndex
        {
            get
            {
                if (_cachedComponentIndex == null)
                {
                    //Exceeds value.
                    if (base.ComponentIndex > 255)
                    {
                        Debug.LogError("ComponentIndex is larger than supported type.");
                        _cachedComponentIndex = 0;
                    }
                    //Doesn't exceed value.
                    else
                    { 
                    _cachedComponentIndex = (byte)Mathf.Abs(base.ComponentIndex);
                    }
                }

                return _cachedComponentIndex.Value;
            }
        }
        /// <summary>
        /// NetworkVisibility component on the root of this object.
        /// </summary>
        private NetworkVisibility _networkVisibility = null;
        /// <summary>
        /// Layers which need to have their state synchronized. Byte is the ParameterIndex.
        /// </summary>
        private HashSet<int> _unsynchronizedLayerStates = new HashSet<int>();
        /// <summary>
        /// Last animator set.
        /// </summary>
        private Animator _lastAnimator = null;
        /// <summary>
        /// Last Controller set.
        /// </summary>
        private RuntimeAnimatorController _lastController = null;
        #endregion

        #region Const.
        /// <summary>
        /// ParameterDetails index which indicates a layer weight change.
        /// </summary>
        private const byte LAYER_WEIGHT = 240;
        /// <summary>
        /// ParameterDetails index which indicates an animator speed change.
        /// </summary>
        private const byte SPEED = 241;
        /// <summary>
        /// ParameterDetails index which indicates a layer state change.
        /// </summary>
        private const byte STATE = 242;
        #endregion

        private void Awake()
        {
            Initialize();
        }

        #region OnSerialize/Deserialize.
        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            if (initialState)
            {
                //Send current parameters.
                if (AnimatorUpdated(out byte[] updatedBytes, true))
                    writer.WriteBytesAndSize(updatedBytes);
                else
                    writer.WriteBytesAndSize(new byte[0]);
            }

            return base.OnSerialize(writer, initialState);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            if (initialState)
            {
                //Parameters.
                byte[] updatedParameters = reader.ReadBytesAndSize();
                ApplyParametersUpdated(updatedParameters);
            }


            base.OnDeserialize(reader, initialState);
        }
        #endregion

        public override void OnStartServer()
        {
            base.OnStartServer();
            _networkVisibility = transform.root.GetComponent<NetworkVisibility>();
        }

        protected virtual void OnEnable()
        {
            FlexNetworkAnimatorManager.AddToActive(this);
        }
        protected virtual void OnDisable()
        {
            FlexNetworkAnimatorManager.RemoveFromActive(this);
        }

        public void ManualUpdate()
        {
            if (base.isClient)
            {
                CheckSendToServer();
                SmoothFloats();
            }
            if (base.isServer)
            {
                CheckSendToClients();
            }
        }

        protected virtual void Reset()
        {
            if (_animator == null)
                SetAnimator(GetComponent<Animator>());
        }

        /// <summary>
        /// Initializes this script for use. Should only be completed once.
        /// </summary>
        private void Initialize()
        {
            if (!_isActive)
            {
                Debug.LogWarning("Animator is null or not enabled; unable to initialize for animator. Use SetAnimator if animator was changed or enable the animator.");
                return;
            }

            //Speed.
            _speed = _animator.speed;

            //Build layer weights.
            _layerWeights = new float[_animator.layerCount];
            for (int i = 0; i < _layerWeights.Length; i++)
                _layerWeights[i] = _animator.GetLayerWeight(i);

            //Create a parameter detail for each parameter that can be synchronized.
            foreach (AnimatorControllerParameter item in _animator.parameters)
            {
                if (!_animator.IsParameterControlledByCurve(item.name))
                {
                    //Over 250 parameters; who would do this!?
                    if (_parameterDetails.Count == 240)
                    {
                        Debug.LogError("Parameter " + item.name + " exceeds the allowed 250 parameter count and is being ignored.");
                        continue;
                    }

                    int typeIndex = 0;
                    //Bools.
                    if (item.type == AnimatorControllerParameterType.Bool)
                    {
                        typeIndex = _bools.Count;
                        _bools.Add(_animator.GetBool(item.nameHash));
                    }
                    //Floats.
                    else if (item.type == AnimatorControllerParameterType.Float)
                    {
                        typeIndex = _floats.Count;
                        _floats.Add(_animator.GetFloat(item.name));
                    }
                    //Ints.
                    else if (item.type == AnimatorControllerParameterType.Int)
                    {
                        typeIndex = _ints.Count;
                        _ints.Add(_animator.GetInteger(item.nameHash));
                    }
                    //Triggers.
                    else if (item.type == AnimatorControllerParameterType.Trigger)
                    {
                        /* Triggers aren't persistent so they don't use stored values
                         * but I do need to make a parameter detail to track the hash. */
                        typeIndex = -1;
                    }

                    _parameterDetails.Add(new ParameterDetail(item, (byte)typeIndex));
                }
            }
        }

        /// <summary>
        /// Sets which animator to use. You must call this with the appropriate animator on all clients and server. This change is not automatically synchronized.
        /// </summary>
        /// <param name="animator"></param>
        public void SetAnimator(Animator animator)
        {
            //No update required.
            if (animator == _lastAnimator)
                return;

            _animator = animator;
            Initialize();
            _lastAnimator = animator;
        }

        /// <summary>
        /// Sets which controller to use. You must call this with the appropriate controller on all clients and server. This change is not automatically synchronized.
        /// </summary>
        /// <param name="controller"></param>        
        public void SetController(RuntimeAnimatorController controller)
        {
            //No update required.
            if (controller == _lastController)
                return;

            _animator.runtimeAnimatorController = controller;
            Initialize();
            _lastController = controller;
        }

        /// <summary>
        /// Checks to send animator data from server to clients.
        /// </summary>
        private void CheckSendToServer()
        {
            if (!_isActive)
                return;
            //Cannot send to server if not client
            if (!base.isClient)
                return;
            //Cannot send to server if not client authoritative.
            if (!_clientAuthoritative)
                return;
            //Cannot send if don't have authority.
            if (!base.hasAuthority)
                return;

            //Not enough time passed to send.
            if (Time.time < _nextClientSendTime)
                return;
            _nextClientSendTime = Time.time + _synchronizeInterval;

            /* If there are updated parameters to send.
             * Don't really need to worry about mtu here
             * because there's no way the sent bytes are
             * ever going to come close to the mtu
             * when sending a single update. */
            if (AnimatorUpdated(out byte[] updatedBytes))
                FlexNetworkAnimatorManager.SendToServer(ReturnAnimatorUpdate(updatedBytes));
        }

        /// <summary>
        /// Checks to send animator data from server to clients.
        /// </summary>
        private void CheckSendToClients()
        {
            if (!_isActive)
                return;
            //Cannot send to clients if not server.
            if (!base.isServer)
                return;
            //Not enough time passed to send.
            if (Time.time < _nextServerSendTime)
                return;

            bool sendFromServer;
            //If client authoritative.
            if (_clientAuthoritative)
            {
                //If no owner then send from server.
                if (base.connectionToClient == null)
                {
                    sendFromServer = true;
                }
                //Somehow has ownership
                else
                {
                    //But no data has been received yet, so cannot send.
                    if (_updatesFromClients.Count == 0)
                        return;
                    else
                        sendFromServer = false;
                }
            }
            //Not client authoritative, always send from server.
            else
            {
                sendFromServer = true;
            }


            //Cannot send yet.
            if (Time.time < _nextServerSendTime)
                return;
            _nextServerSendTime = Time.time + _synchronizeInterval;

            bool sendToAll = (_networkVisibility == null);
            /* If client authoritative then then what was received from clients
             * if data exist. */
            if (!sendFromServer)
            {
                for (int i = 0; i < _updatesFromClients.Count; i++)
                {
                    if (sendToAll)
                    {
                        FlexNetworkAnimatorManager.SendToAll(_updatesFromClients[i]);
                    }
                    else
                    {
                        foreach (KeyValuePair<int, NetworkConnection> item in _networkVisibility.netIdentity.observers)
                            FlexNetworkAnimatorManager.SendToObserver(item.Value, _updatesFromClients[i]);
                    }
                }
                _updatesFromClients.Clear();
            }
            //Otherwise send what's changed.
            else
            {
                if (AnimatorUpdated(out byte[] updatedBytes))
                {
                    if (sendToAll)
                    {
                        FlexNetworkAnimatorManager.SendToAll(ReturnAnimatorUpdate(updatedBytes));
                    }
                    else
                    {
                        foreach (KeyValuePair<int, NetworkConnection> item in _networkVisibility.netIdentity.observers)
                            FlexNetworkAnimatorManager.SendToObserver(item.Value, ReturnAnimatorUpdate(updatedBytes));
                    }
                }
            }
        }

        /// <summary>
        /// Returns a new AnimatorUpdate.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private AnimatorUpdate ReturnAnimatorUpdate(byte[] data)
        {
            return new AnimatorUpdate(CachedComponentIndex, base.netIdentity.netId, data);
        }

        /// <summary>
        /// Smooths floats on clients.
        /// </summary>
        [Client]
        private void SmoothFloats()
        {
            //Don't need to smooth on authoritative client.
            if (!_canSmoothFloats)
                return;

            if (_smoothedFloats.Count > 0)
            {
                float deltaTime = Time.deltaTime;

                List<int> finishedEntries = new List<int>();

                /* Cycle through each target float and move towards it.
                    * Once at a target float mark it to be removed from floatTargets. */
                foreach (KeyValuePair<int, SmoothedFloat> item in _smoothedFloats)
                {
                    float current = _animator.GetFloat(item.Key);
                    float next = Mathf.MoveTowards(current, item.Value.Target, item.Value.Rate * deltaTime);
                    _animator.SetFloat(item.Key, next);

                    if (next == item.Value.Target)
                        finishedEntries.Add(item.Key);
                }

                //Remove finished entries from dictionary.
                for (int i = 0; i < finishedEntries.Count; i++)
                    _smoothedFloats.Remove(finishedEntries[i]);
            }
        }

        /// <summary>
        /// Returns if animator is updated and bytes of updated values.
        /// </summary>
        /// <returns></returns>
        private bool AnimatorUpdated(out byte[] updatedBytes, bool forceAll = false)
        {
            updatedBytes = null;
            List<ChangedParameter> cps = new List<ChangedParameter>();
            //Bytes required to accomodate changed parameters.
            int requiredBytes = 0;

            /* Every time a parameter is updated a byte is added
             * for it's index, this is why requiredBytes increases
             * by 1 when a value updates. ChangedParameter contains
             * the index updated and the new value. The requiresBytes
             * is increased also by however many bytes are required
             * for the type which has changed. Some types use special parameter
             * detail indexes, such as layer weights; these can be found under const. */

            for (byte i = 0; i < _parameterDetails.Count; i++)
            {
                ParameterDetail pd = _parameterDetails[i];
                /* Bool. */
                if (pd.ControllerParameter.type == AnimatorControllerParameterType.Bool)
                {
                    bool next = _animator.GetBool(pd.Hash);
                    //If changed.
                    if (forceAll || _bools[pd.TypeIndex] != next)
                    {
                        cps.Add(new ChangedParameter(i, BitConverter.GetBytes(next)));
                        _bools[pd.TypeIndex] = next;
                        //Parameter index + data.
                        requiredBytes += (1 + 1);
                    }
                }
                /* Float. */
                else if (pd.ControllerParameter.type == AnimatorControllerParameterType.Float)
                {
                    float next = _animator.GetFloat(pd.Hash);
                    //If changed.
                    if (forceAll || _floats[pd.TypeIndex] != next)
                    {
                        byte[] data = Compression.ReturnCompressedFloat(next);
                        cps.Add(new ChangedParameter(i, data));
                        _floats[pd.TypeIndex] = next;
                        //Parameter index + data.
                        requiredBytes += (1 + data.Length);
                    }
                }
                /* Int. */
                else if (pd.ControllerParameter.type == AnimatorControllerParameterType.Int)
                {
                    int next = _animator.GetInteger(pd.Hash);
                    //If changed.
                    if (forceAll || _ints[pd.TypeIndex] != next)
                    {
                        byte[] data = Compression.ReturnCompressedInteger(next);
                        cps.Add(new ChangedParameter(i, data));
                        _ints[pd.TypeIndex] = next;
                        //Parameter index + data.
                        requiredBytes += (1 + data.Length);
                    }
                }
            }

            /* Also add any trigger updates.
             * Each trigger update will require
             * 2 bytes, for the indicator and value.
             * So add on two required bytes
             * for every trigger update count. */
            /* Don't need to force trigger sends since
             * they're one-shots. */
            requiredBytes += (_triggerUpdates.Count * 2);
            cps.AddRange(_triggerUpdates);
            _triggerUpdates.Clear();

            //States.
            if (forceAll)
            {
                //Add all layers to layer states.
                for (int i = 0; i < _animator.layerCount; i++)
                    _unsynchronizedLayerStates.Add(i);
            }
            //Go through each layer which needs to be synchronized.
            foreach (int layerIndex in _unsynchronizedLayerStates)
            {
                if (ReturnLayerState(out int stateHash, out float normalizedTime, layerIndex))
                {
                    int writeIndex = 0;
                    //Cannot compress hash, it will always be too large.
                    byte[] hashBytes = BitConverter.GetBytes(stateHash);
                    byte[] time = Compression.ReturnCompressedFloat(normalizedTime);
                    //Resize for layerindex, hash, and time.
                    byte[] data = new byte[1 + hashBytes.Length + time.Length];
                    //Layer index.
                    data[0] = (byte)layerIndex;
                    writeIndex++;
                    //Hash.
                    Array.Copy(hashBytes, 0, data, writeIndex, hashBytes.Length);
                    writeIndex += hashBytes.Length;
                    //Time.
                    Array.Copy(time, 0, data, writeIndex, time.Length);
                    _animator.Play(stateHash, layerIndex, normalizedTime);
                    //1 for parameter index and then data.
                    requiredBytes += (1 + data.Length);
                    //Add to cps.
                    cps.Add(new ChangedParameter(STATE, data));
                }
            }
            _unsynchronizedLayerStates.Clear();

            //Layer weights are added on as raw bytes
            List<byte> layerBytes = null;
            for (int i = 0; i < _layerWeights.Length; i++)
            {
                float next = _animator.GetLayerWeight(i);
                if (forceAll || _layerWeights[i] != next)
                {
                    if (layerBytes == null)
                        layerBytes = new List<byte>();
                    //Layerweight indicator.
                    layerBytes.Add(LAYER_WEIGHT);
                    //Layer index.
                    layerBytes.Add((byte)i);
                    //Weight value.
                    byte[] compressedWeight = Compression.ReturnCompressedFloat(next);
                    layerBytes.AddRange(compressedWeight);
                    //indicator, layer index, data.
                    requiredBytes += (2 + compressedWeight.Length);

                    _layerWeights[i] = next;
                }
            }

            /* Speed is similar to layer weights but we don't need the index,
             * only the indicator and value. */
            byte[] speedBytes = null;
            float speedNext = _animator.speed;
            if (forceAll || _speed != speedNext)
            {
                byte[] speedCompressed = Compression.ReturnCompressedFloat(speedNext);
                //1 byte for speed indicator, rest for actual speed.
                speedBytes = new byte[1 + speedCompressed.Length];
                speedBytes[0] = SPEED;
                Array.Copy(speedCompressed, 0, speedBytes, 1, speedCompressed.Length);
                //Indicator + value.
                requiredBytes += speedBytes.Length;

                _speed = speedNext;
            }

            //If no bytes are required then there is nothing to update.
            if (requiredBytes == 0)
                return false;

            //Current write index for byte array.
            int updateWriteIndex = 0;
            updatedBytes = new byte[requiredBytes];

            //If speed change exist.
            if (speedBytes != null)
            {
                Array.Copy(speedBytes, 0, updatedBytes, updateWriteIndex, speedBytes.Length);
                updateWriteIndex += speedBytes.Length;
            }
            //If there are layer weights to add.
            if (layerBytes != null)
            {
                byte[] layersArray = layerBytes.ToArray();
                Array.Copy(layersArray, 0, updatedBytes, updateWriteIndex, layersArray.Length);
                updateWriteIndex += layersArray.Length;
            }

            //If there are changed parameters.
            if (cps.Count > 0)
            {
                //Build into byte array.
                foreach (ChangedParameter cp in cps)
                {
                    updatedBytes[updateWriteIndex] = cp.ParameterIndex;
                    Array.Copy(cp.Data, 0, updatedBytes, updateWriteIndex + 1, cp.Data.Length);
                    //Increase index.
                    updateWriteIndex += (1 + cp.Data.Length);
                }
            }

            return true;
        }

        /// <summary>
        /// Applies changed parameters to the animator.
        /// </summary>
        /// <param name="changedParameters"></param>
        private void ApplyParametersUpdated(byte[] updatedParameters)
        {
            if (!_isActive)
                return;

            if (updatedParameters.Length > 0)
            {
                int readIndex = 0;
                while (readIndex < updatedParameters.Length)
                {
                    byte parameterIndex = updatedParameters[readIndex];
                    readIndex++;

                    //Layer weight.
                    if (parameterIndex == LAYER_WEIGHT)
                    {
                        byte layerIndex = updatedParameters[readIndex];
                        readIndex += 1;
                        float value = Compression.ReturnDecompressedFloat(updatedParameters, ref readIndex);
                        _animator.SetLayerWeight((int)layerIndex, value);
                    }
                    //Speed.
                    else if (parameterIndex == SPEED)
                    {
                        float value = Compression.ReturnDecompressedFloat(updatedParameters, ref readIndex);
                        _animator.speed = value;
                    }
                    //State.
                    else if (parameterIndex == STATE)
                    {
                        byte layerIndex = updatedParameters[readIndex];
                        readIndex++;
                        int hash = BitConverter.ToInt32(updatedParameters, readIndex);
                        readIndex += 4;
                        float time = Compression.ReturnDecompressedFloat(updatedParameters, ref readIndex);
                        _animator.Play(hash, layerIndex, time);
                    }
                    //Bool.
                    else if (_parameterDetails[parameterIndex].ControllerParameter.type == AnimatorControllerParameterType.Bool)
                    {
                        bool value = BitConverter.ToBoolean(updatedParameters, readIndex);
                        readIndex += 1;
                        _animator.SetBool(_parameterDetails[parameterIndex].Hash, value);
                    }
                    //Float.
                    else if (_parameterDetails[parameterIndex].ControllerParameter.type == AnimatorControllerParameterType.Float)
                    {
                        float value = Compression.ReturnDecompressedFloat(updatedParameters, ref readIndex);
                        //If able to smooth floats.
                        if (_canSmoothFloats)
                        {
                            float currentValue = _animator.GetFloat(_parameterDetails[parameterIndex].Hash);
                            float past = base.syncInterval + _interpolationFallbehind;
                            float rate = Mathf.Abs(currentValue - value) / past;
                            _smoothedFloats[_parameterDetails[parameterIndex].Hash] = new SmoothedFloat(rate, value);
                        }
                        else
                        {
                            _animator.SetFloat(_parameterDetails[parameterIndex].Hash, value);
                        }
                    }
                    //Integer.
                    else if (_parameterDetails[parameterIndex].ControllerParameter.type == AnimatorControllerParameterType.Int)
                    {
                        int value = Compression.ReturnDecompressedInteger(updatedParameters, ref readIndex);
                        _animator.SetInteger(_parameterDetails[parameterIndex].Hash, value);
                    }
                    //Trigger.
                    else if (_parameterDetails[parameterIndex].ControllerParameter.type == AnimatorControllerParameterType.Trigger)
                    {
                        bool value = BitConverter.ToBoolean(updatedParameters, readIndex);
                        readIndex += 1;
                        if (value)
                            _animator.SetTrigger(_parameterDetails[parameterIndex].Hash);
                        else
                            _animator.ResetTrigger(_parameterDetails[parameterIndex].Hash);
                    }
                }
            }
        }

        /// <summary>
        /// Outputs the current state and time for a layer. Returns true if stateHash is not 0.
        /// </summary>
        /// <param name="stateHash"></param>
        /// <param name="normalizedTime"></param>
        /// <param name="results"></param>
        /// <param name="layerIndex"></param>
        /// <returns></returns>
        private bool ReturnLayerState(out int stateHash, out float normalizedTime, int layerIndex)
        {
            stateHash = 0;
            normalizedTime = 0f;
            if (!_isActive)
                return false;

            AnimatorStateInfo st = _animator.GetCurrentAnimatorStateInfo(layerIndex);
            stateHash = st.fullPathHash;
            normalizedTime = st.normalizedTime;

            return (stateHash != 0);
        }

        #region Play.
        /// <summary>
        /// Plays a state.
        /// </summary>
        public void Play(int hash)
        {
            for (int i = 0; i < _animator.layerCount; i++)
                Play(hash, i, 0f);
        }
        /// <summary>
        /// Plays a state.
        /// </summary>
        public void Play(string name)
        {
            Play(Animator.StringToHash(name));
        }
        /// <summary>
        /// Plays a state.
        /// </summary>
        public void Play(int hash, int layer)
        {
            Play(hash, layer, 0f);
        }
        /// <summary>
        /// Plays a state.
        /// </summary>
        public void Play(string name, int layer)
        {
            Play(Animator.StringToHash(name), layer);
        }
        /// <summary>
        /// Plays a state.
        /// </summary>
        public void Play(int hash, int layer, float normalizedTime)
        {
            if (_animator.HasState(layer, hash))
            {
                _animator.Play(hash, layer, normalizedTime);
                _unsynchronizedLayerStates.Add(layer);
            }
        }
        /// <summary>
        /// Plays a state.
        /// </summary>
        public void Play(string name, int layer, float normalizedTime)
        {
            Play(Animator.StringToHash(name), layer, normalizedTime);
        }
        /// <summary>
        /// Plays a state.
        /// </summary>
        public void PlayInFixedTime(int hash)
        {
            for (int i = 0; i < _animator.layerCount; i++)
                PlayInFixedTime(hash, i);
        }
        /// <summary>
        /// Plays a state.
        /// </summary>
        public void PlayInFixedTime(string name)
        {
            PlayInFixedTime(Animator.StringToHash(name));
        }
        /// <summary>
        /// Plays a state.
        /// </summary>
        public void PlayInFixedTime(int hash, int layer)
        {
            PlayInFixedTime(hash, layer, 0f);
        }
        /// <summary>
        /// Plays a state.
        /// </summary>
        public void PlayInFixedTime(string name, int layer)
        {
            PlayInFixedTime(Animator.StringToHash(name), layer);
        }
        /// <summary>
        /// Plays a state.
        /// </summary>
        public void PlayInFixedTime(int hash, int layer, float fixedTime)
        {
            if (_animator.HasState(layer, hash))
            {
                _animator.PlayInFixedTime(hash, layer, fixedTime);
                _unsynchronizedLayerStates.Add(layer);
            }
        }
        #endregion


        /// <summary>
        /// Sets a trigger on the animator and sends it over the network.
        /// </summary>
        /// <param name="hash"></param>
        public void SetTrigger(int hash)
        {
            if (!_isActive)
                return;

            UpdateTrigger(hash, true);
        }
        /// <summary>
        /// Sets a trigger on the animator and sends it over the network.
        /// </summary>
        /// <param name="hash"></param>
        public void SetTrigger(string name)
        {
            if (!_isActive)
                return;

            SetTrigger(Animator.StringToHash(name));
        }

        /// <summary>
        /// Resets a trigger on the animator and sends it over the network.
        /// </summary>
        /// <param name="hash"></param>
        public void ResetTrigger(int hash)
        {
            if (!_isActive)
                return;

            UpdateTrigger(hash, false);
        }
        /// <summary>
        /// Resets a trigger on the animator and sends it over the network.
        /// </summary>
        /// <param name="hash"></param>
        public void ResetTrigger(string name)
        {
            ResetTrigger(Animator.StringToHash(name));
        }

        /// <summary>
        /// Updates a trigger, sets or resets.
        /// </summary>
        /// <param name="set"></param>
        private void UpdateTrigger(int hash, bool set)
        {
            /* Allow triggers to run on owning client if using client authority,
             * as well when not using client authority but also not using synchronize to owner.
             * This allows clients to run animations locally while maintaining server authority. */
            //Using client authority but not owner.
            if (_clientAuthoritative && !base.hasAuthority)
                return;

            //Also block if not using client authority, synchronizing to owner, and not server.
            if (!_clientAuthoritative && _synchronizeToOwner && !base.isServer)
                return;

            //Update locally.
            if (set)
                _animator.SetTrigger(hash);
            else
                _animator.ResetTrigger(hash);

            /* Can send if not client auth but is server,
            * or if client auth and owner. */
            bool canSend = (!_clientAuthoritative && base.isServer) ||
                (_clientAuthoritative && base.hasAuthority);
            //Only queue a send if proper side.
            if (canSend)
            {
                for (byte i = 0; i < _parameterDetails.Count; i++)
                {
                    if (_parameterDetails[i].Hash == hash)
                    {
                        _triggerUpdates.Add(new ChangedParameter(i, BitConverter.GetBytes(set)));
                        return;
                    }
                }
                //Fall through, hash not found.
                Debug.LogWarning("Hash " + hash + " not found while trying to update a trigger.");
            }
        }

        /// <summary>
        /// Called on server when client data is received.
        /// </summary>
        /// <param name="data"></param>
        [Server]
        public void ClientDataReceived(AnimatorUpdate au)
        {
            if (!_isActive)
                return;
            if (!_clientAuthoritative)
                return;

            ApplyParametersUpdated(au.Data);
            //Add to parameters to send to clients.
            _updatesFromClients.Add(au);
        }

        /// <summary>
        /// Called on clients when server data is received.
        /// </summary>
        /// <param name="data"></param>
        [Client]
        public void ServerDataReceived(AnimatorUpdate au)
        {
            if (!_isActive)
                return;

            //If also server, client host, then do nothing. Animations already ran on server.
            if (base.isServer)
                return;

            //If has authority.
            if (base.hasAuthority)
            {
                //No need to sync to self if client authoritative.
                if (_clientAuthoritative)
                    return;
                //Not client authoritative, but also don't sync to owner.
                else if (!_clientAuthoritative && !_synchronizeToOwner)
                    return;
            }

            ApplyParametersUpdated(au.Data);
        }


    }
}

