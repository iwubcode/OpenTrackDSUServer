﻿using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace OpenTrackToDSUProtocol
{
    class Program
    {
        static void Main(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appsettings.json", false)
                .Build()
            ;

            DSUServer server = null;
            if (configuration.GetValue<bool>("DebugOpenTrack") != true)
            {
                string dsu_server_ip = configuration.GetValue<string>("DSUServerIp");
                if(dsu_server_ip == null)
            {
                    Console.WriteLine("DSUServerIp configuration is not found or invalid");
                    return;
                }

                int? dsu_server_port = configuration.GetValue<int?>("DSUServerPort");
                if (dsu_server_port == null)
                {
                    Console.WriteLine("DSUServerPort configuration is not found or invalid");
                    return;
                }

                server = new DSUServer(dsu_server_ip, dsu_server_port.Value);
            }

            string open_track_ip = configuration.GetValue<string>("OpenTrackIp");
            if (open_track_ip == null)
            {
                Console.WriteLine("OpenTrackIp configuration is not found or invalid");
                return;
            }

            int? open_track_port = configuration.GetValue<int?>("OpenTrackPort");
            if (open_track_port == null)
            {
                Console.WriteLine("OpenTrackPort configuration is not found or invalid");
                return;
            }

            bool? relative_data = configuration.GetValue<bool?>("RelativeData");
            if (relative_data == null)
            {
                relative_data = true;
            }

            bool? divide_by_gravity = configuration.GetValue<bool?>("DivideByGravity");
            if (divide_by_gravity == null)
            {
                divide_by_gravity = true;
            }

            OpenTrackReceiver receiver = null;
            if (server == null)
            {
                receiver = new OpenTrackReceiver(open_track_ip, open_track_port.Value, relative_data.Value, divide_by_gravity.Value);
            }
            else
            {
                receiver = new OpenTrackReceiver(open_track_ip, open_track_port.Value, server, relative_data.Value, divide_by_gravity.Value);
            }

            receiver.Start();
            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();
            receiver.Stop();
        }
    }
}
