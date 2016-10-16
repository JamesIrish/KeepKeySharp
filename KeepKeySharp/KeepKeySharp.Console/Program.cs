using System;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using NBitcoin;

namespace KeepKeySharp.Console
{
    class Program
    {
        private UserPrincipal _currentUser;

        static void Main(string[] args)
        {
            var p = new Program();
            p.Run();
        }

        private void Run()
        {
            _currentUser = UserPrincipal.Current;

            System.Console.WriteLine("Welcome to the KeepKey C# example console!");
            System.Console.WriteLine("Attempting to find KeepKey...");

            using (var kk = new KeepKeyDevice())
            {
                kk.Connected += KkOnConnected;
                kk.Disconnected += KkOnDisconnected;

                if (!kk.TryOpenDevice())
                {
                    kk.WaitForKeepKeyConnectionAsync();
                    System.Console.WriteLine();
                    System.Console.WriteLine("No KeepKey connected.  Please connect KeepKey to the computer NOW. (Leave the console running)");
                }

                System.Console.WriteLine();
                System.Console.WriteLine("Press <enter> to quit.");
                System.Console.ReadLine();

                kk.CloseDevice();
            }
        }

        private void KkOnConnected(object sender, EventArgs eventArgs)
        {
            System.Console.WriteLine();
            System.Console.WriteLine("KeepKey connected");

            var kk = (KeepKeyDevice) sender;

            System.Console.WriteLine();
            System.Console.WriteLine("Getting features...");

            // Step 1.  Initialize the device and get a list of it's features
            var features = kk.Initialize();

            System.Console.WriteLine("Vendor:     {0}", features.Vendor);
            System.Console.WriteLine("DeviceId:   {0}", features.DeviceId);
            System.Console.WriteLine("Label:      {0}", features.Label);
            System.Console.WriteLine("Version:    {0}.{1}.{2}", features.MajorVersion.GetValueOrDefault(), features.MinorVersion.GetValueOrDefault(), features.PatchVersion.GetValueOrDefault());
            System.Console.WriteLine("Coins:      {0}", features.Coins.Select(c => $"{c.CoinName} ({c.CoinShortcut})").Aggregate((c, n) => $"{c}, {n}"));
            if (features.Policies.Any()) System.Console.WriteLine("Policies:   {0}", features.Policies.Select(p => p.PolicyName).Aggregate((c, n) => $"{c}, {n}"));
            System.Console.Write("Protection: ");
            if (features.PinProtection.HasValue && features.PinProtection.Value) System.Console.Write("Pin");
            if (features.PassphraseProtection.HasValue && features.PassphraseProtection.Value) System.Console.Write(", Passphrase");
            System.Console.WriteLine();

            System.Console.WriteLine();
            System.Console.WriteLine("Pinging device...  (look at it, hold the button)");
            var name = _currentUser.DisplayName;                            // fullname
            if (name.IndexOf(' ') != -1) name = name.Split(' ')[0].Trim();  // firstname
            var pingReply = kk.Ping($"Hello {name}, how long can this message be, does it scroll or just error?  Lorem ipsum dolor sit amet, consectetur adipiscing elit.", true);
            System.Console.WriteLine(pingReply);

            for (var acc = 0; acc < 2; acc++)
            {
                System.Console.WriteLine();
                System.Console.WriteLine("Getting addresses for Bitcoin account {0}...", acc + 1);
                for (var adr = 0; adr < 10; adr++)
                {
                    var publicKey = kk.GetPublicKey($"44'/0'/{acc}'/0/{adr}");
                    var extPubKey = ExtPubKey.Parse(publicKey.Xpub, Network.Main);
                    var pubKey = extPubKey.PubKey;
                    var address = pubKey.GetAddress(Network.Main);
                    System.Console.WriteLine("Address: {0}", address);
                }
            }
        }

        private static void KkOnDisconnected(object sender, EventArgs eventArgs)
        {
            System.Console.WriteLine();
            System.Console.WriteLine("KeepKey disconnected.");
        }
    }
}
