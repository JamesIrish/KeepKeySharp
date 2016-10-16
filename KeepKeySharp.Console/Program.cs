using System;
using System.DirectoryServices.AccountManagement;
using System.Linq;
using System.Threading.Tasks;
using KeepKeySharp.Contracts;
using NBitcoin;

namespace KeepKeySharp.Console
{
    class Program
    {
        private UserPrincipal _currentUser;

        static void Main(string[] args)
        {
            var p = new Program();
            p.Run().Wait();
        }

        private async Task Run()
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
                    System.Console.WriteLine();
                    System.Console.WriteLine("No KeepKey connected.  Please connect KeepKey to the computer NOW. (Leave the console running)");

                    await kk.WaitForKeepKeyConnectionAsync();
                }

                System.Console.WriteLine();
                System.Console.WriteLine("Getting features...");

                // Step 1.  Initialize the device and get a list of it's features
                var features = kk.Initialize();

                // Print features to console - ouch!
                System.Console.WriteLine("Vendor:     {0}", features.Vendor);
                System.Console.WriteLine("DeviceId:   {0}", features.DeviceId);
                System.Console.WriteLine("Label:      {0}", features.Label);
                System.Console.WriteLine("Version:    {0}.{1}.{2}", features.MajorVersion.GetValueOrDefault(), features.MinorVersion.GetValueOrDefault(), features.PatchVersion.GetValueOrDefault());
                System.Console.WriteLine("Coins:      {0}", features.Coins.Select(c => $"{c.CoinName} ({c.CoinShortcut})").Aggregate((c, n) => $"{c}, {n}"));
                if (features.Policies.Any()) System.Console.WriteLine("Policies:   {0}", features.Policies.Select(p => p.PolicyName).Aggregate((c, n) => $"{c}, {n}"));
                System.Console.Write("Protection: ");
                var proPin = features.PinProtection.GetValueOrDefault();
                var proPass = features.PassphraseProtection.GetValueOrDefault();
                if (proPin && proPass) System.Console.WriteLine("Pin, Passphrase");
                else if (proPin && !proPass) System.Console.WriteLine("Pin ONLY");
                else if (!proPin && proPass) System.Console.WriteLine("Passphrase ONLY");
                else System.Console.WriteLine("Neither!");
                System.Console.Write("Cached:     ");
                var cchPin = features.PinCached.GetValueOrDefault();
                var cchPass = features.PassphraseCached.GetValueOrDefault();
                if (cchPin && cchPass) System.Console.WriteLine("Pin, Passphrase");
                else if (cchPin && !cchPass) System.Console.WriteLine("Pin");
                else if (!cchPin && cchPass) System.Console.WriteLine("Passphrase");
                else System.Console.WriteLine("[nothing]");

                // Step 2.  Have some fun with Ping!
                System.Console.WriteLine();
                System.Console.WriteLine("Pinging device...  (look at it, hold the button)");
                var name = _currentUser.DisplayName;                            // fullname
                if (name.IndexOf(' ') != -1) name = name.Split(' ')[0].Trim();  // firstname
                var pingReply = kk.Ping($"Hello {name}!  Lorem ipsum dolor sit amet, consectetur adipiscing elit. Nam lacinia quis mauris volutpat scelerisque.", true);
                System.Console.WriteLine(pingReply);

                // Step 3a.  Getting the public key MIGHT require the Pin to be entered so lets define a function that allows it be entered via the console
                var pinChallengeFunction = new Func<PinMatrixRequestType?, string>(t =>
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine("PIN CHALLENGE!!");
                    System.Console.WriteLine("┌───┬───┬───┐");
                    System.Console.WriteLine("│ 7 │ 8 │ 9 │");
                    System.Console.WriteLine("├───┼───┼───┤");
                    System.Console.WriteLine("│ 4 │ 5 │ 6 │");
                    System.Console.WriteLine("├───┼───┼───┤");
                    System.Console.WriteLine("│ 1 │ 2 │ 3 │");
                    System.Console.WriteLine("└───┴───┴───┘");
                    System.Console.WriteLine("Match the device screen with the above and enter the numbers of your scrambled pin below, press <enter> to send.");
                    var pin = System.Console.ReadLine();
                    System.Console.WriteLine();
                    return pin;
                });

                // Step 3b.  Get the public key and extract the address
                for (var acc = 0; acc < 2; acc++)
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine("Getting addresses for Bitcoin account {0}...", acc + 1);
                    System.Console.WriteLine();
                    for (var adr = 0; adr < 10; adr++)
                    {
                        var publicKey = kk.GetPublicKey(pinChallengeFunction, $"44'/0'/{acc}'/0/{adr}");
                        var extPubKey = ExtPubKey.Parse(publicKey.Xpub, Network.Main);
                        var pubKey = extPubKey.PubKey;
                        var address = pubKey.GetAddress(Network.Main);
                        System.Console.WriteLine("Address: {0}", address);
                    }
                }

                System.Console.WriteLine();
                System.Console.WriteLine("Press <enter> to quit console gracefully.");
                System.Console.ReadLine();

                kk.CloseDevice();
            }
        }

        private void KkOnConnected(object sender, EventArgs eventArgs)
        {
            System.Console.WriteLine();
            System.Console.WriteLine("KeepKey connected");
            System.Console.WriteLine();
        }

        private static void KkOnDisconnected(object sender, EventArgs eventArgs)
        {
            System.Console.WriteLine();
            System.Console.WriteLine("KeepKey disconnected.");
        }
    }
}
