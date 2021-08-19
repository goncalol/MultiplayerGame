using Mirror;
using System;
using UnityEngine;

namespace FirstGearGames.Mirrors.Assets.FlexNetworkAnimators
{
    /// <summary>
    /// Compression levels for data.
    /// </summary>
    public enum CompressionLevels : byte
    {
        None = 0,
        //Data can fit into a sbyte.
        Level1Positive = 1,
        Level1Negative = 2,
        //Data can fit into a short.
        Level2Positive = 3,
        Level2Negative = 4
    }

    public static class Compression
    {

        #region Floats.
        /// <summary>
        /// Returns a decompressed float.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="readIndex"></param>
        /// <returns></returns>
        public static float ReturnDecompressedFloat(byte[] data, ref int readIndex)
        {
            float result;
            CompressionLevels cl = (CompressionLevels)data[readIndex];
            readIndex += 1;

            //Decompressed from a byte.
            if (cl == CompressionLevels.Level1Positive)
            {
                result = (float)(data[readIndex] / 100f);
                readIndex += 1;
            }
            else if (cl == CompressionLevels.Level1Negative)
            {
                result = (float)(data[readIndex] / -100f);
                readIndex += 1;
            }
            //Decompressed from a short.
            else if (cl == CompressionLevels.Level2Positive)
            {
                result = (float)BitConverter.ToUInt16(data, readIndex) / 100f;
                readIndex += 2;
            }
            //Decompressed from a short.
            else if (cl == CompressionLevels.Level2Positive)
            {
                result = (float)BitConverter.ToUInt16(data, readIndex) / -100f;
                readIndex += 2;
            }
            //Not compressed.
            else
            {
                result = BitConverter.ToSingle(data, readIndex);
                readIndex += 4;
            }

            return result;
        }

        /// <summary>
        /// Returns a byte array for a compressed float.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] ReturnCompressedFloat(float value)
        {
            CompressionLevels cl;
            byte[] compressedBytes;
            /* When checking ranges I use maxValue - 1
             * because of rounding. */
            float absValue = Mathf.Abs(value);
            //Can compress into sbyte.
            if (absValue <= byte.MaxValue - 1)
            {
                //Determine if positive or negative.
                if (Mathf.Sign(value) > 0)
                    cl = CompressionLevels.Level1Positive;
                else
                    cl = CompressionLevels.Level1Negative;
                //Get compressed value.
                byte val = (byte)Mathf.Round(absValue * 100f);
                compressedBytes = new byte[] { val };
            }
            //Can compress into short.
            else if (absValue < ushort.MaxValue - 1)
            {
                //Determine if positive or negative.
                if (Mathf.Sign(value) > 0)
                    cl = CompressionLevels.Level2Positive;
                else
                    cl = CompressionLevels.Level2Negative;
                //Get compressed value.
                short val = (short)Mathf.Round(absValue * 100f);
                compressedBytes = BitConverter.GetBytes(val);
            }
            //Cannot compress.
            else
            {
                cl = CompressionLevels.None;
                compressedBytes = BitConverter.GetBytes(value);
            }

            ////Resize to include compression level and compressed bytes.
            byte[] result = new byte[1 + compressedBytes.Length];
            result[0] = (byte)cl;
            Array.Copy(compressedBytes, 0, result, 1, compressedBytes.Length);

            return result;
        }
        #endregion

        #region Integers.
        /// <summary>
        /// Returns a decompressed float.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="readIndex"></param>
        /// <returns></returns>
        public static int ReturnDecompressedInteger(byte[] data, ref int readIndex)
        {
            int result;
            CompressionLevels cl = (CompressionLevels)data[readIndex];
            readIndex += 1;

            //Decompressed from a byte.
            if (cl == CompressionLevels.Level1Positive)
            {
                result = (int)data[readIndex];
                readIndex += 1;
            }
            else if (cl == CompressionLevels.Level1Negative)
            {
                result = (int)-data[readIndex];
                readIndex += 1;
            }
            //Decompressed from a short.
            else if (cl == CompressionLevels.Level2Positive)
            {
                result = (int)BitConverter.ToUInt16(data, readIndex);
                readIndex += 2;
            }
            //Decompressed from a short.
            else if (cl == CompressionLevels.Level2Positive)
            {
                result = (int)-BitConverter.ToUInt16(data, readIndex);
                readIndex += 2;
            }
            //Not compressed.
            else
            {
                result = BitConverter.ToInt32(data, readIndex);
                readIndex += 4;
            }

            return result;
        }


