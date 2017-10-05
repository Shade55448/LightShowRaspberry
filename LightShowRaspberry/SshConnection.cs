﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Renci.SshNet;
using System.IO;
using System.Threading;

namespace LightShowRaspberry
{
    class SshConnection
    {
        public bool IsConnected { get; private set; } = false;
        SftpClient ftpClient;
        SshClient sshClient;
        ShellStream shellStream;

        public SshConnection(string host, string username, string password)
        {
            ftpClient = new SftpClient(host, username, password);
            sshClient = new SshClient(host, username, password);
        }

        public bool Connect()
        {
            try
            {
                ftpClient.Connect();
                sshClient.Connect();

                ftpClient.ChangeDirectory("/home/pi/Music");
                IsConnected = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public IEnumerable<string> GetSongList()
        {
            var fileList = ftpClient.ListDirectory(ftpClient.WorkingDirectory).Where(x => x.Name != ".." && x.Name != ".").Select(s => s.Name);
            return fileList;
        }
        public int GetVolume()
        {
            var response = sshClient.RunCommand("awk -F\"[][]\" '/dB/ { print $2 }' <(amixer sget PCM)");
            string result = response.Result;
            result = result.Remove(result.Length - 2);
            return Convert.ToInt32(result);
        }

        public void Upload(string[] files)
        {
            foreach (var file in files)
            {
                if (!ftpClient.Exists(file))
                {
                    using (var fileStream = new FileStream(file, FileMode.Open))
                    {
                        ftpClient.BufferSize = 4 * 1024; // bypass Payload error large files
                        ftpClient.UploadFile(fileStream, Path.GetFileName(file));
                    }
                }
            }
        }

        public void Delete(string[] files)
        {
            foreach (var file in files)
            {
                if(ftpClient.Exists(file))
                    ftpClient.DeleteFile(file);
            }
        }

        public bool Play(string file, int volume)
        {
            try
            {
                shellStream = sshClient.CreateShellStream("LightShow", 80, 24, 800, 600, 1024);
                shellStream.WriteLine("sudo amixer set PCM " + volume + "%");
                shellStream.WriteLine("sudo python /home/pi/lightshow/py/synchronized_lights.py --file=/home/pi/Music/\"" + file + "\"");
                return true;
            }
            catch
            {
                return false;
            }

        }

        public void Stop()
        {
            try
            {
                shellStream.WriteLine("\x03");
                Thread.Sleep(200);
                shellStream.Close();
            }
            catch { }
        }

        public void Exit()
        {
            if (IsConnected)
            {
                ftpClient.Disconnect();

                sshClient.Disconnect();

                shellStream.Close();

                IsConnected = false;
            }
        }

    }
}
