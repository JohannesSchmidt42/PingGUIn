using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Threading;
using System.Net.Sockets;

namespace PingGUIn
{
    public partial class PingGUI : Form
    {
        private bool _isClosing;
        
        public PingGUI()
        {
            InitializeComponent();
            this.FormClosing += PingGUI_FormClosing;
            this.ActiveControl = richTextBoxIp;
        }        

        #region form setup
        void PingGUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            _isClosing = true;
        }
        #endregion

        private void PingHostForever(string hostNameOrIp, ListViewItem item)
        {
            var thread = new Thread(()=>
            {
                using(var pinger = new Ping())
                {
                    bool triedGetName = false;
                    while((item.ListView != null) && (_isClosing == false))
                    {
                        PingReply reply = null;
                        try
                        {
                            reply = pinger.Send(hostNameOrIp);
                        }
                        catch 
                        {
                            
                        }

                        if(reply != null && reply.Status == IPStatus.Success)
                        {
                            string text;
                            if(reply.RoundtripTime == 0)
                            { 
                                text = "< 1 ms";
                            }
                            else
                            {
                                text = reply.RoundtripTime.ToString() + " ms";    
                            }

                            BeginInvoke(new Action(()=> item.SubItems[1].Text = text));

                            if(!triedGetName)
                            {
                                triedGetName = true;
                                try
                                {
                                    var host = Dns.GetHostEntry(reply.Address);
                                    BeginInvoke(new Action(() => item.SubItems[2].Text = "Hostname = " + host.HostName));
                                }
                                catch { }
                            }

                            if(reply.RoundtripTime < 1000)
                            { 
                                Thread.Sleep((int)((long)1000 - reply.RoundtripTime));
                            }

                            
                        }
                        else
                        {
                            BeginInvoke(new Action(()=> item.SubItems[1].Text = "n/a"));
                            Thread.Sleep(1000);
                        }
                    }
                }
            });
            thread.Start();
        }

        #region User input
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            if(richTextBoxIp.Text != string.Empty)
            {
                var item = new ListViewItem(richTextBoxIp.Text);
                item.SubItems.Add(new ListViewItem.ListViewSubItem(item, "n/a"));
                item.SubItems.Add(new ListViewItem.ListViewSubItem(item, ""));
                listView.Items.Add(item);
                PingHostForever(richTextBoxIp.Text,item);

                richTextBoxIp.Text = string.Empty;
                this.ActiveControl = richTextBoxIp;
            }           
        }

        private void buttonRemove_Click(object sender, EventArgs e)
        {
            if(listView.SelectedIndices.Count > 0)
            { 
                var item = listView.Items[listView.SelectedIndices[0]];                
                listView.Items.RemoveAt(listView.SelectedIndices[0]);
            }
        }

   
        #endregion

      

        private void richTextBoxIp_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Enter)
            { 
                buttonAdd_Click(sender,EventArgs.Empty);
            }
        }

        private void buttonScanSubnet_Click(object sender, EventArgs e)
        {
            IPAddress localAdress;
            IPAddress subnetMask;
            var success = GetLocalSubnetMask(out localAdress, out subnetMask);
            if(success)
            {
                listView.Items.Clear();
                StartScan(localAdress,subnetMask);
            }
        } 

        private bool GetLocalSubnetMask(out IPAddress localAdress, out IPAddress subnetMask)
        {
            localAdress = null;
            subnetMask = null;

            try
            {
                NetworkInterface defaultInterface = null;                
                localAdress = null;
                subnetMask = null;

                foreach(NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if(nic.OperationalStatus == OperationalStatus.Up)
                        if((nic.Speed == 100000000) || (nic.Speed == 1000000000))
                            if(nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                            {
                                defaultInterface = nic;
                                break;
                            }
                }

                if(defaultInterface != null)
                {
                    foreach(var unicastIPAddressInformation in defaultInterface.GetIPProperties().UnicastAddresses)
                    {
                        if(unicastIPAddressInformation.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            localAdress = unicastIPAddressInformation.Address;
                            subnetMask = unicastIPAddressInformation.IPv4Mask;
                            break;
                        }
                    }
                }
                else return false;

                if(subnetMask != null)
                { 
                    return true;
                }
                else return false;
            }
            catch
            {
                return false;
            }
        }

        private void StartScan(IPAddress localAdress, IPAddress subnetMask)
        {
            var addressBytes = localAdress.GetAddressBytes();
            var maskBytes = subnetMask.GetAddressBytes();
            var address = new byte[4];

            uint hostCount = (uint)((byte)~maskBytes[0] << 24 | (byte)~maskBytes[1] << 16 | (byte)~maskBytes[2] << 8 | (byte)~maskBytes[3] << 0);
            
            for(int i = 0; i < address.Length; i++)
            address[i] = (byte)(addressBytes[i] & maskBytes[i]);            

            for(int i = 1; i < hostCount - 1; i++)
            {                
                var adr = (byte[])address.Clone();

                var ip = "";

                for(int x = 0; x < adr.Length; x++)
                {
                    var mask = (byte)(i >> (24 - x * 8));
                    adr[x] = (byte)(adr[x] | mask);
                    ip += adr[x];
                    if(x + 1 < adr.Length)
                    {
                        ip += ".";
                    }
                }
                
                try
                {
                    var currentIp = IPAddress.Parse(ip);
                    var thread = new Thread(() =>                    
                    {
                        try
                        {
                            using(var pinger = new Ping())
                            {
                                var reply = pinger.Send(currentIp,4000);
                                if(reply.Status == IPStatus.Success)
                                {
                                    var item = new ListViewItem(currentIp.ToString());
                                    item.SubItems.Add(new ListViewItem.ListViewSubItem(item, "" + reply.RoundtripTime));
                                    item.SubItems.Add(new ListViewItem.ListViewSubItem(item, ""));
                                    Invoke(new Action(()=> listView.Items.Add(item)));
                                    PingHostForever(currentIp.ToString(), item);
                                }
                            }
                        }
                        catch
                        {

                        }
                    });
                    thread.Start();
                    
                }
                catch 
                {
                
                }
                
            }
            MessageBox.Show("Test");
        }

    }
}
