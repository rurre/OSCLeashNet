using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using BuildSoft.OscCore;

namespace OSCLeashNet
{
    public static class Program
    {
        const string ParamPrefix = "/avatar/parameters/";
        const string InputPrefix = "/input/";
        
        static readonly string ZPosAddress = $"{ParamPrefix}{Config.Instance.Parameters["Z_Positive"]}";
        static readonly string ZNegAddress = $"{ParamPrefix}{Config.Instance.Parameters["Z_Negative"]}";
        static readonly string XPosAddress = $"{ParamPrefix}{Config.Instance.Parameters["X_Positive"]}";
        static readonly string XNegAddress = $"{ParamPrefix}{Config.Instance.Parameters["X_Negative"]}";
        static readonly string GrabAddress = $"{ParamPrefix}{Config.Instance.Parameters["PhysboneParameter"]}_IsGrabbed";
        static readonly string StretchAddress = $"{ParamPrefix}{Config.Instance.Parameters["PhysboneParameter"]}_Stretch";
        
        static readonly object LockObj = new object();
        static readonly LeashParameters Leash = new LeashParameters();

        static readonly float RunDeadzone = Config.Instance.RunDeadzone;
        static readonly float WalkDeadzone = Config.Instance.WalkDeadzone;
        static readonly TimeSpan InactiveDelay = TimeSpan.FromSeconds(Config.Instance.InputSendDelay);
        static readonly bool Logging = Config.Instance.Logging;
        
        static OscClient Client;
        static OscServer Server;
        
        public static async Task Main()
        {
            Console.Title = "OSCLeashNet";
            Console.WriteLine("\x1b OSCLeash is Running! \x1b");
            Console.WriteLine(Config.Instance.Ip == IPAddress.Loopback.ToString() ? "IP: Localhost" : $"IP: {Config.Instance.Ip} | Not Localhost? Wack.");
            Console.WriteLine("Listening on port: " + Config.Instance.ListeningPort);
            Console.WriteLine("Sending to port: " + Config.Instance.SendingPort);
            Console.WriteLine($"Run deadzone {MathF.Round(Config.Instance.RunDeadzone * 100, 3)}% of stretch");
            Console.WriteLine($"Walking deadzone {MathF.Round(Config.Instance.WalkDeadzone * 100, 3)}% of stretch");
            Console.WriteLine($"Delays of {Config.Instance.ActiveDelay * 1000}ms & {Config.Instance.InactiveDelay * 1000}ms");

            Client = new OscClient(Config.Instance.Ip, Config.Instance.SendingPort);

            StartServer();
            await Task.Run(async () =>
            {
                LeashOutput(0f, 0f, 0f);
                TimeSpan delay = TimeSpan.FromSeconds(Config.Instance.ActiveDelay);
                while(true)
                {
                    LeashRun();
                    await Task.Delay(delay);
                }
            });
        }
        
        static void StartServer()
        {
            if(IPGlobalProperties.GetIPGlobalProperties().GetActiveUdpListeners().Any(x => x.Port == Config.Instance.ListeningPort))
            {
                Console.WriteLine("\x1b                                                            \x1b");
                Console.WriteLine($"\x1b  Warning: An application is already running on port {Config.Instance.ListeningPort}!  \x1b");
                Console.WriteLine("\x1b                                                            \x1b");
                Console.WriteLine("Press any key to Exit.");

                Console.ReadKey(true);
                Environment.Exit(0);
            }

            Server = new OscServer(Config.Instance.ListeningPort);

            Server.TryAddMethod(ZPosAddress, OnReceiveZPos);
            Server.TryAddMethod(ZNegAddress, OnReceiveZNeg);
            Server.TryAddMethod(XPosAddress, OnReceiveXPos);
            Server.TryAddMethod(XNegAddress, OnReceiveXNeg);
            Server.TryAddMethod(GrabAddress, OnReceiveGrab);
            Server.TryAddMethod(StretchAddress, OnReceiveStretch);

            Server.Start();
        }
        
        static void LeashRun()
        {
            bool leashGrabbed, leashReleased;
            float verticalOutput, horizontalOutput;

            lock(LockObj)
            {
                verticalOutput = (Leash.ZPositive - Leash.ZNegative) * Leash.Stretch;
                horizontalOutput = (Leash.XPositive - Leash.XNegative) * Leash.Stretch;

                leashGrabbed = Leash.Grabbed;

                if(leashGrabbed)
                    Leash.WasGrabbed = true;

                leashReleased = Leash.Grabbed != Leash.WasGrabbed;

                if(leashReleased)
                    Leash.WasGrabbed = false;
            }

            if(leashGrabbed)
            {
                if(Leash.Stretch > RunDeadzone)
                    LeashOutput(verticalOutput, horizontalOutput, 1f);
                else if(Leash.Stretch > WalkDeadzone)
                    LeashOutput(verticalOutput, horizontalOutput, 0f);
                else
                    LeashOutput(0f, 0f, 0f);
            }
            else if(leashReleased)
            {
                LeashOutput(0f, 0f, 0f);
                Thread.Sleep(InactiveDelay);
                LeashOutput(0f, 0f, 0f);
            }
            else
            {
                Thread.Sleep(InactiveDelay);
            }
        }

        static void LeashOutput(float vertical, float horizontal, float run)
        {
            Client.Send($"{InputPrefix}Vertical", vertical);
            Client.Send($"{InputPrefix}Horizontal", horizontal);
            Client.Send($"{InputPrefix}Run", run);

            if(Logging)
                Console.WriteLine($"Sending: Vertical - {MathF.Round(vertical, 2)} | Horizontal = {MathF.Round(horizontal, 2)} | Run - {run}");
        }
        
        static void OnReceiveZPos(OscMessageValues msg)
        {
            try
            {
                lock(LockObj)
                    Leash.ZPositive = msg.ReadFloatElement(0);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception occured when trying to read float value on address {ZPosAddress}:\n{ex.Message}");
            }
        }
        
        static void OnReceiveZNeg(OscMessageValues msg)
        {
            try
            {
                lock(LockObj)
                    Leash.ZNegative = msg.ReadFloatElement(0);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception occured when trying to read float value on address {ZNegAddress}:\n{ex.Message}");
            }
        }

        static void OnReceiveXPos(OscMessageValues msg)
        {
            try
            {
                lock(LockObj)
                    Leash.XPositive = msg.ReadFloatElement(0);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception occured when trying to read float value on address {XPosAddress}:\n{ex.Message}");
            }
        }

        static void OnReceiveXNeg(OscMessageValues msg)
        {
            try
            {
                lock(LockObj)
                    Leash.XNegative = msg.ReadFloatElement(0);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception occured when trying to read float value on address {XNegAddress}:\n{ex.Message}");
            }
        }

        static void OnReceiveStretch(OscMessageValues msg)
        {
            try
            {
                lock(LockObj)
                    Leash.Stretch = msg.ReadFloatElement(0);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception occured when trying to read float value on address {StretchAddress}:\n{ex.Message}");
            }
        }

        static void OnReceiveGrab(OscMessageValues msg)
        {
            try
            {
                lock(LockObj)
                    Leash.Grabbed = msg.ReadBooleanElement(0);
            }
            catch(Exception ex)
            {
                Console.WriteLine($"Exception occured when trying to read float value on address {GrabAddress}:\n{ex.Message}");
            }
        }
    }
}