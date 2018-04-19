using canTransport;
using Dongzr.MidiLite;
using SecurityAccess;
using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Uds;
using System.Threading;

namespace TPMS_Receptance
{
    public partial class Form1 : Form
    {

        can_driver driver = new can_driver();
        canTrans driverTrans = new canTrans();
        SecurityKey securityDriver = new SecurityKey();

        public int id;
        byte[] data = new byte[8];
        byte[] FL_id = new byte[4];
        byte[] FR_id = new byte[4];
        byte[] RL_id = new byte[4];
        byte[] RR_id = new byte[4];

        long FL_first_time;
        long FL_last_time;
        long FR_first_time;
        long FR_last_time;
        long RL_first_time;
        long RL_last_time;
        long RR_first_time;
        long RR_last_time;

        int[] frame = new int[4];//0,1,2,3--FL,FR,RLR,RR
        int[] package = new int[4];
        float[] frame_per = new float[4];
        float[] package_per = new float[4];

        int interval_up;
        int interval_down;

        int dlc;
        long timestamp;
        bool rx_success;

        int Startflag;

        public Form1()
        {
            InitializeComponent();
            BusParamsInit();
            mmTime_init();
        }

        private void BusParamsInit()
        {
            string[] channel = new string[0];
            channel = driver.GetChannel();
            comboBoxCanDevice.Items.Clear();
            comboBoxCanDevice.Items.AddRange(channel);//add items for comboBox
            comboBoxCanDevice.SelectedIndex = 0;//default select the first , physical driver always come first
            comboBoxCanBaudRate.SelectedIndex = 4;//default select 500K                                   
        }

        private void BusButton_Click(object sender, EventArgs e)
        {
            if (BusButton.Text == "Bus On")//bus on和 bus off 对整个应用程序（包括子窗体）有绝对的控制功能
            {
                if (driver.OpenChannel(comboBoxCanDevice.SelectedIndex, comboBoxCanBaudRate.Text) == true)
                {
                    BusButton.Text = "Bus Off";
                    driverTrans.Start();
                    mmTimer.Start();
                    t_Start();
                    comboBoxCanDevice.Enabled = false;
                    comboBoxCanBaudRate.Enabled = false;
                    textBoxFL.Enabled = true;
                    textBoxFR.Enabled = true;
                    textBoxRL.Enabled = true;
                    textBoxRR.Enabled = true;
                }
                else
                {
                    MessageBox.Show("打开" + comboBoxCanDevice.Text + "通道失败!");
                }
            }
            else
            {
                driver.CloseChannel();
                BusButton.Text = "Bus On";
                driverTrans.Stop();
                mmTimer.Stop();
                t_Stop();
                comboBoxCanDevice.Enabled = true;
                comboBoxCanBaudRate.Enabled = true;
                StartButton.Text = "Start";
                Startflag = 0;
                textBoxFL.Enabled = true;
                textBoxFR.Enabled = true;
                textBoxRL.Enabled = true;
                textBoxRR.Enabled = true;
                intervalUp.Enabled = true;
                intervalDown.Enabled = true;
            }
        }

