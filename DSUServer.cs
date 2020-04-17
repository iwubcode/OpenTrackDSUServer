using Force.Crc32;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OpenTrackToDSUProtocol
{
    public class DSUServer
    {
        private Socket _socket;
        private Thread _packet_receive_thread;
        private byte[] _packet_received_bytes = new byte[1024];
        private bool _waiting_on_packet = false;

        class ClientData
        {
            public DateTime last_access_time = DateTime.UtcNow;
            public uint last_packet_count = 0;
        }
        private Dictionary<EndPoint, ClientData> _client_endpoint_to_client_data;

        private const ushort max_protocol_version_supported = 1001;
        private uint _server_id = 0;

        private bool Running
        {
            get;
            set;
        } = false;

        enum MessageType
        {
            VersionRequest = 0x100000,
            VersionResponse = 0x100000,
            PortRequest = 0x100001,
            PortResponse = 0x100001,
            PadDataRequest = 0x100002,
            PadDataResponse = 0x100002,
        };

        class Header
        {
            public string identifier;
            public ushort protocol_version;
            public ushort packet_size;
            public uint crc_value;
            public uint client_id;
            public uint message_type;

            public static uint size = 20;
        };

        class OutputControllerInformation
        {
            public byte slot;
            public byte slot_state;
            public byte device_model;
            public byte connection_type;
            public byte[] mac_address;
            public byte battery_status;

            public static ushort size = 11;
        };

        class OutputControllerTouchData
        {
            public byte is_touch_active;
            public byte touch_id;
            public ushort x_pos;
            public ushort y_pos;

            public static uint size = 6;
        };

        class OutputControllerData
        {
            public byte controller_connected;
            public uint packet_number;
            public byte button_data;
            public byte more_button_data;
            public byte share_button;
            public byte ps_button;
            public byte left_stick_x;
            public byte left_stick_y;
            public byte right_stick_x;
            public byte right_stick_y;
            public byte d_pad_left;
            public byte d_pad_down;
            public byte d_pad_right;
            public byte d_pad_up;
            public byte analog_y;
            public byte analog_b;
            public byte analog_a;
            public byte analog_x;
            public byte analog_r1;
            public byte analog_l1;
            public byte analog_r2;
            public byte analog_l2;
            public OutputControllerTouchData first_touch = new OutputControllerTouchData();
            public OutputControllerTouchData second_touch = new OutputControllerTouchData();
            public Int64 motion_timestamp;
            public float accelerometer_x;
            public float accelerometer_y;
            public float accelerometer_z;
            public float gyro_pitch;
            public float gyro_yaw;
            public float gyro_roll;

            public static ushort size = 69;
        };

        public DSUServer(string ip, int port)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.ReuseAddress, true);
            _socket.Bind(new IPEndPoint(IPAddress.Parse(ip), port));
            Console.WriteLine($"DSUServer listening for clients on ip '{ip}' and port '{port.ToString()}'");

            _client_endpoint_to_client_data = new Dictionary<EndPoint, ClientData>();
        }

        private void ProcessIncomingMessage(EndPoint client_endpoint, byte[] message_bytes)
        {
            var header = ProcessHeader(message_bytes);
            if (header == null)
            {
                return;
            }

            ProcessMessage(client_endpoint, header, message_bytes);
        }

        private Header ProcessHeader(byte[] message_bytes)
        {
            Header header = new Header();
            int index = 0;

            header.identifier = "DSUC";
            if (message_bytes[0] != header.identifier[0] || message_bytes[1] != header.identifier[1] || message_bytes[2] != header.identifier[2] || message_bytes[3] != header.identifier[3])
            {
                return null;
            }
            index += 4;

            header.protocol_version = BitConverter.ToUInt16(message_bytes, index);
            index += 2;

            if (header.protocol_version > max_protocol_version_supported)
            {
                return null;
            }

            header.packet_size = BitConverter.ToUInt16(message_bytes, index);
            index += 2;

            if (header.packet_size < 0)
            {
                return null;
            }

            header.packet_size += 16;
            if (header.packet_size > message_bytes.Length)
            {
                return null;
            }
            else if (header.packet_size < message_bytes.Length)
            {
                byte[] truncated_message = new byte[header.packet_size];
                Array.Copy(message_bytes, truncated_message, header.packet_size);
                message_bytes = truncated_message;
            }

            header.crc_value = BitConverter.ToUInt32(message_bytes, index);

            // Zero out the CRC so we can recalculate to compare
            message_bytes[index++] = 0;
            message_bytes[index++] = 0;
            message_bytes[index++] = 0;
            message_bytes[index++] = 0;

            uint crc_calculated = Crc32Algorithm.Compute(message_bytes);
            if (header.crc_value != crc_calculated)
            {
                return null;
            }

            header.client_id = BitConverter.ToUInt32(message_bytes, index);
            index += 4;

            header.message_type = BitConverter.ToUInt32(message_bytes, index);
            index += 4;

            return header;
        }

        private void WriteHeader(ref byte[] message_bytes, Header header)
        {
            int index = 0;
            message_bytes[index++] = (byte)header.identifier[0];
            message_bytes[index++] = (byte)header.identifier[1];
            message_bytes[index++] = (byte)header.identifier[2];
            message_bytes[index++] = (byte)header.identifier[3];

            Array.Copy(BitConverter.GetBytes(header.protocol_version), 0, message_bytes, index, 2);
            index += 2;

            Array.Copy(BitConverter.GetBytes(header.packet_size), 0, message_bytes, index, 2);
            index += 2;

            Array.Clear(message_bytes, index, 4); // crc placeholder
            index += 4;

            Array.Copy(BitConverter.GetBytes(header.client_id), 0, message_bytes, index, 4);
            index += 4;

            Array.Copy(BitConverter.GetBytes(header.message_type), 0, message_bytes, index, 4);
        }

        private void WriteCrc(ref byte[] message_bytes)
        {
            uint crc_calculated = Crc32Algorithm.Compute(message_bytes);
            Array.Copy(BitConverter.GetBytes(crc_calculated), 0, message_bytes, 8, 4);
        }

        private void WriteControllerInformation(ref byte[] message_bytes, OutputControllerInformation info, uint index)
        {
            message_bytes[index++] = info.slot;
            message_bytes[index++] = info.slot_state;
            message_bytes[index++] = info.device_model;
            message_bytes[index++] = info.connection_type;

            Array.Copy(info.mac_address, 0, message_bytes, index, 6);
            index += 6;

            message_bytes[index++] = info.battery_status;
        }

        private void WriteControllerData(ref byte[] message_bytes, OutputControllerData data, uint index)
        {
            message_bytes[index++] = data.controller_connected;

            Array.Copy(BitConverter.GetBytes(data.packet_number), 0, message_bytes, index, 4);
            index += 4;

            message_bytes[index++] = data.button_data;
            message_bytes[index++] = data.more_button_data;
            message_bytes[index++] = data.share_button;
            message_bytes[index++] = data.ps_button;
            message_bytes[index++] = data.left_stick_x;
            message_bytes[index++] = data.left_stick_y;
            message_bytes[index++] = data.right_stick_x;
            message_bytes[index++] = data.right_stick_y;
            message_bytes[index++] = data.d_pad_left;
            message_bytes[index++] = data.d_pad_down;
            message_bytes[index++] = data.d_pad_right;
            message_bytes[index++] = data.d_pad_up;
            message_bytes[index++] = data.analog_y;
            message_bytes[index++] = data.analog_b;
            message_bytes[index++] = data.analog_a;
            message_bytes[index++] = data.analog_x;
            message_bytes[index++] = data.analog_r1;
            message_bytes[index++] = data.analog_l1;
            message_bytes[index++] = data.analog_r2;
            message_bytes[index++] = data.analog_l2;

            WriteControllerTouchData(ref message_bytes, data.first_touch, index);
            index += OutputControllerTouchData.size;

            WriteControllerTouchData(ref message_bytes, data.second_touch, index);
            index += OutputControllerTouchData.size;

            Array.Copy(BitConverter.GetBytes(data.motion_timestamp), 0, message_bytes, index, 8);
            index += 8;

            Array.Copy(BitConverter.GetBytes(data.accelerometer_x), 0, message_bytes, index, 4);
            index += 4;

            Array.Copy(BitConverter.GetBytes(data.accelerometer_y), 0, message_bytes, index, 4);
            index += 4;

            Array.Copy(BitConverter.GetBytes(data.accelerometer_z), 0, message_bytes, index, 4);
            index += 4;

            Array.Copy(BitConverter.GetBytes(data.gyro_pitch), 0, message_bytes, index, 4);
            index += 4;

            Array.Copy(BitConverter.GetBytes(data.gyro_yaw), 0, message_bytes, index, 4);
            index += 4;

            Array.Copy(BitConverter.GetBytes(data.gyro_roll), 0, message_bytes, index, 4);
            index += 4;
       }
        private void WriteControllerTouchData(ref byte[] message_bytes, OutputControllerTouchData data, uint index)
        {
            message_bytes[index++] = data.is_touch_active;
            message_bytes[index++] = data.touch_id;

            Array.Copy(BitConverter.GetBytes(data.x_pos), 0, message_bytes, index, 2);
            index += 2;

            Array.Copy(BitConverter.GetBytes(data.y_pos), 0, message_bytes, index, 2);
            index += 2;
        }

        private void ProcessMessage(EndPoint client_endpoint, Header header, byte[] message_bytes)
        {
            if (header.message_type == (uint)MessageType.VersionRequest)
            {
                SendVersionResponseMessage(client_endpoint);
            }
            else if (header.message_type == (uint)MessageType.PortRequest)
            {
                SendPortResponseMessage(client_endpoint);
            }
            else if (header.message_type == (uint)MessageType.PadDataRequest)
            {
                UpdateClientTime(client_endpoint);
            }
        }

        private void SendVersionResponseMessage(EndPoint client_endpoint)
        {
            ushort message_size = 2;
            byte[] output = new byte[Header.size + message_size];
            Header header = new Header();
            header.identifier = "DSUS";
            header.protocol_version = max_protocol_version_supported;
            header.packet_size = (ushort)(message_size + 4);  // Message type is not part of the header
            header.client_id = _server_id;
            header.message_type = (uint)MessageType.VersionResponse;
            WriteHeader(ref output, header);

            uint index = Header.size;
            Array.Copy(BitConverter.GetBytes(max_protocol_version_supported), 0, output, index, 2);
            index += 2;

            WriteCrc(ref output);

            try
            {
                _socket.SendTo(output, client_endpoint);
            }
            catch (SocketException)
            {
            }
        }

        private void SendPortResponseMessage(EndPoint client_endpoint)
        {
            ushort message_size = (ushort)(OutputControllerInformation.size + 1);
            byte[] output = new byte[Header.size + message_size];
            Header header = new Header();
            header.identifier = "DSUS";
            header.protocol_version = max_protocol_version_supported;
            header.packet_size = (ushort)(message_size + 4);    // Message type is not part of the header
            header.client_id = _server_id;
            header.message_type = (uint)MessageType.PortResponse;
            WriteHeader(ref output, header);

            uint index = Header.size;
            OutputControllerInformation controller_information = new OutputControllerInformation();
            controller_information.slot = 0;
            controller_information.slot_state = 2;
            controller_information.device_model = 2;
            controller_information.connection_type = 0;
            controller_information.mac_address = new byte[6];
            controller_information.battery_status = 0;
            WriteControllerInformation(ref output, controller_information, index);
            index += OutputControllerInformation.size;

            output[index] = (byte)'\0';

            WriteCrc(ref output);

            try
            {
                _socket.SendTo(output, client_endpoint);
            }
            catch (SocketException)
            {
            }
        }

        private void UpdateClientTime(EndPoint client_endpoint)
        {
            lock(_client_endpoint_to_client_data)
            {
                if (_client_endpoint_to_client_data.ContainsKey(client_endpoint))
                {
                    _client_endpoint_to_client_data[client_endpoint].last_access_time = DateTime.UtcNow;
                }
                else
                {
                    _client_endpoint_to_client_data.Add(client_endpoint, new ClientData());
                }
            }
        }

        public void SendOpenTrackData(OpenTrackData data)
        {
            List<Tuple<EndPoint, uint>> clients = new List<Tuple<EndPoint, uint>>();
            lock(_client_endpoint_to_client_data)
            {
                List<EndPoint> clients_to_remove = new List<EndPoint>();
                foreach (var pair in _client_endpoint_to_client_data)
                {
                    if ((DateTime.UtcNow - pair.Value.last_access_time).TotalSeconds > 5)
                    {
                        clients_to_remove.Add(pair.Key);
                    }
                    else
                    {
                        clients.Add(new Tuple<EndPoint, uint>(pair.Key, pair.Value.last_packet_count++));
                    }
                }

                foreach(var client in clients_to_remove)
                {
                    _client_endpoint_to_client_data.Remove(client);
                }
            }

            foreach (var client in clients)
            {
                SendPadDataResponse(client.Item1, client.Item2, data);
            }
        }

        private void SendPadDataResponse(EndPoint client_endpoint, uint packet_number, OpenTrackData data)
        {
            ushort message_size = (ushort)(OutputControllerInformation.size + OutputControllerData.size);
            byte[] output = new byte[Header.size + message_size];
            Header header = new Header();
            header.identifier = "DSUS";
            header.protocol_version = max_protocol_version_supported;
            header.packet_size = (ushort)(message_size + 4);  // Message type is not part of the header
            header.client_id = _server_id;
            header.message_type = (uint)MessageType.PadDataResponse;
            WriteHeader(ref output, header);

            uint index = Header.size;
            OutputControllerInformation controller_information = new OutputControllerInformation();
            controller_information.slot = 0;
            controller_information.slot_state = 2;
            controller_information.device_model = 2;
            controller_information.connection_type = 0;
            controller_information.mac_address = new byte[6];
            controller_information.battery_status = 0;
            WriteControllerInformation(ref output, controller_information, index);
            index += OutputControllerInformation.size;

            OutputControllerData controller_data = new OutputControllerData();
            controller_data.controller_connected = 1;
            controller_data.packet_number = packet_number;
            controller_data.button_data = 0;
            controller_data.more_button_data = 0;
            controller_data.share_button = 0;
            controller_data.ps_button = 0;
            controller_data.left_stick_x = 128;
            controller_data.left_stick_y = 128;
            controller_data.right_stick_x = 128;
            controller_data.right_stick_y = 128;
            controller_data.d_pad_left = 0;
            controller_data.d_pad_down = 0;
            controller_data.d_pad_right = 0;
            controller_data.d_pad_up = 0;
            controller_data.analog_y = 0;
            controller_data.analog_b = 0;
            controller_data.analog_a = 0;
            controller_data.analog_x = 0;
            controller_data.analog_r1 = 0;
            controller_data.analog_l1 = 0;
            controller_data.analog_r2 = 0;
            controller_data.analog_l2 = 0;

            controller_data.first_touch = new OutputControllerTouchData();
            controller_data.first_touch.is_touch_active = 0;
            controller_data.first_touch.touch_id = 0;
            controller_data.first_touch.x_pos = 0;
            controller_data.first_touch.y_pos = 0;

            controller_data.second_touch = new OutputControllerTouchData();
            controller_data.second_touch.is_touch_active = 0;
            controller_data.second_touch.touch_id = 0;
            controller_data.second_touch.x_pos = 0;
            controller_data.second_touch.y_pos = 0;

            DateTimeOffset date_time_offset = DateTime.UtcNow;
            controller_data.motion_timestamp = date_time_offset.ToUnixTimeMilliseconds() * 1000;
            controller_data.accelerometer_x = (float)data.x;
            controller_data.accelerometer_y = (float)data.y;
            controller_data.accelerometer_z = (float)data.z;
            controller_data.gyro_pitch = (float)data.pitch;
            controller_data.gyro_yaw = (float)data.yaw;
            controller_data.gyro_roll = (float)data.roll;
            WriteControllerData(ref output, controller_data, index);

            WriteCrc(ref output);

            try
            {
                _socket.SendTo(output, client_endpoint);
            }
            catch (SocketException)
            {
            }
        }

        public void Start()
        {
            Running = true;
            var r = new Random();
            _server_id = (uint)r.Next();

            _packet_receive_thread = new Thread(() =>
            {
                while (Running)
                {
                    ReceiveUDPPacket();

                    
                }
            });
            _packet_receive_thread.Start();
        }

        private void ReceiveUDPPacket()
        {
            if (_waiting_on_packet)
            {
                Thread.Sleep(1);
                return;
            }

            _waiting_on_packet = true;
            EndPoint client_endpoint = new IPEndPoint(IPAddress.Any, 0);

            // Ugly try..catch, seems to be "by design": https://stackoverflow.com/questions/4662553/how-to-abort-sockets-beginreceive
            try
            {
                _socket.BeginReceiveFrom(_packet_received_bytes, 0, _packet_received_bytes.Length, SocketFlags.None, ref client_endpoint, (ar) =>
                {
                    try
                    {
                        int message_size = _socket.EndReceiveFrom(ar, ref client_endpoint);

                        byte[] message_bytes = new byte[message_size];
                        Array.Copy(_packet_received_bytes, message_bytes, message_size);
                        _waiting_on_packet = false;

                        ProcessIncomingMessage(client_endpoint, message_bytes);
                    }
                    catch(SocketException)
                    {
                        uint IOC_IN = 0x80000000;
                        uint IOC_VENDOR = 0x18000000;
                        uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                        _socket.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
                    }
                    catch
                    {
                    }
                }, null);
            }
            catch
            {
            }
        }

        public void Stop()
        {
            Running = false;
            _packet_receive_thread.Join();
            _socket.Close();
        }
    }
}