        /// <summary>
        /// Returns a byte array for a compressed integer.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] ReturnCompressedInteger(int value)
        {
            CompressionLevels cl;
            byte[] compressedBytes;

            float absValue = Mathf.Abs(value);
            //Can compress into sbyte.
            if (absValue <= byte.MaxValue)
            {
                //Determine if positive or negative.
                if (Mathf.Sign(value) > 0)
                    cl = CompressionLevels.Level1Positive;
                else
                    cl = CompressionLevels.Level1Negative;
                //Get compressed value.
                byte val = (byte)absValue;
                compressedBytes = new byte[] { val };
            }
            //Can compress into short.
            else if (absValue < ushort.MaxValue - 1)
            {
                //Determine if positive or negative.
                if (Mathf.Sign(value) > 0)
                    cl = CompressionLevels.Level2Positive;
                else
                    cl = CompressionLevels.Level2Negative;
                //Get compressed value.
                short val = (short)absValue;
                compressedBytes = BitConverter.GetBytes(val);
            }
            //Cannot compress.
            else
            {
                cl = CompressionLevels.None;
                compressedBytes = BitConverter.GetBytes(value);
            }

            ////Resize to include compression level and compressed bytes.
            byte[] result = new byte[1 + compressedBytes.Length];
            result[0] = (byte)cl;
            Array.Copy(compressedBytes, 0, result, 1, compressedBytes.Length);

            return result;
        }
        #endregion

    }


    [System.Serializable]
    public struct AnimatorUpdate
    {
        public byte ComponentIndex;
        public uint NetworkIdentity;
        public byte[] Data;

        public AnimatorUpdate(byte componentIndex, uint networkIdentity, byte[] data)
        {
            ComponentIndex = componentIndex;
            NetworkIdentity = networkIdentity;
            Data = data;
        }
    }


    public static class FNASerializer
    {
        public static void WriteAnimatorUpdate(this NetworkWriter writer, AnimatorUpdate au)
        {
            //Component index.
            writer.WriteByte(au.ComponentIndex);

            //Write compressed network identity.
            //byte.
            if (au.NetworkIdentity <= byte.MaxValue)
            {
                writer.WriteByte(1);
                writer.WriteByte((byte)au.NetworkIdentity);
            }
            //ushort.
            else if (au.NetworkIdentity <= ushort.MaxValue)
            {
                writer.WriteByte(2);
                writer.WriteUInt16((ushort)au.NetworkIdentity);
            }
            //Full value.
            else
            {
                writer.WriteByte(4);
                writer.WriteUInt32(au.NetworkIdentity);
            }

            //Animation data.
            //Compress data length.
            if (au.Data.Length <= byte.MaxValue)
            {
                writer.WriteByte(1);
                writer.WriteByte((byte)au.Data.Length);
            }
            else if (au.Data.Length <= ushort.MaxValue)
            {
                writer.WriteByte(2);
                writer.WriteUInt16((ushort)au.Data.Length);
            }
            else
            {
                writer.WriteByte(4);
                writer.WriteInt32(au.Data.Length);
            }
            if (au.Data.Length > 0)
                writer.WriteBytes(au.Data, 0, au.Data.Length);
        }

        public static AnimatorUpdate ReadAnimatorUpdate(this NetworkReader reader)
        {
            AnimatorUpdate au = new AnimatorUpdate();

            //Component index.
            au.ComponentIndex = reader.ReadByte();

            //Network identity.
            byte netIdCompression = reader.ReadByte();
            if (netIdCompression == 1)
                au.NetworkIdentity = reader.ReadByte();
            else if (netIdCompression == 2)
                au.NetworkIdentity = reader.ReadUInt16();
            else
                au.NetworkIdentity = reader.ReadUInt32();

            //Animation data.
            byte dataLengthCompression = reader.ReadByte();
            int dataLength;
            if (dataLengthCompression == 1)
                dataLength = reader.ReadByte();
            else if (dataLengthCompression == 2)
                dataLength = reader.ReadUInt16();
            else
                dataLength = reader.ReadInt32();

            if (dataLength > 0)
                au.Data = reader.ReadBytes(dataLength);
            else
                au.Data = new byte[0];

            return au;
        }



    }


}