        private void StartButton_Click(object sender, EventArgs e)
        {

            if ((StartButton.Text == "Start") && (BusButton.Text == "Bus Off"))
            {
                StartButton.Text = "Stop";
                Startflag = 1;
                textBoxFL.Enabled = false;
                textBoxFR.Enabled = false;
                textBoxRL.Enabled = false;
                textBoxRR.Enabled = false;
                intervalUp.Enabled = false ;
                intervalDown.Enabled = false ;

                /*textbox清屏*/
                textBoxFLF.Clear();
                textBoxFLFper.Clear();
                textBoxFLP.Clear();
                textBoxFLPper.Clear();
                textBoxFRF.Clear();
                textBoxFRFper.Clear();
                textBoxFRP.Clear();
                textBoxFRPper.Clear();
                textBoxRLF.Clear();
                textBoxRLFper.Clear();
                textBoxRLP.Clear();
                textBoxRLPper.Clear();
                textBoxRRF.Clear();
                textBoxRRFper.Clear();
                textBoxRRP.Clear();
                textBoxRRPper.Clear();
                richTextBoxDisplay.Clear();

                /*匹配ID*/
                if (Regex.IsMatch(textBoxFL.Text, @"^\w{8}$"))//必须是8位字符
                {
                    FL_id = StringToHex(textBoxFL.Text);
                }
                if (Regex.IsMatch(textBoxFR.Text, @"^\w{8}$"))
                {
                    FR_id = StringToHex(textBoxFR.Text);
                }
                if (Regex.IsMatch(textBoxRL.Text, @"^\w{8}$"))
                {
                    RL_id = StringToHex(textBoxRL.Text);
                }
                if (Regex.IsMatch(textBoxRR.Text, @"^\w{8}$"))
                {
                    RR_id = StringToHex(textBoxRR.Text);
                }

                if (Regex.IsMatch(intervalUp.Text, @"\d+"))
                {
                    interval_up = Convert.ToInt32(float.Parse(intervalUp.Text) );//取包时间间隔上限
                }
                if (Regex.IsMatch(intervalDown.Text, @"\d+"))
                {
                    interval_down = Convert.ToInt32(float.Parse(intervalDown.Text) );//取包时间间隔下限
                }

                byte[] dat = { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x03 };
                long time;
                driver.WriteData(0x31A, dat, 8, out time);//发0x31A
            }
            else
            {
                StartButton.Text = "Start";
                Startflag = 0;
                textBoxFL.Enabled = true;
                textBoxFR.Enabled = true;
                textBoxRL.Enabled = true;
                textBoxRR.Enabled = true;
                intervalUp.Enabled = true;
                intervalDown.Enabled = true;

                /*ID初始化为00000000，防止错误帧FFFFFFFF干扰，计数器清0*/
                FL_id = StringToHex("00000000");
                FR_id = StringToHex("00000000");
                RL_id = StringToHex("00000000");
                RR_id = StringToHex("00000000");

                FL_first_time = 0;
                FL_last_time = 0;
                FR_first_time = 0;
                FR_last_time = 0;
                RL_first_time = 0;
                RL_last_time = 0;
                RR_first_time = 0;
                RR_last_time = 0;

                for (int i = 0; i < 4; i++)//0,1,2,3--FL,FR,RLR,RR
                {
                    frame[i] = 0;
                }
                for (int i = 0; i < 4; i++)
                {
                    package[i] = 0;
                }
                for (int i = 0; i < 4; i++)
                {
                    frame_per[i] = 0;
                }
                for (int i = 0; i < 4; i++)
                {
                    package_per[i] = 0;
                }
            }
        }

        #region Timer
        public delegate void Tick_10ms();
        public delegate void Tick_50ms();
        public delegate void Tick_100ms();
        public delegate void Tick_1s();
        public Tick_10ms mmtimer_tick_10ms;
        public Tick_10ms mmtimer_tick_50ms;
        public Tick_100ms mmtimer_tick_100ms;
        public Tick_1s mmtimer_tick_1s;
        public MmTimer mmTimer;
        const int timer_interval = 10;
        int timer_10ms_counter = 0;
        int timer_50ms_counter = 0;
        int timer_100ms_counter = 0;
        int timer_1s_counter = 0;

        private void mmTime_init()
        {
            mmTimer = new MmTimer();
            mmTimer.Mode = MmTimerMode.Periodic;
            mmTimer.Interval = timer_interval;
            mmTimer.Tick += mmTimer_tick;

            mmtimer_tick_10ms += delegate
            {

            };

            mmtimer_tick_50ms += delegate
            {

            };

            mmtimer_tick_100ms += delegate
            {

            };

            mmtimer_tick_1s += delegate
            {
                EventHandler BusLoadUpdate = delegate
                {
                    BusLoad.Text = "Bus Load：" + driver.BusLoad().ToString() + "% ";
                };
                try { Invoke(BusLoadUpdate); } catch { };
            };
        }

        void mmTimer_tick(object sender, EventArgs e)
        {
            timer_10ms_counter += timer_interval;
            if (timer_10ms_counter >= 10)
            {
                timer_10ms_counter = 0;
                if (mmtimer_tick_10ms != null)
                {
                    mmtimer_tick_10ms();
                }
            }

            timer_50ms_counter += timer_interval;
            if (timer_50ms_counter >= 50)
            {
                timer_50ms_counter = 0;
                if (mmtimer_tick_10ms != null)
                {
                    mmtimer_tick_50ms();
                }
            }

            timer_100ms_counter += timer_interval;
            if (timer_100ms_counter >= 100)
            {
                timer_100ms_counter = 0;
                if (mmtimer_tick_100ms != null)
                {
                    mmtimer_tick_100ms();
                }
            }

            timer_1s_counter += timer_interval;
            if (timer_1s_counter >= 1000)
            {
                timer_1s_counter = 0;
                if (mmtimer_tick_1s != null)
                {
                    mmtimer_tick_1s();
                }
            }
        }
        #endregion

