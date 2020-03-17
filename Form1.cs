using System;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.ServiceProcess;
using System.Diagnostics;
using Microsoft.Win32;

namespace BaiduPCS_service_win
{
    public partial class Form1 : Form
    {
        #region -- Setting --

        const string servicename = "BaiduPCS-Go";
        const string bat_directory = @"/script/createservice.bat";
        const string baidupcs_directory = @"/script/BaiduPCS-Go.exe";
        const string recreate_log = "-- Recreate Action --";
        const int refresh_delay_ms = 2000;
        static string startup_path = Application.StartupPath.Replace("\\", "/");
        public bool isRecreate = false;
        IniFiles ini = new IniFiles(startup_path + @"/Config.ini");

        #endregion

        #region -- Form --

        public Form1()
        {
            InitializeComponent();
            new Thread(new ThreadStart(cmdinit)).Start();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            System.Environment.Exit(0);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!File.Exists(startup_path + @"/script/BaiduPCS-Go.exe"))
            {
                DialogResult dr = MessageBox.Show("尚未检测到BaiduPCS-Go.exe，是否前往下载？\n（下载后放入script文件夹下）", "提示", MessageBoxButtons.OKCancel);
                if (dr == DialogResult.OK) System.Diagnostics.Process.Start("https://github.com/liuzhuoling2011/baidupcs-web/releases");
                System.Environment.Exit(0);
            }
            CheckService();
            if (!ini.ExistINIFile())
            {
                FileStream fs = new FileStream("Config.ini", FileMode.Create, FileAccess.Write);//创建写入文件 
                fs.Close();
                ini.IniWriteValue("App Config", "AutoStart", "True");
                ini.IniWriteValue("App Config", "Directory", startup_path);
                checkBox1.Checked = true;
            }
            else
            {
                checkBox1.Checked = Convert.ToBoolean(ini.IniReadValue("App Config", "AutoStart"));
                if (ini.IniReadValue("App Config", "Directory") != startup_path) ReCreateService();
            }
        }

        #endregion

        #region -- CMD --

        public String isRun = "start";
        CmdUtils cmd = new CmdUtils();

        private void cmdinit()
        {
            cmd.sendCmd(this);
        }

        private void RunCmdCommand(string command)
        {
            cmd.shell = command;
        }

        private string RunBat(string batPath)
        {
            Process pro = new Process();
            FileInfo file = new FileInfo(batPath);
            pro.StartInfo.WorkingDirectory = file.Directory.FullName;
            pro.StartInfo.FileName = batPath;
            pro.StartInfo.Arguments = "";//“/C”表示执行完命令后马上退出  
            pro.StartInfo.UseShellExecute = false;//不使用系统外壳程序启动  
            pro.StartInfo.RedirectStandardInput = true;//不重定向输入  
            pro.StartInfo.RedirectStandardOutput = true; //重定向输出  
            pro.StartInfo.RedirectStandardError = true;
            pro.StartInfo.CreateNoWindow = true;//不创建窗口  
            pro.Start();
            pro.StandardInput.WriteLine("ping www.baidu.com");
            pro.StandardInput.AutoFlush = true;
            StreamReader reader = pro.StandardOutput;//截取输出流
            StreamReader error = pro.StandardError;//截取错误信息
            string str = reader.ReadToEnd() + error.ReadToEnd();
            pro.WaitForExit();//等待程序执行完退出进程
            pro.Close();
            return str;
        }

        #endregion

        #region -- Error Display --

        private void SumbitLog()
        {
            if (cmdLogTextArea.Text != null)
            {
                string logtxt = cmdLogTextArea.Text;
                Clipboard.SetText(logtxt);
                MessageBox.Show("已复制日志到剪贴板");
                string date = DateTime.Now.ToLocalTime().ToString().Replace("/", "-").Replace(":","-");
                string path = startup_path + @"/errorlog/";
                string filename = date + ".txt";
                if(!Directory.Exists(startup_path + @"/errorlog")) System.IO.Directory.CreateDirectory(path);
                FileStream fs = new FileStream(path + filename, FileMode.Create);
                StreamWriter sw = new StreamWriter(fs);
                sw.WriteLine(logtxt.Replace("\n","\r\n"));
                sw.Flush(); sw.Close(); fs.Close();
                System.Diagnostics.Process.Start("notepad.exe", path + filename);
            }
            System.Diagnostics.Process.Start("https://github.com/JamesHoi/BaiduPCS-service-win/issues");
        }

        private void DisplayError()
        {
            DialogResult dr = MessageBox.Show("操作失败，是否反馈？", "提示", MessageBoxButtons.OKCancel);
            if (dr == DialogResult.OK) SumbitLog();
        }

        private void DisplayCreateError()
        {
            DialogResult dr = MessageBox.Show("注册失败，是否尝试方法二？", "提示", MessageBoxButtons.OKCancel);
            if (dr == DialogResult.OK) CreateService(2);
        }

        #endregion

        #region -- Service --

        public static bool IsServiceInstalled(string serviceName)
        {
            // get list of Windows services
            ServiceController[] services = ServiceController.GetServices();
            // try to find service name
            foreach (ServiceController service in services)
            {
                if (service.ServiceName == serviceName)return true;
            }
            return false;
        }

