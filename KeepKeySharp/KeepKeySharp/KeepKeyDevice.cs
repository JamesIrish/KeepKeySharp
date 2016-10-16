using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HidLibrary;
using KeepKeySharp.Contracts;

namespace KeepKeySharp
{
    public class KeepKeyDevice : IDisposable
    {

        private const int VendorId = 0x2B24;
        private const int ProductId = 0x0001;

        private HidDevice _device;
        private KeepKeyCommunicator _communicator;

        private bool _attached;
        public bool IsConnected => _attached;

        private bool _connectedToDriver;
        private bool _disposed;

        /// <summary>Occurs when a KeepKey device is connected.</summary>
        public event EventHandler Connected;

        /// <summary>Occurs when a KeepKey device is disconnected.</summary>
        public event EventHandler Disconnected;

        private void OnInserted()
        {
            _attached = true;
            Connected?.Invoke(this, EventArgs.Empty);
        }
        private void OnRemoved()
        {
            _attached = false;
            Disconnected?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>Attempts to connect to a KeepKey device.  After a successful connection, the Connected event will fire.</summary>
        /// <returns>true if a KeepKey device is connected, false otherwise.  If false, subsequently call WaitForKeepKeyConnectionAsync().</returns>
        public bool TryOpenDevice()
        {
            _device = HidDevices.Enumerate(VendorId, ProductId).FirstOrDefault();
            
            if (_device == null) return false;

            Construct();

            return true;
        }
        /// <summary>Checks for the presence of the KeepKey device. The Connected event will fire once it has been connected. Optionally you can await this method (the event will fire either way).</summary>
        /// <returns></returns>
        public async Task WaitForKeepKeyConnectionAsync()
        {
            while (_device == null && !_disposed)
            {
                await Task.Delay(100).ConfigureAwait(true);

                _device = HidDevices.Enumerate(VendorId, ProductId).FirstOrDefault();
            }

            if (_device == null) return;

            Construct();
        }
        private void Construct()
        {
            _communicator = new KeepKeyCommunicator(_device);

            _connectedToDriver = true;

            _device.Inserted += OnInserted;
            _device.Removed += OnRemoved;
            _device.MonitorDeviceEvents = true;
        }
        
        /// <summary>Closes the connection to the device.</summary>
        public void CloseDevice()
        {
            if (_connectedToDriver)
            {
                _device.CloseDevice();
                _connectedToDriver = false;
            }
        }

        /// <summary>Details of the KeepKey device</summary>
        public Features Features { get; internal set; }

        /// <summary>Queries the KeepKey for its details. While not essential we suggest you do this prior to any other operations.</summary>
        /// <returns></returns>
        public Features Initialize()
        {
            var init = new Initialize();
            var msg = Contracts.Initialize.SerializeToBytes(init);

            _communicator.SendMessage(msg, MessageType.MessageType_Initialize);

            MessageType recievedType;
            var received = _communicator.RecieveMessage(out recievedType);

            if (recievedType == MessageType.MessageType_Features)
            {
                Features = Features.Deserialize(received);
                return Features;
            }

            throw new NotImplementedException("Unable to process unexpected message type: " + recievedType);
        }

        /// <summary>Displays a message on the KeepKey screen.  Supply true to the second parameter to force the user to press the device button.</summary>
        /// <param name="message">The message to display.  Note the maximum number of characters is approximately 130 - text longer than this gets truncated by the device.</param>
        /// <param name="buttonProtection">true to wait for the user to press and hold the button on the device, false to return the message immediately.</param>
        /// <returns></returns>
        public string Ping(string message, bool buttonProtection = true)
        {
            // Build the message
            var ping = new Ping
            {                       // Provide ALL fields
                Message = message,
                ButtonProtection = buttonProtection,
                PinProtection = false,
                PassphraseProtection = false
            };
            // Serialize it to bytes
            var msg = Contracts.Ping.SerializeToBytes(ping);
            
            // Send the message to the device
            if (!_communicator.SendMessage(msg, MessageType.MessageType_Ping))
                throw new ApplicationException("Error writing to device");

            // Get the response from the device
            MessageType recievedType;
            var received = _communicator.RecieveMessage(out recievedType);

            // ButtonRequest was received
            if (recievedType == MessageType.MessageType_ButtonRequest)
            {
                // Acknowledge the button request & wait for the next response
                _communicator.SendMessage(ButtonAck.SerializeToBytes(new ButtonAck()), MessageType.MessageType_ButtonAck);
                received = _communicator.RecieveMessage(out recievedType);
            }
            
            // The device returned Success
            if (recievedType == MessageType.MessageType_Success)
                return Success.Deserialize(received).Message;

            // The device returned Failure
            if (recievedType == MessageType.MessageType_Failure)
            {
                var failure = Failure.Deserialize(received);
                return failure.Code.HasValue ? $"{failure.Code.GetValueOrDefault()} - {failure.Message}" : failure.Message;
            }
            
            throw new NotImplementedException("Unable to process unexpected message type: " + recievedType);
        }

        /// <summary>Get the Extended Public Key from the device for the BIP32 path specified.</summary>
        /// <param name="accountPath">BIP32 path e.g. "44'/0'/0'/0/0 = first Bitcoin account, first public key., from this you can derive the address.</param>
        /// <returns>The public key of the for the account at the path specified.</returns>
        public PublicKey GetPublicKey(string accountPath)
        {
            // Get the path as units
            var path = NBitcoin.KeyPath.Parse(accountPath);
            
            // Build the message
            var xpub = new GetPublicKey
            {
                AddressN = new List<uint>(path.Indexes),
                EcdsaCurveName = "secp256k1",
                ShowDisplay = false
            };

            // Serialize it to bytes
            var msg = Contracts.GetPublicKey.SerializeToBytes(xpub);

            // Send the message to the device
            if (!_communicator.SendMessage(msg, MessageType.MessageType_GetPublicKey))
                throw new ApplicationException("Error writing to device");

            // Get the response from the device
            MessageType recievedType;
            var received = _communicator.RecieveMessage(out recievedType);

            // PublicKey received
            if (recievedType == MessageType.MessageType_PublicKey)
                return PublicKey.Deserialize(received);

            // The device returned Failure
            if (recievedType == MessageType.MessageType_Failure)
                throw new KeepKeyException(Failure.Deserialize(received));

            throw new NotImplementedException("Unable to process unexpected message type: " + recievedType);
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    CloseDevice();
                }

                _disposed = true;
            }
        }
    }
}