        #region thread t_Receive
        Thread t_Receive;
        private void t_Receive_Thread()
        {
            while (true)
            {
                int i = 0;
                while (i < 50)
                {
                    CycleRecieve();
                    i++;
                }
                t_Sleep(20);//休息10ms
            }
        }

        private void CycleRecieve()
        {
            rx_success = driver.ReadData(out id, ref data, out dlc, out timestamp);//接收一帧数据
            if (Startflag == 1)//从这里读取的Buff数据才是有效的
            {
                if ((rx_success) && (id == 0x003)&&(dlc==8))
                {
                    EventHandler Display = delegate
                    {
                        richTextBoxDisplay.AppendText(" $" + id.ToString("X3") + ": " + dlc.ToString() + "  " + HexToStrings(data, " ") + " " + (timestamp / 1000).ToString() + "." + (timestamp % 1000).ToString("000") + "\r\n");
                        richTextBoxDisplay.ScrollToCaret();
                    };
                    try { Invoke(Display); } catch { };

                    /*FL*/
                    if ((data[0] + data[1] + data[2] + data[3]) == (FL_id[0] + FL_id[1] + FL_id[2] + FL_id[3]))
                    {
                        if (FL_first_time == 0)//首包/帧
                        {
                            EventHandler FL1 = delegate
                            {
                                textBoxFLP.Text = (++package[0]).ToString();
                                textBoxFLF.Text = (++frame[0]).ToString();
                            };
                            try { Invoke(FL1); } catch { };
                            FL_first_time = timestamp;
                        }
                        else
                        {
                            FL_last_time = timestamp;
                            if (((FL_last_time - FL_first_time) > interval_down) && ((FL_last_time - FL_first_time) < interval_up))
                            {
                                EventHandler FL2 = delegate
                                {
                                    textBoxFLP.Text = (++package[0]).ToString();//包+1
                                };
                                try { Invoke(FL2); } catch { };
                            }
                            EventHandler FL3 = delegate
                            {
                                textBoxFLF.Text = (++frame[0]).ToString();//帧+1
                            };
                            try { Invoke(FL3); } catch { };
                            FL_first_time = FL_last_time;//本次时间变成上一次时间
                        }
                        EventHandler FL4 = delegate
                        {
                            textBoxFLFper.Text = ((float)frame[0] / package[0] / 4 * 100).ToString("0.00");
                            textBoxFLPper.Text = (package[0] / package[0] * 100).ToString("0.00");
                        };
                        try { Invoke(FL4); } catch { };
                    }
                    /*FR*/
                    else if ((data[0] + data[1] + data[2] + data[3]) == (FR_id[0] + FR_id[1] + FR_id[2] + FR_id[3]))
                    {
                        if (FR_first_time == 0)
                        {
                            EventHandler FR1 = delegate
                            {
                                textBoxFRP.Text = (++package[1]).ToString();
                                textBoxFRF.Text = (++frame[1]).ToString();
                            };
                            try { Invoke(FR1); } catch { };
                            FR_first_time = timestamp;
                        }
                        else
                        {
                            FR_last_time = timestamp;
                            if (((FR_last_time - FR_first_time) > interval_down) && ((FR_last_time - FR_first_time) < interval_up))
                            {
                                EventHandler FR2 = delegate
                                {
                                    textBoxFRP.Text = (++package[1]).ToString();
                                };
                                try { Invoke(FR2); } catch { };
                            }
                            EventHandler FR3 = delegate
                            {
                                textBoxFRF.Text = (++frame[1]).ToString();
                            };
                            try { Invoke(FR3); } catch { }
                            FR_first_time = FR_last_time;
                        }
                        EventHandler FR4 = delegate
                        {
                            textBoxFRFper.Text = ((float)frame[1] / package[1] / 4 * 100).ToString("0.00");
                            textBoxFRPper.Text = (package[1] / package[1] * 100).ToString("0.00");
                        };
                        try { Invoke(FR4); } catch { }
                    }
                    /*RL*/
                    else if ((data[0] + data[1] + data[2] + data[3]) == (RL_id[0] + RL_id[1] + RL_id[2] + RL_id[3]))
                    {
                        if (RL_first_time == 0)
                        {
                            EventHandler RL1 = delegate
                            {
                                textBoxRLP.Text = (++package[2]).ToString();
                                textBoxRLF.Text = (++frame[2]).ToString();
                            };
                            try { Invoke(RL1); } catch { };
                            RL_first_time = timestamp;
                        }
                        else
                        {
                            RL_last_time = timestamp;
                            if (((RL_last_time - RL_first_time) > interval_down) && ((RL_last_time - RL_first_time) < interval_up))
                            {
                                EventHandler RL2 = delegate
                                {
                                    textBoxRLP.Text = (++package[2]).ToString();
                                };
                                try { Invoke(RL2); } catch { };
                            }
                            EventHandler RL3 = delegate
                            {
                                textBoxRLF.Text = (++frame[2]).ToString();
                            };
                            try { Invoke(RL3); } catch { };
                            RL_first_time = RL_last_time;
                        }
                        EventHandler RL4 = delegate
                        {
                            textBoxRLFper.Text = ((float)frame[2] / package[2] / 4 * 100).ToString("0.00");
                            textBoxRLPper.Text = (package[2] / package[2] * 100).ToString("0.00");
                        };
                        try { Invoke(RL4); } catch { };
                    }
                    /*RR*/
                    else if ((data[0] + data[1] + data[2] + data[3]) == (RR_id[0] + RR_id[1] + RR_id[2] + RR_id[3]))
                    {
                        if (RR_first_time == 0)
                        {
                            EventHandler RR1 = delegate
                            {
                                textBoxRRP.Text = (++package[3]).ToString();
                                textBoxRRF.Text = (++frame[3]).ToString();
                            };
                            try { Invoke(RR1); } catch { };
                            RR_first_time = timestamp;
                        }
                        else
                        {
                            RR_last_time = timestamp;
                            if (((RR_last_time - RR_first_time) > interval_down) && ((RR_last_time - RR_first_time) < interval_up))
                            {
                                EventHandler RR2 = delegate
                                {
                                    textBoxRRP.Text = (++package[3]).ToString();
                                };
                                try { Invoke(RR2); } catch { };
                            }
                            EventHandler RR3 = delegate
                            {
                                textBoxRRF.Text = (++frame[3]).ToString();
                            };
                            try { Invoke(RR3); } catch { };
                            RR_first_time = RR_last_time;
                        }
                        EventHandler RR4 = delegate
                        {
                            textBoxRRFper.Text = ((float)frame[3] / package[3] / 4 * 100).ToString("0.00");
                            textBoxRRPper.Text = (package[3] / package[3] * 100).ToString("0.00");
                        };
                        try { Invoke(RR4); } catch { };
                    }
                }
            }
        }

