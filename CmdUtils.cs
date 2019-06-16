using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Threading;

namespace BaiduPCS_service_win
{
    public class CmdUtils
    {
        public String shell = "";
        Process cmd = new Process();//创建进程对象  
        ProcessStartInfo startInfo = new ProcessStartInfo();
        public void sendCmd(Form1 cmdoom)
        {
            startInfo.FileName = "cmd.exe";//设定需要执行的命令  
            startInfo.Arguments = "";//“/C”表示执行完命令后马上退出  
            startInfo.UseShellExecute = false;//不使用系统外壳程序启动  
            startInfo.RedirectStandardInput = true;//不重定向输入  
            startInfo.RedirectStandardOutput = true; //重定向输出  
            startInfo.CreateNoWindow = true;//不创建窗口  
            cmd.StartInfo = startInfo;
            if (cmd.Start())//开始进程  
            {
                cmd.StandardOutput.ReadLine().Trim();
                cmd.StandardOutput.ReadLine().Trim();
                while (cmdoom.isRun.IndexOf("start") != -1)
                {
                    if (shell.Length > 0)
                    {
                        cmd.StandardInput.WriteLine(shell);
                        cmd.StandardOutput.ReadLine().Trim();

                        cmd.StandardInput.WriteLine("\n");
                        String log = cmd.StandardOutput.ReadLine().Trim();
                        String path = log.Substring(0, 2).ToUpper();
                        updateLog(cmdoom, log);
                        log = "";
                        do
                        {
                            String logm = cmd.StandardOutput.ReadLine().Trim();
                            if (logm.IndexOf(path) != -1)
                            {
                                break;
                            }
                            updateLog(cmdoom, logm + "\n");
                            log += logm;

                        } while (true);

                        shell = "";
                    }
                    Thread.Sleep(1);
                }

                cmd.Close();

                cmd = null;
                return;
            }
            return;
        }
        private delegate void UpdateLog();

        private void updateLog(Form1 cmd, String log)
        {
            UpdateLog set = delegate ()
            {
                cmd.cmdLogTextArea.AppendText(log+"\n");
            };
            cmd.Invoke(set);
        }
    }
}