        public static bool IsServiceRunning(string serviceName)
        {
            // get list of Windows services
            ServiceController[] services = ServiceController.GetServices();
            // try to find service name
            foreach (ServiceController service in services)
            {
                if (service.ServiceName == serviceName)
                {
                    return service.Status == ServiceControllerStatus.Running;
                }
            }
            return false;
        }

        private void CreateService(int solution)
        {
            FileStream fs = new FileStream(startup_path + bat_directory, FileMode.Create);
            StreamWriter sw = new StreamWriter(fs);
            string createcommand = "";
            string path = "\"".Insert(1, startup_path);
            switch (solution)
            {
                case 1:
                    createcommand = "sc create " + servicename + " binpath=" + path + "/script/srvany.exe\"";
                    break;
                case 2:
                    createcommand = path + "/script/instsrv.exe\" " + servicename + " " + path + "/script/srvany.exe\"";
                    break;
            }
            sw.WriteLine(createcommand);
            sw.Flush(); sw.Close(); fs.Close();
            string output = RunBat(startup_path + bat_directory);

            //check service if it is installed
            Thread.Sleep(refresh_delay_ms);
            CheckService();
            if (isRecreate) output = recreate_log + output;
            cmdLogTextArea.AppendText(output+"\n");
            if (IsServiceInstalled(servicename) != true)
            {
                if (solution == 2) DisplayError();
                else DisplayCreateError();
                return;
            }

            //create regedit
            try
            {
                RegistryKey key = Registry.LocalMachine;
                string directory = @"System\CurrentControlSet\Services\" + servicename + @"\Parameters";
                RegistryKey service = key.CreateSubKey(directory);
                service.SetValue("Application", startup_path + baidupcs_directory);
                service.SetValue("AppDirectory", startup_path + @"\script");
                service.SetValue("AppParameters", "");
                service.Close();
                key.Close();
            }catch(Exception e)
            {
                MessageBox.Show("注册表修改失败！请确保已关闭所有安全软件", "提示");
                DeleteService(0);
                CheckService();
            }
        }

        private void ReCreateService()
        {
            MessageBox.Show("检测到软件目录变更，自动重新注册服务", "提示");
            isRecreate = true;
            DeleteService(1);
            CreateService(1);
            ini.IniWriteValue("App Config", "Directory", startup_path);
        }

        private void DeleteService(int mode)
        {
            if (IsServiceRunning(servicename)&&mode!=1)
            {
                MessageBox.Show("请先关闭服务后再卸载", "提示");
                return;
            }
            RunCmdCommand("sc delete " + servicename);
            Thread.Sleep(refresh_delay_ms);
            CheckService();
            if (IsServiceInstalled(servicename) != false) DisplayError();
        }
        
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            //autostart
            if (!checkBox1.Enabled) return;
            bool isautostart = checkBox1.Checked;
            ini.IniWriteValue("App Config", "AutoStart", isautostart.ToString());
            string status = "";
            if (isautostart) status = "auto";
            else status = "demand";
            RunCmdCommand("sc config " + servicename +" start= "+ status);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //button1.Enabled = false;
            bool isinstalled = IsServiceInstalled(servicename);
            if (isinstalled) DeleteService(0);
            else
            {
                CreateService(1);
                checkBox1.Checked = true;
            }
            //Application.DoEvents();
            //button1.Enabled = true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            button2.Enabled = false;
            string action = "";
            bool isrunning = IsServiceRunning(servicename);
            if (isrunning) action = "stop";
            else action = "start";
            RunCmdCommand("sc " + action + " " + servicename);
            Thread.Sleep(refresh_delay_ms);
            if (isrunning == IsServiceRunning(servicename))
            {
                DisplayError();
            }
            CheckServiceRunning();
            Application.DoEvents();
            button2.Enabled = true;
        }

        private void CheckService()
        {
            if (IsServiceInstalled(servicename))
            {
                button1.Text = "卸载服务";
                button2.Enabled = true;
                checkBox1.Enabled = true;
                CheckServiceRunning();
            }
            else
            {
                button1.Text = "注册服务";
                button2.Enabled = false;
                button3.Enabled = false;
                checkBox1.Enabled = false;
            }
        }

        private void CheckServiceRunning()
        {
            if (IsServiceRunning(servicename))
            {
                button2.Text = "关闭服务";
                button3.Enabled = true;
            }
            else
            {
                button2.Text = "开启服务";
                button3.Enabled = false;
            }
        }

        #endregion

        #region -- Website --

        private void GithubToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/JamesHoi/BaiduPCS-service-win");
        }

        private void IssueToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SumbitLog();
        }

        private void FaqStripMenuItem1_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/JamesHoi/BaiduPCS-service-win#faq");
        }

        private void button3_Click(object sender, EventArgs e)
        {
            MessageBox.Show("将网页添加进收藏夹会更方便哟", "提示");
            System.Diagnostics.Process.Start("http://localhost:5299");
        }

        #endregion
    }
}
