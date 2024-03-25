using System;
using System.Net;
using System.Reflection.Emit;
using System.Text;
using System.Web;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;

namespace WebServerCommands
{
    internal sealed class ModEntry : Mod
    {
        public static HttpListener listener;
        public static string url = "http://127.0.0.1:56802/";

        // https://gist.github.com/Shockah/ec111245868ee9b7dbf2ca2928dd2896
        private static readonly Lazy<Action<string>> AddToRawCommandQueue = new(() =>
        {
            var scoreType = AccessTools.TypeByName("StardewModdingAPI.Framework.SCore, StardewModdingAPI")!;
            var commandQueueType = AccessTools.TypeByName("StardewModdingAPI.Framework.CommandQueue, StardewModdingAPI")!;
            var scoreGetter = AccessTools.PropertyGetter(scoreType, "Instance")!;
            var rawCommandQueueField = AccessTools.Field(scoreType, "RawCommandQueue")!;
            var commandQueueAddMethod = AccessTools.Method(commandQueueType, "Add");
            var dynamicMethod = new DynamicMethod("AddToRawCommandQueue", null, new Type[] { typeof(string) });
            var il = dynamicMethod.GetILGenerator();
            il.Emit(OpCodes.Call, scoreGetter);
            il.Emit(OpCodes.Ldfld, rawCommandQueueField);
            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Call, commandQueueAddMethod);
            il.Emit(OpCodes.Ret);
            return dynamicMethod.CreateDelegate<Action<string>>();
        });

        private static void ExecuteCommand(string command)
        {
            if (string.IsNullOrEmpty(command))
                return;
            AddToRawCommandQueue.Value(command);
        }

        public async Task HandleIncomingConnections()
        {
            while (true)
            {
                HttpListenerContext ctx = await listener.GetContextAsync();

                HttpListenerRequest req = ctx.Request;
                HttpListenerResponse resp = ctx.Response;

                byte[] data = Encoding.UTF8.GetBytes("Please GET /execute with the ?command=asdf parameter for running commands.");

                this.Monitor.Log($"Received request {req.Url?.AbsolutePath}", LogLevel.Info);

                if (req.Url?.AbsolutePath == "/execute") {
                    string querystring = req.Url.Query;
                    this.Monitor.Log("URL Query (full string): " + querystring, LogLevel.Info);

                    var uri = new Uri(req.Url.AbsoluteUri);
                    var queries = HttpUtility.ParseQueryString(uri.Query);

                    var cmd = queries.Get("command");
                    if (cmd != "" && cmd is not null) {
                        ExecuteCommand(cmd);

                        data = Encoding.UTF8.GetBytes($"Running command {cmd}");
                    }
                }

                await resp.OutputStream.WriteAsync(data, 0, data.Length);
                resp.Close();

            }
        }

        public async Task InitialiseServer()
        {
            listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            this.Monitor.Log($"Listening for connections on {url}", LogLevel.Info);

            Task listenTask = HandleIncomingConnections();
            listenTask.GetAwaiter().GetResult();

            listener.Close();
        }

        public override void Entry(IModHelper helper)
        {
            Task.Factory.StartNew(() => InitialiseServer());


        }
    }
}