        public void t_Start()
        {
            t_Receive = new Thread(new ThreadStart(t_Receive_Thread));
            t_Receive.IsBackground = true;
            t_Receive.Priority = ThreadPriority.Lowest;
            t_Receive.Start();
        }
        public void t_Stop()
        {
            if (t_Receive != null && t_Receive.IsAlive)
            {
                t_Receive.Abort();
            }
        }
        public void t_Sleep(int timespan)
        {
            if (t_Receive != null && t_Receive.IsAlive)
            {
                Thread.Sleep(timespan);
            }
        }
        #endregion

        /*将十六进制数组转换成十六进制字符串，并以space隔开*/
        public string HexToStrings(byte[] hex, string space)
        {
            string strings = "";
            for (int i = 0; i < hex.Length; i++)//逐字节变为16进制字符，并以space隔开
            {
                strings += hex[i].ToString("X2") + space;
            }
            return strings;
        }

        /*将十六进制字符串转换成十六进制数组（不足末尾补0），失败返回空数组*/
        byte[] StringToHex(string strings)
        {
            byte[] hex = new byte[0];
            try
            {
                strings = strings.Replace("0x", "");
                strings = strings.Replace("0X", "");
                strings = strings.Replace(" ", "");
                strings = Regex.Replace(strings, @"(?i)[^a-f\d\s]+", "");//表示不可变正则表达式
                if (strings.Length % 2 != 0)
                {
                    strings += "0";
                }
                hex = new byte[strings.Length / 2];
                for (int i = 0; i < hex.Length; i++)
                {
                    hex[i] = Convert.ToByte(strings.Substring(i * 2, 2), 16);
                }
                return hex;
            }
            catch
            {
                return hex;
            }
        }

        private void coverToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(richTextBoxDisplay.Text);
        }

        private void clearToolStripMenuItem_Click(object sender, EventArgs e)
        {
            richTextBoxDisplay.Clear();
        }
    }
}
