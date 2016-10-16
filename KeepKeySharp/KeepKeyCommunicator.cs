using System;
using HidLibrary;
using KeepKeySharp.Contracts;
using SilentOrbit.ProtocolBuffers;

namespace KeepKeySharp
{
    internal class KeepKeyCommunicator
    {
        private readonly HidDevice _device;

        public KeepKeyCommunicator(HidDevice device)
        {
            _device = device;
        }

        public bool SendMessage(byte[] message, MessageType type)
        {
            var msgSize = message.Length;
            var msgId = (int) type;
            var data = new byte[msgSize + 1024];
            data[0] = (byte) '#';
            data[1] = (byte) '#';
            data[2] = (byte) ((msgId >> 8) & 0xFF);
            data[3] = (byte) (msgId & 0xFF);
            data[4] = (byte) ((msgSize >> 24) & 0xFF);
            data[5] = (byte) ((msgSize >> 16) & 0xFF);
            data[6] = (byte) ((msgSize >> 8) & 0xFF);
            data[7] = (byte) (msgSize & 0xFF);

            Array.Copy(message, 0, data, 8, message.Length);

            var chunks = (msgSize+8) / 63;
            for (var i = 0; i <= chunks; i++)
            {
                var buffer = new byte[64];
                buffer[0] = (byte)'?';

                Array.Copy(data, i*63, buffer, 1, 63);

                if (!_device.Write(buffer))
                    return false;
            }

            return true;
        }

        internal byte[] RecieveMessage(out MessageType gotType)
        {
            byte[] data;
            byte[] b;
            
            int msgSize;
            var invalidChunksCounter = 0;

            int position;

            for (;;)
            {
                b = _device.Read().Data;
                
                if (b.Length < 9 || b[0] != (byte)'?' || b[1] != (byte)'#' || b[2] != (byte)'#')
                {
                    if (invalidChunksCounter++ > 5)
                        throw new ProtocolBufferException("Too many invalid chunks");
                    continue;
                }

                if (b[0] != (byte)'?' || b[1] != (byte)'#' || b[2] != (byte)'#')
                    continue;

                gotType = (MessageType)(((int)b[3] & 0xFF) << 8) + ((int)b[4] & 0xFF);
                msgSize = (((int) b[5] & 0xFF) << 24)
                           + (((int) b[6] & 0xFF) << 16)
                           + (((int) b[7] & 0xFF) << 8)
                           + ((int) b[8] & 0xFF);

                data = new byte[msgSize + 64];

                Array.Copy(b, 9, data, 0, b.Length - 9);

                position = b.Length - 9;

                break;
            }

            invalidChunksCounter = 0;

            while (position < msgSize)
            {
                b = _device.Read().Data;

                if (b[0] != (byte)'?')
                {
                    if (invalidChunksCounter++ > 5)
                        throw new ProtocolBufferException("Too many invalid chunks (2)");
                    continue;
                }
                
                Array.Copy(b, 1, data, position, b.Length - 1);

                position += b.Length - 1;
            }

            var msgData = new byte[msgSize];
            Array.Copy(data, 0, msgData, 0, msgSize);

            return msgData;
        }
    }
}